using Japlayer.Contracts;
using Japlayer.Data.Contracts;
using Japlayer.Data.Entities;
using Japlayer.Data.Models;
using Japlayer.Models;
using Japlayer.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.ViewModels
{
    [TestClass]
    public class LibraryViewModelTests
    {
        private class StubMediaProvider : IMediaProvider
        {
            public List<LibraryItem> LibraryItemsToReturn { get; set; } = [];
            public List<string> UserTagsToReturn { get; set; } = [];
            public List<string> GenresToReturn { get; set; } = [];

            public Task<IEnumerable<LibraryItem>> GetLibraryItemsAsync()
                => Task.FromResult<IEnumerable<LibraryItem>>(LibraryItemsToReturn);

            public Task<IEnumerable<string>> GetUserTagsAsync()
                => Task.FromResult<IEnumerable<string>>(UserTagsToReturn);

            public Task<IEnumerable<string>> GetGenresAsync()
                => Task.FromResult<IEnumerable<string>>(GenresToReturn);

            public Task<MediaItem> GetMediaItemAsync(string mediaId)
                => Task.FromResult(new MediaItem { MediaId = mediaId });
        }

        private class StubMediaSceneProvider : IMediaSceneProvider
        {
            public Task<IEnumerable<MediaScene>> GetMediaScenesAsync(string mediaId)
                => Task.FromResult<IEnumerable<MediaScene>>([]);
        }

        private class StubThumbnailProvider : IMediaThumbnailProvider
        {
            public Task<IEnumerable<MediaThumbnail>> GetThumbnailsAsync(string mediaId)
                => Task.FromResult<IEnumerable<MediaThumbnail>>([]);
        }

        private class StubSettingsService : ISettingsService
        {
            public string ImagePath { get; set; } = @"C:\Images";
            public string SqliteDatabasePath { get; set; } = @"C:\Images\medias.db";
            public Task LoadAsync() => Task.CompletedTask;
            public Task SaveAsync() => Task.CompletedTask;
        }

        private IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISettingsService, StubSettingsService>();
            services.AddSingleton<IMediaThumbnailProvider, StubThumbnailProvider>();
            return services.BuildServiceProvider();
        }

        [TestMethod]
        public async Task LoadDataAsync_PopulatesMediaItemsAndFilterOptions()
        {
            // Arrange
            var mediaProvider = new StubMediaProvider
            {
                LibraryItemsToReturn =
                [
                    new() { MediaId = "AAA-001", Title = "First Movie", HighlightCount = 2, CreatedAt = DateTime.UtcNow.AddDays(-2) },
                    new() { MediaId = "BBB-002", Title = "Second Movie", HighlightCount = 5, CreatedAt = DateTime.UtcNow.AddDays(-1) }
                ],
                UserTagsToReturn = ["VR", "Favorite"],
                GenresToReturn = ["Action", "Comedy"]
            };
            var sceneProvider = new StubMediaSceneProvider();
            var serviceProvider = CreateServiceProvider();

            var viewModel = new LibraryViewModel(mediaProvider, sceneProvider, serviceProvider);

            // Act
            await viewModel.LoadDataAsync();

            // Assert
            Assert.IsTrue(viewModel.IsDataLoaded);
            Assert.AreEqual(2, viewModel.MediaItems.Count);
            Assert.AreEqual(2, viewModel.TagFilterItems.Count);
            Assert.AreEqual(2, viewModel.GenreFilterItems.Count);
        }

        [TestMethod]
        public async Task SearchText_FiltersMediaItemsCaseInsensitiveAndNormalized()
        {
            // Arrange
            var mediaProvider = new StubMediaProvider
            {
                LibraryItemsToReturn =
                [
                    new() { MediaId = "VRKM-1023", Title = "Cosplay Special" },
                    new() { MediaId = "DSVR-0572", Title = "Beach Vacation" },
                    new() { MediaId = "KIWVR-00394", Title = "Another Title" }
                ]
            };
            var sceneProvider = new StubMediaSceneProvider();
            var serviceProvider = CreateServiceProvider();
            var viewModel = new LibraryViewModel(mediaProvider, sceneProvider, serviceProvider);
            await viewModel.LoadDataAsync();

            // Act 1: Search by partial normalized ID (e.g. without hyphen)
            viewModel.SearchText = "vrkm1023";
            viewModel.ApplyFilterDirect();

            // Assert 1
            Assert.AreEqual(1, viewModel.MediaItems.Count);
            Assert.AreEqual("VRKM-1023", viewModel.MediaItems[0].Id);

            // Act 2: Search by title substring
            viewModel.SearchText = "Beach";
            viewModel.ApplyFilterDirect();

            // Assert 2
            Assert.AreEqual(1, viewModel.MediaItems.Count);
            Assert.AreEqual("DSVR-0572", viewModel.MediaItems[0].Id);

            // Act 3: Clear search
            viewModel.SearchText = string.Empty;
            viewModel.ApplyFilterDirect();

            // Assert 3
            Assert.AreEqual(3, viewModel.MediaItems.Count);
        }

        [TestMethod]
        public async Task TagFilter_TriStateLogic_IncludesAndExcludesCorrectly()
        {
            // Arrange
            var mediaProvider = new StubMediaProvider
            {
                LibraryItemsToReturn =
                [
                    new() { MediaId = "ITEM-1", UserTags = ["VR", "Favorites"] },
                    new() { MediaId = "ITEM-2", UserTags = ["VR"] },
                    new() { MediaId = "ITEM-3", UserTags = ["2D"] }
                ],
                UserTagsToReturn = ["VR", "Favorites", "2D"]
            };
            var sceneProvider = new StubMediaSceneProvider();
            var serviceProvider = CreateServiceProvider();
            var viewModel = new LibraryViewModel(mediaProvider, sceneProvider, serviceProvider);
            await viewModel.LoadDataAsync();

            var vrTag = viewModel.TagFilterItems.First(t => t.Name == "VR");
            var favTag = viewModel.TagFilterItems.First(t => t.Name == "Favorites");

            // 1. Include "VR": should return ITEM-1 and ITEM-2
            vrTag.IsSelected = true;
            viewModel.ApplyFilterDirect();
            Assert.AreEqual(2, viewModel.MediaItems.Count);
            Assert.IsTrue(viewModel.MediaItems.All(i => i.Id == "ITEM-1" || i.Id == "ITEM-2"));

            // 2. Exclude "Favorites" (IsSelected = null): should remove ITEM-1, keeping only ITEM-2
            favTag.IsSelected = null;
            viewModel.ApplyFilterDirect();
            Assert.AreEqual(1, viewModel.MediaItems.Count);
            Assert.AreEqual("ITEM-2", viewModel.MediaItems[0].Id);

            // 3. Reset both (IsSelected = false): should return all 3 items
            vrTag.IsSelected = false;
            favTag.IsSelected = false;
            viewModel.ApplyFilterDirect();
            Assert.AreEqual(3, viewModel.MediaItems.Count);
        }

        [TestMethod]
        public async Task SortOrder_SortsCorrectlyByAllModes()
        {
            // Arrange
            var mediaProvider = new StubMediaProvider
            {
                LibraryItemsToReturn =
                [
                    new() { MediaId = "BBB", Title = "B Title", HighlightCount = 10, ReleaseDate = new DateOnly(2023, 1, 1), CreatedAt = new DateTime(2023, 1, 1) },
                    new() { MediaId = "AAA", Title = "A Title", HighlightCount = 20, ReleaseDate = new DateOnly(2024, 1, 1), CreatedAt = new DateTime(2024, 1, 1) },
                    new() { MediaId = "CCC", Title = "C Title", HighlightCount = 5,  ReleaseDate = new DateOnly(2022, 1, 1), CreatedAt = new DateTime(2022, 1, 1) }
                ]
            };
            var sceneProvider = new StubMediaSceneProvider();
            var serviceProvider = CreateServiceProvider();
            var viewModel = new LibraryViewModel(mediaProvider, sceneProvider, serviceProvider);
            await viewModel.LoadDataAsync();

            // Alphabetical Ascending
            viewModel.SortOrder = LibrarySortOption.AlphabeticalAscending;
            viewModel.ApplyFilterDirect();
            Assert.AreEqual("AAA", viewModel.MediaItems[0].Id);
            Assert.AreEqual("BBB", viewModel.MediaItems[1].Id);
            Assert.AreEqual("CCC", viewModel.MediaItems[2].Id);

            // Alphabetical Descending
            viewModel.SortOrder = LibrarySortOption.AlphabeticalDescending;
            viewModel.ApplyFilterDirect();
            Assert.AreEqual("CCC", viewModel.MediaItems[0].Id);
            Assert.AreEqual("BBB", viewModel.MediaItems[1].Id);
            Assert.AreEqual("AAA", viewModel.MediaItems[2].Id);

            // Highlights Descending
            viewModel.SortOrder = LibrarySortOption.HighlightsDescending;
            viewModel.ApplyFilterDirect();
            Assert.AreEqual("AAA", viewModel.MediaItems[0].Id); // 20
            Assert.AreEqual("BBB", viewModel.MediaItems[1].Id); // 10
            Assert.AreEqual("CCC", viewModel.MediaItems[2].Id); // 5

            // Highlights Ascending
            viewModel.SortOrder = LibrarySortOption.HighlightsAscending;
            viewModel.ApplyFilterDirect();
            Assert.AreEqual("CCC", viewModel.MediaItems[0].Id); // 5
            Assert.AreEqual("BBB", viewModel.MediaItems[1].Id); // 10
            Assert.AreEqual("AAA", viewModel.MediaItems[2].Id); // 20
        }
    }
}
