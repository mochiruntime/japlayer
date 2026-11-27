using Japlayer.Data.Contracts;
using Microsoft.Data.Sqlite;

namespace Japlayer.Data.Services;

/// <summary>
/// Uses SQLite <c>PRAGMA data_version</c> on a dedicated read-only connection to detect
/// committed changes from external connections without false positives from WAL checkpoints.
/// </summary>
public sealed class SqliteDatabaseVersionTracker : IDatabaseVersionTracker
{
    private readonly object _versionLock = new();
    private SqliteConnection? _connection;
    private long? _knownDataVersion;
    private string _databasePath = string.Empty;
    private bool _disposed;

    public void Open(string databasePath)
    {
        lock (_versionLock)
        {
            if (_disposed)
            {
                return;
            }

            _databasePath = databasePath;
            ResetConnectionLocked();
            (_connection, _knownDataVersion) = TryOpenAndReadVersion(_databasePath);
        }
    }

    public void RefreshKnownVersion()
    {
        lock (_versionLock)
        {
            if (_disposed || string.IsNullOrWhiteSpace(_databasePath))
            {
                return;
            }

            if (_connection == null)
            {
                (_connection, _knownDataVersion) = TryOpenAndReadVersion(_databasePath);
                return;
            }

            var version = TryReadDataVersionLocked();
            if (version.HasValue)
            {
                _knownDataVersion = version;
            }
        }
    }

    public bool HasExternalChange()
    {
        lock (_versionLock)
        {
            if (_disposed)
            {
                return false;
            }

            if (_connection == null)
            {
                if (string.IsNullOrWhiteSpace(_databasePath))
                {
                    return true;
                }

                (_connection, _knownDataVersion) = TryOpenAndReadVersion(_databasePath);
                // Non-SQLite or inaccessible file: fall back to FileSystemWatcher notification.
                return _connection == null || !_knownDataVersion.HasValue;
            }

            var currentVersion = TryReadDataVersionLocked();
            if (!currentVersion.HasValue)
            {
                ResetConnectionLocked();
                if (!string.IsNullOrWhiteSpace(_databasePath))
                {
                    (_connection, _knownDataVersion) = TryOpenAndReadVersion(_databasePath);
                }

                return true;
            }

            if (_knownDataVersion.HasValue && currentVersion.Value == _knownDataVersion.Value)
            {
                return false;
            }

            _knownDataVersion = currentVersion.Value;
            return true;
        }
    }

    public void Reset()
    {
        lock (_versionLock)
        {
            ResetConnectionLocked();
        }
    }

    public void Dispose()
    {
        lock (_versionLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ResetConnectionLocked();
        }
    }

    private long? TryReadDataVersionLocked()
    {
        if (_connection == null)
        {
            return null;
        }

        try
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "PRAGMA data_version;";
            var result = command.ExecuteScalar();
            return result is long version ? version : Convert.ToInt64(result);
        }
        catch
        {
            return null;
        }
    }

    private static (SqliteConnection? Connection, long? DataVersion) TryOpenAndReadVersion(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
        {
            return (null, null);
        }

        SqliteConnection? connection = null;
        try
        {
            connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA data_version;";
            var result = command.ExecuteScalar();
            var version = result is long parsedVersion ? parsedVersion : Convert.ToInt64(result);
            return (connection, version);
        }
        catch
        {
            connection?.Dispose();
            return (null, null);
        }
    }

    private void ResetConnectionLocked()
    {
        _connection?.Dispose();
        _connection = null;
        _knownDataVersion = null;
    }
}
