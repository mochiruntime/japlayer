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
            public List<string> CastToReturn { get; set; } = [];
            public List<string> StudiosToReturn { get; set; } = [];
            public List<string> SeriesToReturn { get; set; } = [];

            public Task<IEnumerable<LibraryItem>> GetLibraryItemsAsync()
                => Task.FromResult<IEnumerable<LibraryItem>>(LibraryItemsToReturn);

            public Task<IEnumerable<string>> GetUserTagsAsync()
                => Task.FromResult<IEnumerable<string>>(UserTagsToReturn);

            public Task<IEnumerable<string>> GetGenresAsync()
                => Task.FromResult<IEnumerable<string>>(GenresToReturn);

            public Task<IEnumerable<string>> GetCastAsync()
                => Task.FromResult<IEnumerable<string>>(CastToReturn);

            public Task<IEnumerable<string>> GetStudiosAsync()
                => Task.FromResult<IEnumerable<string>>(StudiosToReturn);

            public Task<IEnumerable<string>> GetSeriesAsync()
                => Task.FromResult<IEnumerable<string>>(SeriesToReturn);

            public Task<MediaItem> GetMediaItemAsync(string mediaId)
                => Task.FromResult(new MediaItem { MediaId = mediaId });

            public Task AddUserTagAsync(string mediaId, string tagName) => Task.CompletedTask;

            public Task RemoveUserTagAsync(string mediaId, string tagName) => Task.CompletedTask;

            public Task SetRatingAsync(string mediaId, int rating) => Task.CompletedTask;
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

        [TestMethod]
        public async Task CastStudiosSeriesFacets_TriStateLogic_FiltersAndUpdatesActiveChips()
        {
            var mediaProvider = new StubMediaProvider
            {
                LibraryItemsToReturn =
                [
                    new() { MediaId = "M1", Cast = ["Actor A"], Studios = ["Studio X"], Series = ["Series 1"] },
                    new() { MediaId = "M2", Cast = ["Actor A", "Actor B"], Studios = ["Studio Y"], Series = ["Series 1"] },
                    new() { MediaId = "M3", Cast = ["Actor B"], Studios = ["Studio X"], Series = ["Series 2"] }
                ],
                CastToReturn = ["Actor A", "Actor B"],
                StudiosToReturn = ["Studio X", "Studio Y"],
                SeriesToReturn = ["Series 1", "Series 2"]
            };

            var viewModel = new LibraryViewModel(mediaProvider, new StubMediaSceneProvider(), CreateServiceProvider());
            await viewModel.LoadDataAsync();

            // Include Actor A, Exclude Studio Y -> should leave only M1
            viewModel.CastFacet.FilteredItems.First(item => item.Name == "Actor A").IsSelected = true;
            viewModel.StudiosFacet.FilteredItems.First(item => item.Name == "Studio Y").IsSelected = null;
            viewModel.ApplyFilterDirect();

            Assert.AreEqual(1, viewModel.MediaItems.Count);
            Assert.AreEqual("M1", viewModel.MediaItems[0].Id);
            Assert.AreEqual(2, viewModel.ActiveFilterChips.Count);
            Assert.IsTrue(viewModel.HasActiveFilters);

            // Clear all filters
            viewModel.ClearAllFiltersCommand.Execute(null);
            viewModel.ApplyFilterDirect();
            Assert.AreEqual(3, viewModel.MediaItems.Count);
            Assert.IsFalse(viewModel.HasActiveFilters);
        }

        [TestMethod]
        public async Task RatingAndRuntimeRangeFilters_FilterAndResetCorrectly()
        {
            var mediaProvider = new StubMediaProvider
            {
                LibraryItemsToReturn =
                [
                    new() { MediaId = "M1", Rating = 2, RuntimeMinutes = 45 },
                    new() { MediaId = "M2", Rating = 4, RuntimeMinutes = 90 },
                    new() { MediaId = "M3", Rating = 5, RuntimeMinutes = 150 }
                ]
            };

            var viewModel = new LibraryViewModel(mediaProvider, new StubMediaSceneProvider(), CreateServiceProvider());
            await viewModel.LoadDataAsync();

            viewModel.RatingMin = 4;
            viewModel.RatingMax = 5;
            viewModel.RuntimeMin = 60;
            viewModel.RuntimeMax = 120;
            viewModel.ApplyFilterDirect();

            Assert.AreEqual(1, viewModel.MediaItems.Count);
            Assert.AreEqual("M2", viewModel.MediaItems[0].Id);
            Assert.IsTrue(viewModel.IsRatingFilterActive);
            Assert.IsTrue(viewModel.IsRuntimeFilterActive);

            viewModel.ResetRatingFilterCommand.Execute(null);
            viewModel.ResetRuntimeFilterCommand.Execute(null);
            viewModel.ApplyFilterDirect();

            Assert.AreEqual(3, viewModel.MediaItems.Count);
            Assert.IsFalse(viewModel.IsRatingFilterActive);
            Assert.IsFalse(viewModel.IsRuntimeFilterActive);
        }

        [TestMethod]
        public async Task SortFieldAndDirection_SynchronizeAndHideDirectionOnRandom()
        {
            var mediaProvider = new StubMediaProvider
            {
                LibraryItemsToReturn =
                [
                    new() { MediaId = "M1", Rating = 2 },
                    new() { MediaId = "M2", Rating = 5 },
                    new() { MediaId = "M3", Rating = 3 }
                ]
            };

            var viewModel = new LibraryViewModel(mediaProvider, new StubMediaSceneProvider(), CreateServiceProvider());
            await viewModel.LoadDataAsync();

            viewModel.SelectedSortField = viewModel.SortFieldOptions.First(option => option.Field == LibrarySortField.Rating);
            viewModel.IsSortDescending = true;
            viewModel.ApplyFilterDirect();

            Assert.IsTrue(viewModel.IsNotRandomSort);
            Assert.AreEqual(LibrarySortOption.RatingDescending, viewModel.SortOrder);
            Assert.AreEqual("M2", viewModel.MediaItems[0].Id);

            viewModel.ToggleSortDirectionCommand.Execute(null);
            viewModel.ApplyFilterDirect();
            Assert.AreEqual(LibrarySortOption.RatingAscending, viewModel.SortOrder);
            Assert.AreEqual("M1", viewModel.MediaItems[0].Id);

            viewModel.ShuffleCommand.Execute(null);
            Assert.AreEqual(LibrarySortOption.Random, viewModel.SortOrder);
            Assert.IsFalse(viewModel.IsNotRandomSort);
        }
    }
}
