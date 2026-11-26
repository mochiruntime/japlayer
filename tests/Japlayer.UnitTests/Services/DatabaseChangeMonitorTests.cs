using Japlayer.Contracts;
using Japlayer.Services;
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
    }
}
