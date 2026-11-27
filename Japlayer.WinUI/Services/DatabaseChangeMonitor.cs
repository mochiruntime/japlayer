#nullable enable
using System;
using System.IO;
using System.Threading;
using Japlayer.Contracts;
using Japlayer.Data.Contracts;
using Japlayer.Data.Services;
using Microsoft.UI.Dispatching;

namespace Japlayer.Services
{
    public interface IDatabaseChangeMonitor : IDisposable
    {
        public event EventHandler? DatabaseChangedExternally;

        public void Start();

        public IDisposable BeginInternalWrite();
    }

    /// <summary>
    /// Watches the SQLite database and its WAL file for external writes, debouncing bursts
    /// and marshaling notifications onto the UI DispatcherQueue.
    /// </summary>
    public sealed class DatabaseChangeMonitor(
        ISettingsService settingsService,
        int debounceMilliseconds = 1000,
        int internalWriteGraceMilliseconds = 250,
        IDatabaseVersionTracker? versionTracker = null) : IDatabaseChangeMonitor
    {
        private readonly ISettingsService _settingsService = settingsService;
        private readonly int _debounceMilliseconds = debounceMilliseconds;
        private readonly int _internalWriteGraceMilliseconds = internalWriteGraceMilliseconds;
        private readonly IDatabaseVersionTracker _versionTracker = versionTracker ?? new SqliteDatabaseVersionTracker();
        private readonly object _syncLock = new();
        private FileSystemWatcher? _watcher;
        private Timer? _debounceTimer;
        private DispatcherQueue? _dispatcherQueue;
        private string _databaseFileName = string.Empty;
        private string _walFileName = string.Empty;
        private int _activeInternalWrites;
        private long _lastInternalWriteEndTick;
        private bool _disposed;

        public event EventHandler? DatabaseChangedExternally;

        public void Start()
        {
            lock (_syncLock)
            {
                if (_disposed || _watcher != null)
                {
                    return;
                }

                var databasePath = _settingsService.SqliteDatabasePath;
                if (string.IsNullOrWhiteSpace(databasePath))
                {
                    return;
                }

                var directory = Path.GetDirectoryName(databasePath);
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                {
                    return;
                }

                _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                _databaseFileName = Path.GetFileName(databasePath);
                _walFileName = _databaseFileName + "-wal";

                _versionTracker.Open(databasePath);

                _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

                _watcher = new FileSystemWatcher(directory)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                _watcher.Changed += OnFileSystemChanged;
                _watcher.Created += OnFileSystemChanged;
                _watcher.Renamed += OnFileSystemRenamed;
                _watcher.Error += OnWatcherError;
            }
        }

        public IDisposable BeginInternalWrite()
        {
            Interlocked.Increment(ref _activeInternalWrites);
            return new InternalWriteScope(this);
        }

        private void EndInternalWrite()
        {
            Volatile.Write(ref _lastInternalWriteEndTick, Environment.TickCount64);
            if (Interlocked.Decrement(ref _activeInternalWrites) == 0)
            {
                _versionTracker.RefreshKnownVersion();
            }
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs eventArgs)
        {
            if (!IsTargetDatabaseFile(eventArgs.Name))
            {
                return;
            }

            if (eventArgs.ChangeType == WatcherChangeTypes.Created &&
                string.Equals(eventArgs.Name, _databaseFileName, StringComparison.OrdinalIgnoreCase))
            {
                _versionTracker.Reset();
            }

            ScheduleDebouncedNotificationIfExternal();
        }

        private void OnFileSystemRenamed(object sender, RenamedEventArgs eventArgs)
        {
            if (!IsTargetDatabaseFile(eventArgs.Name) && !IsTargetDatabaseFile(eventArgs.OldName))
            {
                return;
            }

            if (string.Equals(eventArgs.Name, _databaseFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(eventArgs.OldName, _databaseFileName, StringComparison.OrdinalIgnoreCase))
            {
                _versionTracker.Reset();
            }

            ScheduleDebouncedNotificationIfExternal();
        }

        private void OnWatcherError(object sender, ErrorEventArgs eventArgs) =>
            // Recover from FileSystemWatcher buffer overflows by triggering a coalesced reload.
            ScheduleDebouncedNotificationIfExternal();

        private bool IsTargetDatabaseFile(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            return string.Equals(fileName, _databaseFileName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, _walFileName, StringComparison.OrdinalIgnoreCase);
        }

        private void ScheduleDebouncedNotificationIfExternal()
        {
            if (IsWithinInternalWriteWindow())
            {
                return;
            }

            lock (_syncLock)
            {
                if (_disposed || _debounceTimer == null)
                {
                    return;
                }

                _debounceTimer.Change(_debounceMilliseconds, Timeout.Infinite);
            }
        }

        private bool IsWithinInternalWriteWindow()
        {
            if (Volatile.Read(ref _activeInternalWrites) > 0)
            {
                return true;
            }

            var elapsedSinceInternalWrite = Environment.TickCount64 - Volatile.Read(ref _lastInternalWriteEndTick);
            return elapsedSinceInternalWrite >= 0 && elapsedSinceInternalWrite < _internalWriteGraceMilliseconds;
        }

        private void OnDebounceElapsed(object? state)
        {
            if (_disposed || IsWithinInternalWriteWindow() || !_versionTracker.HasExternalChange())
            {
                return;
            }

            if (_dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    if (!_disposed)
                    {
                        DatabaseChangedExternally?.Invoke(this, EventArgs.Empty);
                    }
                });
            }
            else
            {
                DatabaseChangedExternally?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            lock (_syncLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Changed -= OnFileSystemChanged;
                    _watcher.Created -= OnFileSystemChanged;
                    _watcher.Renamed -= OnFileSystemRenamed;
                    _watcher.Error -= OnWatcherError;
                    _watcher.Dispose();
                    _watcher = null;
                }

                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }

            _versionTracker.Dispose();
        }

        private sealed class InternalWriteScope(DatabaseChangeMonitor monitor) : IDisposable
        {
            private DatabaseChangeMonitor? _monitor = monitor;

            public void Dispose() => Interlocked.Exchange(ref _monitor, null)?.EndInternalWrite();
        }
    }
}
