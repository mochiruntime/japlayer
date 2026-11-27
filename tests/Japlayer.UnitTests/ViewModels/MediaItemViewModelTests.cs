using System.Collections.Specialized;
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
            public Task<IEnumerable<string>> GetCastAsync() => Task.FromResult<IEnumerable<string>>([]);
            public Task<IEnumerable<string>> GetStudiosAsync() => Task.FromResult<IEnumerable<string>>([]);
            public Task<IEnumerable<string>> GetSeriesAsync() => Task.FromResult<IEnumerable<string>>([]);

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

            public Task AddUserTagAsync(string mediaId, string tagName) => Task.CompletedTask;
            public Task RemoveUserTagAsync(string mediaId, string tagName) => Task.CompletedTask;
            public Task SetRatingAsync(string mediaId, int rating) => Task.CompletedTask;
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

        private class EmptyThumbnailProvider : IMediaThumbnailProvider
        {
            public Task<IEnumerable<MediaThumbnail>> GetThumbnailsAsync(string mediaId) => Task.FromResult<IEnumerable<MediaThumbnail>>([]);
        }

        [TestMethod]
        public async Task LoadDetailsAsync_WhenNoThumbnailsExist_LeavesScenePosterPathNull()
        {
            // Arrange
            var libraryItem = new LibraryItem { MediaId = "TEST-002", Title = "No Thumbnails" };
            var settingsService = new StubSettingsService();
            var viewModel = new MediaItemViewModel(
                libraryItem,
                new StubImageProvider(),
                new StubSceneProvider(),
                new StubMediaProvider(),
                settingsService,
                new EmptyThumbnailProvider(),
                new StubHighlightProvider());

            // Act
            await viewModel.LoadDetailsAsync();

            // Assert
            Assert.IsNotNull(viewModel.SelectedScene);
            Assert.IsNull(viewModel.SelectedScene.PosterPath);
        }

        [TestMethod]
        public void SetGenreCollapseLimit_WhenCalledWithSameValue_DoesNotRaisePropertyChanged()
        {
            // Arrange
            var libraryItem = new LibraryItem { MediaId = "TEST-003", Title = "Genre Limit" };
            var viewModel = new MediaItemViewModel(
                libraryItem,
                new StubImageProvider(),
                new StubSceneProvider(),
                new StubMediaProvider(),
                new StubSettingsService(),
                new EmptyThumbnailProvider(),
                new StubHighlightProvider());
            viewModel.SetGenreCollapseLimit(3);

            var raisedCount = 0;
            viewModel.PropertyChanged += (_, _) => raisedCount++;

            // Act
            viewModel.SetGenreCollapseLimit(3);

            // Assert
            Assert.AreEqual(0, raisedCount);
        }

        [TestMethod]
        public void TagSuggestionItem_ToString_ReturnsName()
        {
            var existingSuggestion = new TagSuggestionItem("2D", false);
            var newSuggestion = new TagSuggestionItem("CustomTag", true);

            Assert.AreEqual("2D", existingSuggestion.ToString());
            Assert.AreEqual("CustomTag", newSuggestion.ToString());
        }

        private sealed class TagConfigurableMediaProvider(IEnumerable<string> availableTags, IEnumerable<string> itemTags) : IMediaProvider
        {
            private readonly List<string> _availableTags = [.. availableTags];
            private readonly List<string> _itemTags = [.. itemTags];

            public Task<IEnumerable<LibraryItem>> GetLibraryItemsAsync() => Task.FromResult<IEnumerable<LibraryItem>>([]);
            public Task<IEnumerable<string>> GetUserTagsAsync() => Task.FromResult<IEnumerable<string>>(_availableTags);
            public Task<IEnumerable<string>> GetGenresAsync() => Task.FromResult<IEnumerable<string>>([]);
            public Task<IEnumerable<string>> GetCastAsync() => Task.FromResult<IEnumerable<string>>([]);
            public Task<IEnumerable<string>> GetStudiosAsync() => Task.FromResult<IEnumerable<string>>([]);
            public Task<IEnumerable<string>> GetSeriesAsync() => Task.FromResult<IEnumerable<string>>([]);

            public Task<MediaItem> GetMediaItemAsync(string mediaId) => Task.FromResult(new MediaItem
            {
                MediaId = mediaId,
                Title = "Tag Test Item",
                UserTags = [.. _itemTags]
            });

            public Task AddUserTagAsync(string mediaId, string tagName) => Task.CompletedTask;
            public Task RemoveUserTagAsync(string mediaId, string tagName) => Task.CompletedTask;
            public Task SetRatingAsync(string mediaId, int rating) => Task.CompletedTask;
        }

        [TestMethod]
        public async Task TagSuggestions_ReactivelyUpdateOnLoadSearchAddAndRemove()
        {
            var libraryItem = new LibraryItem { MediaId = "TAG-001", Title = "Tag Test" };
            var viewModel = new MediaItemViewModel(
                libraryItem,
                new StubImageProvider(),
                new StubSceneProvider(),
                new TagConfigurableMediaProvider(["2D", "VR"], ["VR"]),
                new StubSettingsService(),
                new EmptyThumbnailProvider(),
                new StubHighlightProvider());

            var initialCollection = viewModel.TagSuggestions;
            var resetCount = 0;
            viewModel.TagSuggestions.CollectionChanged += (_, eventArgs) =>
            {
                if (eventArgs.Action == NotifyCollectionChangedAction.Reset)
                {
                    resetCount++;
                }
            };

            await viewModel.LoadDetailsAsync();

            Assert.AreSame(initialCollection, viewModel.TagSuggestions);
            Assert.AreEqual(1, viewModel.TagSuggestions.Count);
            Assert.AreEqual(new TagSuggestionItem("2D", false), viewModel.TagSuggestions[0]);

            viewModel.TagSearchText = "NewTag";
            Assert.AreEqual(1, viewModel.TagSuggestions.Count);
            Assert.AreEqual(new TagSuggestionItem("NewTag", true), viewModel.TagSuggestions[0]);

            await viewModel.AddUserTagAsync("NewTag");
            viewModel.TagSearchText = string.Empty;

            CollectionAssert.Contains(viewModel.UserTags, "NewTag");
            CollectionAssert.Contains(viewModel.AllAvailableTags, "NewTag");
            Assert.AreEqual(1, viewModel.TagSuggestions.Count);
            Assert.AreEqual(new TagSuggestionItem("2D", false), viewModel.TagSuggestions[0]);

            await viewModel.RemoveUserTagAsync("NewTag");

            Assert.AreEqual(2, viewModel.TagSuggestions.Count);
            CollectionAssert.AreEqual(
                new[] { new TagSuggestionItem("2D", false), new TagSuggestionItem("NewTag", false) },
                viewModel.TagSuggestions.ToList());
            Assert.IsTrue(resetCount >= 4);
        }

        [TestMethod]
        public async Task AddUserTagAsync_WhenTagMatchesExistingCaseInsensitively_UsesCanonicalCasing()
        {
            var libraryItem = new LibraryItem { MediaId = "TAG-002", Title = "Tag Casing Test" };
            var viewModel = new MediaItemViewModel(
                libraryItem,
                new StubImageProvider(),
                new StubSceneProvider(),
                new TagConfigurableMediaProvider(["2D", "VR"], ["VR"]),
                new StubSettingsService(),
                new EmptyThumbnailProvider(),
                new StubHighlightProvider());

            await viewModel.LoadDetailsAsync();
            await viewModel.AddUserTagAsync("2d");

            CollectionAssert.Contains(viewModel.UserTags, "2D");
            CollectionAssert.DoesNotContain(viewModel.UserTags, "2d");
            Assert.AreEqual(2, viewModel.AllAvailableTags.Count);
            Assert.AreEqual(0, viewModel.TagSuggestions.Count);
        }
    }
}
