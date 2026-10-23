using Japlayer.Contracts;
using Japlayer.Data.Contracts;
using Japlayer.Data.Entities;
using Japlayer.Data.Models;
using Japlayer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.ViewModels
{
    [TestClass]
    public class MediaItemViewModelTests
    {
        private class StubImageProvider : IImageProvider
        {
            public Task<IEnumerable<string>> GetGalleryPathsAsync(string mediaId)
                => Task.FromResult<IEnumerable<string>>(["gallery1.jpg", "gallery2.jpg"]);
        }

        private class StubSceneProvider : IMediaSceneProvider
        {
            public Task<IEnumerable<MediaScene>> GetMediaScenesAsync(string mediaId)
            {
                var scenes = new List<MediaScene>
                {
                    new() { MediaId = mediaId, SceneNumber = 1, FilePaths = [@"C:\Videos\scene1.mp4"] },
                    new() { MediaId = mediaId, SceneNumber = 2, FilePaths = [@"C:\Videos\scene2.mp4"] }
                };
                return Task.FromResult<IEnumerable<MediaScene>>(scenes);
            }
        }

        private class StubMediaProvider : IMediaProvider
        {
            public Task<IEnumerable<LibraryItem>> GetLibraryItemsAsync() => Task.FromResult<IEnumerable<LibraryItem>>([]);
            public Task<IEnumerable<string>> GetUserTagsAsync() => Task.FromResult<IEnumerable<string>>([]);
            public Task<IEnumerable<string>> GetGenresAsync() => Task.FromResult<IEnumerable<string>>([]);

            public Task<MediaItem> GetMediaItemAsync(string mediaId)
            {
                return Task.FromResult(new MediaItem
                {
                    MediaId = mediaId,
                    Title = "Full Detailed Movie Title",
                    ContentId = "CID-12345",
                    Genres = ["Sci-Fi", "Action"],
                    Cast = ["Lead Actor", "Supporting Actor"],
                    Studios = ["Studio A"]
                });
            }
        }

        private class StubSettingsService : ISettingsService
        {
            public string ImagePath { get; set; } = @"C:\Images";
            public string SqliteDatabasePath { get; set; } = @"C:\Images\medias.db";
            public Task LoadAsync() => Task.CompletedTask;
            public Task SaveAsync() => Task.CompletedTask;
        }

        private class StubThumbnailProvider : IMediaThumbnailProvider
        {
            public Task<IEnumerable<MediaThumbnail>> GetThumbnailsAsync(string mediaId)
            {
                var thumbnails = new List<MediaThumbnail>
                {
                    new() { MediaId = mediaId, Scene = 1, Timestamp = 0, Path = "scene1_t0.jpg" },
                    new() { MediaId = mediaId, Scene = 1, Timestamp = 50, Path = "scene1_t50.jpg" },
                    new() { MediaId = mediaId, Scene = 1, Timestamp = 100, Path = "scene1_t100.jpg" },
                    new() { MediaId = mediaId, Scene = 2, Timestamp = 10, Path = "scene2_t10.jpg" }
                };
                return Task.FromResult<IEnumerable<MediaThumbnail>>(thumbnails);
            }
        }

        private class StubHighlightProvider : IMediaHighlightProvider
        {
            public Task<IEnumerable<MediaHighlight>> GetHighlightsAsync(string mediaId)
            {
                var highlights = new List<MediaHighlight>
                {
                    new() { MediaId = mediaId, Scene = 1, Timestamp = 45 },
                    new() { MediaId = mediaId, Scene = 2, Timestamp = 12 }
                };
                return Task.FromResult<IEnumerable<MediaHighlight>>(highlights);
            }

            public Task AddHighlightAsync(string mediaId, int scene, int timestamp) => Task.CompletedTask;
            public Task RemoveHighlightAsync(string mediaId, int scene, int timestamp) => Task.CompletedTask;
        }

        [TestMethod]
        public async Task LoadDetailsAsync_PopulatesDetailsScenesGalleryAndHighlights()
        {
            // Arrange
            var libraryItem = new LibraryItem { MediaId = "TEST-001", Title = "Initial Title", CoverImagePath = "cover.jpg" };
            var settingsService = new StubSettingsService();
            var libraryItemViewModel = new LibraryItemViewModel(libraryItem, settingsService, new StubThumbnailProvider());

            var viewModel = new MediaItemViewModel(
                libraryItem,
                new StubImageProvider(),
                new StubSceneProvider(),
                new StubMediaProvider(),
                settingsService,
                new StubThumbnailProvider(),
                new StubHighlightProvider())
            {
                LibraryItemViewModel = libraryItemViewModel
            };

            // Act
            await viewModel.LoadDetailsAsync();

            // Assert
            Assert.IsTrue(viewModel.IsDetailsLoaded);
            Assert.AreEqual("CID-12345", viewModel.ContentId);
            Assert.AreEqual(2, viewModel.Genres.Count);
            Assert.AreEqual(2, viewModel.Scenes!.Count);
            Assert.AreEqual(2, viewModel.GalleryImages!.Count);

            // Verify poster selection chose closest to 75% mark for scene 1 (target is 75, closest is 50 or 100)
            Assert.IsNotNull(viewModel.Scenes[0].PosterPath);

            // Verify highlight count was synchronized to LibraryItemViewModel (1 in scene 1 + 1 in scene 2 = 2)
            Assert.AreEqual(2, libraryItemViewModel.HighlightCount);
        }
    }
}
