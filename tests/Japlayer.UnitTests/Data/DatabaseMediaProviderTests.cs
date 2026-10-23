using Japlayer.Data.Context;
using Japlayer.Data.Entities;
using Japlayer.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.Data
{
    [TestClass]
    public class DatabaseMediaProviderTests : IDisposable
    {
        private readonly SqliteConnection _sqliteConnection;
        private readonly DatabaseContext _databaseContext;
        private readonly DatabaseMediaProvider _mediaProvider;

        public DatabaseMediaProviderTests()
        {
            _sqliteConnection = new SqliteConnection("Data Source=:memory:");
            _sqliteConnection.Open();

            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseSqlite(_sqliteConnection)
                .Options;

            _databaseContext = new DatabaseContext(options);
            _databaseContext.Database.EnsureCreated();

            _mediaProvider = new DatabaseMediaProvider(_databaseContext);
        }

        public void Dispose()
        {
            _databaseContext.Dispose();
            _sqliteConnection.Dispose();
        }

        [TestMethod]
        public async Task GetLibraryItemsAsync_OnlyReturnsMediaWithLocations()
        {
            // Arrange
            var mediaWithLocation = new Media { MediaId = "WITH-LOC" };
            mediaWithLocation.MediaLocations.Add(new MediaLocation
            {
                MediaId = "WITH-LOC",
                Scene = 1,
                Hostname = "localhost",
                Path = @"C:\video.mp4",
                CreatedAt = new DateTime(2024, 1, 10, 12, 0, 0, DateTimeKind.Utc)
            });
            mediaWithLocation.MediaMetadata.Add(new MediaMetadata
            {
                MetadataUrl = "https://example.com/with-loc",
                MediaId = "WITH-LOC",
                Title = "Has Location Title",
                Cover = null
            });

            var mediaWithoutLocation = new Media { MediaId = "NO-LOC" };

            _databaseContext.Media.AddRange(mediaWithLocation, mediaWithoutLocation);
            await _databaseContext.SaveChangesAsync();

            // Act
            var libraryItems = (await _mediaProvider.GetLibraryItemsAsync()).ToList();

            // Assert
            Assert.AreEqual(1, libraryItems.Count);
            Assert.AreEqual("WITH-LOC", libraryItems[0].MediaId);
            Assert.AreEqual("Has Location Title", libraryItems[0].Title);
            Assert.IsNull(libraryItems[0].CoverImagePath);
        }

        [TestMethod]
        public async Task GetUserTagsAsync_ReturnsDistinctOrderedTags()
        {
            // Arrange
            var media = new Media { MediaId = "M1" };
            media.UserTags.Add(new UserTag { Name = "VR" });
            media.UserTags.Add(new UserTag { Name = "Action" });

            var media2 = new Media { MediaId = "M2" };
            media2.UserTags.Add(new UserTag { Name = "VR" }); // Duplicate
            media2.UserTags.Add(new UserTag { Name = "Comedy" });

            _databaseContext.Media.AddRange(media, media2);
            await _databaseContext.SaveChangesAsync();

            // Act
            var tags = (await _mediaProvider.GetUserTagsAsync()).ToList();

            // Assert
            Assert.AreEqual(3, tags.Count);
            Assert.AreEqual("Action", tags[0]);
            Assert.AreEqual("Comedy", tags[1]);
            Assert.AreEqual("VR", tags[2]);
        }

        [TestMethod]
        public async Task GetMediaItemAsync_LoadsFullNavigationPropertiesAndConvertsRuntime()
        {
            // Arrange
            var media = new Media { MediaId = "DETAIL-001" };
            media.MediaMetadata.Add(new MediaMetadata
            {
                MetadataUrl = "https://example.com/detail-001",
                MediaId = "DETAIL-001",
                Title = "Detail Movie",
                ContentId = "CID-999",
                RuntimeMinutes = 120
            });
            media.Studios.Add(new MediaStudio { Name = "Major Studio" });
            media.Series.Add(new MediaSeries { Name = "Epic Series" });
            media.Genres.Add(new MediaGenre { Name = "Sci-Fi" });

            _databaseContext.Media.Add(media);
            await _databaseContext.SaveChangesAsync();

            // Act
            var mediaItem = await _mediaProvider.GetMediaItemAsync("DETAIL-001");

            // Assert
            Assert.IsNotNull(mediaItem);
            Assert.AreEqual("DETAIL-001", mediaItem.MediaId);
            Assert.AreEqual("Detail Movie", mediaItem.Title);
            Assert.AreEqual("CID-999", mediaItem.ContentId);
            Assert.AreEqual(TimeSpan.FromMinutes(120), mediaItem.Runtime);
            Assert.AreEqual("Major Studio", mediaItem.Studios.Single());
            Assert.AreEqual("Epic Series", mediaItem.Series.Single());
            Assert.AreEqual("Sci-Fi", mediaItem.Genres.Single());
        }
    }
}
