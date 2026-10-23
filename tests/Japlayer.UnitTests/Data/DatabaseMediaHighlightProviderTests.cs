using Japlayer.Data.Context;
using Japlayer.Data.Entities;
using Japlayer.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.Data
{
    [TestClass]
    public class DatabaseMediaHighlightProviderTests : IDisposable
    {
        private readonly SqliteConnection _sqliteConnection;
        private readonly DatabaseContext _databaseContext;
        private readonly DatabaseMediaHighlightProvider _highlightProvider;

        public DatabaseMediaHighlightProviderTests()
        {
            _sqliteConnection = new SqliteConnection("Data Source=:memory:");
            _sqliteConnection.Open();

            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseSqlite(_sqliteConnection)
                .Options;

            _databaseContext = new DatabaseContext(options);
            _databaseContext.Database.EnsureCreated();

            _databaseContext.Media.Add(new Media { MediaId = "TEST-001" });
            _databaseContext.SaveChanges();

            _highlightProvider = new DatabaseMediaHighlightProvider(_databaseContext);
        }

        public void Dispose()
        {
            _databaseContext.Dispose();
            _sqliteConnection.Dispose();
        }

        [TestMethod]
        public async Task AddHighlightAsync_InsertsNewHighlightRow()
        {
            // Act
            await _highlightProvider.AddHighlightAsync("TEST-001", 1, 42);

            // Assert
            var highlights = (await _highlightProvider.GetHighlightsAsync("TEST-001")).ToList();
            Assert.AreEqual(1, highlights.Count);
            Assert.AreEqual("TEST-001", highlights[0].MediaId);
            Assert.AreEqual(1, highlights[0].Scene);
            Assert.AreEqual(42, highlights[0].Timestamp);
        }

        [TestMethod]
        public async Task AddHighlightAsync_WhenHighlightAlreadyExists_DoesNotDuplicate()
        {
            // Arrange
            await _highlightProvider.AddHighlightAsync("TEST-001", 1, 42);

            // Act
            await _highlightProvider.AddHighlightAsync("TEST-001", 1, 42);

            // Assert
            var highlights = (await _highlightProvider.GetHighlightsAsync("TEST-001")).ToList();
            Assert.AreEqual(1, highlights.Count);
        }

        [TestMethod]
        public async Task RemoveHighlightAsync_DeletesExistingHighlight()
        {
            // Arrange
            await _highlightProvider.AddHighlightAsync("TEST-001", 1, 42);
            await _highlightProvider.AddHighlightAsync("TEST-001", 1, 100);

            // Act
            await _highlightProvider.RemoveHighlightAsync("TEST-001", 1, 42);

            // Assert
            var highlights = (await _highlightProvider.GetHighlightsAsync("TEST-001")).ToList();
            Assert.AreEqual(1, highlights.Count);
            Assert.AreEqual(100, highlights[0].Timestamp);
        }

        [TestMethod]
        public async Task GetHighlightsAsync_OrdersBySceneThenTimestamp()
        {
            // Arrange
            await _highlightProvider.AddHighlightAsync("TEST-001", 2, 10);
            await _highlightProvider.AddHighlightAsync("TEST-001", 1, 50);
            await _highlightProvider.AddHighlightAsync("TEST-001", 1, 20);

            // Act
            var highlights = (await _highlightProvider.GetHighlightsAsync("TEST-001")).ToList();

            // Assert
            Assert.AreEqual(3, highlights.Count);
            Assert.AreEqual((1, 20), (highlights[0].Scene, highlights[0].Timestamp));
            Assert.AreEqual((1, 50), (highlights[1].Scene, highlights[1].Timestamp));
            Assert.AreEqual((2, 10), (highlights[2].Scene, highlights[2].Timestamp));
        }
    }
}
