using Japlayer.Data.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.Data
{
    [TestClass]
    public class SqlitePragmaInterceptorTests
    {
        [TestMethod]
        public async Task ConnectionOpenedAsync_SetsWalModeAndBusyTimeout()
        {
            var databaseFile = Path.Combine(Path.GetTempPath(), $"japlayer_wal_test_{Guid.NewGuid():N}.db");
            try
            {
                var options = new DbContextOptionsBuilder<DatabaseContext>()
                    .UseSqlite($"Data Source={databaseFile}")
                    .AddInterceptors(new SqlitePragmaInterceptor())
                    .Options;

                await using var context = new DatabaseContext(options);
                await context.Database.OpenConnectionAsync();

                var connection = context.Database.GetDbConnection();

                await using var journalCommand = connection.CreateCommand();
                journalCommand.CommandText = "PRAGMA journal_mode;";
                var journalMode = (await journalCommand.ExecuteScalarAsync())?.ToString();

                await using var timeoutCommand = connection.CreateCommand();
                timeoutCommand.CommandText = "PRAGMA busy_timeout;";
                var busyTimeout = Convert.ToInt32(await timeoutCommand.ExecuteScalarAsync());

                Assert.AreEqual("wal", journalMode, ignoreCase: true);
                Assert.AreEqual(10000, busyTimeout);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(databaseFile))
                {
                    File.Delete(databaseFile);
                }
                if (File.Exists($"{databaseFile}-wal"))
                {
                    File.Delete($"{databaseFile}-wal");
                }
                if (File.Exists($"{databaseFile}-shm"))
                {
                    File.Delete($"{databaseFile}-shm");
                }
            }
        }
    }
}
