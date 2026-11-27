using Japlayer.Contracts;
using Japlayer.Data.Contracts;
using Japlayer.Services;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.Services
{
    [TestClass]
    public class DatabaseChangeMonitorTests : IDisposable
    {
        private sealed class StubSettingsService(string databasePath) : ISettingsService
        {
            public string ImagePath { get; set; } = Path.GetDirectoryName(databasePath)!;
            public string SqliteDatabasePath { get; set; } = databasePath;
            public Task LoadAsync() => Task.CompletedTask;
            public Task SaveAsync() => Task.CompletedTask;
        }

        private readonly string _tempDirectory;
        private readonly string _databasePath;
        private readonly string _walPath;
        private readonly DatabaseChangeMonitor _monitor;

        public DatabaseChangeMonitorTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "JaplayerMonitorTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            _databasePath = Path.Combine(_tempDirectory, "medias.db");
            _walPath = _databasePath + "-wal";
            File.WriteAllText(_databasePath, "initial");
            File.WriteAllText(_walPath, "initial");

            _monitor = new DatabaseChangeMonitor(
                new StubSettingsService(_databasePath),
                debounceMilliseconds: 80,
                internalWriteGraceMilliseconds: 200);
        }

        public void Dispose()
        {
            _monitor.Dispose();
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch
            {
            }
        }

        [TestMethod]
        public async Task ExternalWriteToDatabaseOrWal_DebouncesBurstIntoSingleEvent()
        {
            var eventCount = 0;
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _monitor.DatabaseChangedExternally += (_, _) =>
            {
                Interlocked.Increment(ref eventCount);
                signal.TrySetResult();
            };

            _monitor.Start();

            // Burst multiple writes to DB and WAL
            await File.WriteAllTextAsync(_databasePath, "write1");
            await File.WriteAllTextAsync(_walPath, "wal1");
            await File.WriteAllTextAsync(_walPath, "wal2");

            var completed = await Task.WhenAny(signal.Task, Task.Delay(2000));
            Assert.AreSame(signal.Task, completed, "Expected debounced DatabaseChangedExternally notification.");

            await Task.Delay(200);
            Assert.AreEqual(1, Volatile.Read(ref eventCount));
        }

        [TestMethod]
        public async Task UnrelatedFileAndInternalWrite_AreIgnored()
        {
            var eventCount = 0;
            _monitor.DatabaseChangedExternally += (_, _) => Interlocked.Increment(ref eventCount);
            _monitor.Start();

            // Unrelated file in same folder
            await File.WriteAllTextAsync(Path.Combine(_tempDirectory, "unrelated.txt"), "noise");

            // Internal write within BeginInternalWrite() scope
            using (_monitor.BeginInternalWrite())
            {
                await File.WriteAllTextAsync(_walPath, "internal-write");
            }

            await Task.Delay(350);
            Assert.AreEqual(0, Volatile.Read(ref eventCount));
        }

        [TestMethod]
        public async Task Dispose_StopsRaisingEvents()
        {
            var eventCount = 0;
            _monitor.DatabaseChangedExternally += (_, _) => Interlocked.Increment(ref eventCount);
            _monitor.Start();
            _monitor.Dispose();

            await File.WriteAllTextAsync(_databasePath, "after-dispose");
            await Task.Delay(250);

            Assert.AreEqual(0, Volatile.Read(ref eventCount));
        }

        [TestMethod]
        public async Task InternalSqliteWriteAndDelayedWalCheckpoint_DoNotRaiseExternalChange()
        {
            var sqliteDbPath = Path.Combine(_tempDirectory, "wal_internal.db");
            var connectionString = await CreateWalDatabaseAsync(sqliteDbPath);

            using var sqliteMonitor = new DatabaseChangeMonitor(
                new StubSettingsService(sqliteDbPath),
                debounceMilliseconds: 80,
                internalWriteGraceMilliseconds: 200);

            var eventCount = 0;
            sqliteMonitor.DatabaseChangedExternally += (_, _) => Interlocked.Increment(ref eventCount);
            sqliteMonitor.Start();

            using (sqliteMonitor.BeginInternalWrite())
            {
                await using var writeConnection = new SqliteConnection(connectionString);
                await writeConnection.OpenAsync();
                await using var command = writeConnection.CreateCommand();
                command.CommandText = "UPDATE Items SET Rating = 5 WHERE Id = 'M1';";
                await command.ExecuteNonQueryAsync();
            }

            // Wait past the 200ms internal write grace window, then trigger an automatic-style PASSIVE WAL checkpoint
            await Task.Delay(320);

            await using (var checkpointConnection = new SqliteConnection(connectionString))
            {
                await checkpointConnection.OpenAsync();
                await using var command = checkpointConnection.CreateCommand();
                command.CommandText = "PRAGMA wal_checkpoint(PASSIVE);";
                await command.ExecuteNonQueryAsync();
            }

            await Task.Delay(250);
            Assert.AreEqual(0, Volatile.Read(ref eventCount), "Internal write and subsequent WAL checkpoint should not raise DatabaseChangedExternally.");
        }

        [TestMethod]
        public async Task ExternalSqliteCommit_RaisesExternalChange()
        {
            var sqliteDbPath = Path.Combine(_tempDirectory, "wal_external.db");
            var connectionString = await CreateWalDatabaseAsync(sqliteDbPath);

            using var sqliteMonitor = new DatabaseChangeMonitor(
                new StubSettingsService(sqliteDbPath),
                debounceMilliseconds: 80,
                internalWriteGraceMilliseconds: 200);

            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventCount = 0;
            sqliteMonitor.DatabaseChangedExternally += (_, _) =>
            {
                Interlocked.Increment(ref eventCount);
                signal.TrySetResult();
            };
            sqliteMonitor.Start();

            await using (var externalConnection = new SqliteConnection(connectionString))
            {
                await externalConnection.OpenAsync();
                await using var command = externalConnection.CreateCommand();
                command.CommandText = "INSERT INTO Items VALUES ('M2', 4);";
                await command.ExecuteNonQueryAsync();
            }

            var completed = await Task.WhenAny(signal.Task, Task.Delay(2000));
            Assert.AreSame(signal.Task, completed, "Expected DatabaseChangedExternally after external SQLite commit.");
            await Task.Delay(150);
            Assert.AreEqual(1, Volatile.Read(ref eventCount));
        }

        [TestMethod]
        public async Task VersionTracker_SuppressesNotificationWhenHasExternalChangeIsFalse()
        {
            var tracker = new StubDatabaseVersionTracker { ExternalChangeResult = false };
            using var monitor = new DatabaseChangeMonitor(
                new StubSettingsService(_databasePath),
                debounceMilliseconds: 60,
                internalWriteGraceMilliseconds: 50,
                versionTracker: tracker);

            var eventCount = 0;
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            monitor.DatabaseChangedExternally += (_, _) =>
            {
                Interlocked.Increment(ref eventCount);
                signal.TrySetResult();
            };
            monitor.Start();
            Assert.IsTrue(tracker.IsOpened);

            await File.WriteAllTextAsync(_databasePath, "checkpoint-noise");
            await Task.Delay(200);
            Assert.AreEqual(0, Volatile.Read(ref eventCount));

            tracker.ExternalChangeResult = true;
            await File.WriteAllTextAsync(_databasePath, "external-commit");
            var completed = await Task.WhenAny(signal.Task, Task.Delay(2000));
            Assert.AreSame(signal.Task, completed);
            Assert.AreEqual(1, Volatile.Read(ref eventCount));
        }

        private static async Task<string> CreateWalDatabaseAsync(string databasePath)
        {
            var connectionString = $"Data Source={databasePath};Pooling=False";
            await using var setupConnection = new SqliteConnection(connectionString);
            await setupConnection.OpenAsync();
            await using var command = setupConnection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE Items (Id TEXT PRIMARY KEY, Rating INT); INSERT INTO Items VALUES ('M1', 1);";
            await command.ExecuteNonQueryAsync();
            return connectionString;
        }

        private sealed class StubDatabaseVersionTracker : IDatabaseVersionTracker
        {
            public bool IsOpened { get; private set; }
            public bool ExternalChangeResult { get; set; }

            public void Open(string databasePath) => IsOpened = true;
            public void RefreshKnownVersion() { }
            public bool HasExternalChange() => ExternalChangeResult;
            public void Reset() { }
            public void Dispose() { }
        }
    }
}
