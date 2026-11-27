using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Messaging;
using Japlayer.Contracts;
using Japlayer.Data.Contracts;
using Japlayer.Data.Entities;
using Japlayer.Data.Models;
using Japlayer.Messages;
using Japlayer.Models;
using Japlayer.Services;
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

        [TestMethod]
        public async Task FacetFilterGroup_DefersReorderingWhileFlyoutIsOpen()
        {
            var mediaProvider = new StubMediaProvider
            {
                LibraryItemsToReturn =
                [
                    new() { MediaId = "M1", UserTags = ["Alpha", "Zeta"] }
                ],
                UserTagsToReturn = ["Alpha", "Zeta"]
            };

            var viewModel = new LibraryViewModel(mediaProvider, new StubMediaSceneProvider(), CreateServiceProvider());
            await viewModel.LoadDataAsync();

            Assert.AreEqual("Alpha", viewModel.TagsFacet.FilteredItems[0].Name);
            Assert.AreEqual("Zeta", viewModel.TagsFacet.FilteredItems[1].Name);

            // While flyout is open, selecting "Zeta" must not move it above "Alpha"
            viewModel.TagsFacet.BeginInteraction();
            viewModel.TagsFacet.FilteredItems[1].IsSelected = true;
            viewModel.ApplyFilterDirect();

            Assert.AreEqual("Alpha", viewModel.TagsFacet.FilteredItems[0].Name);
            Assert.AreEqual("Zeta", viewModel.TagsFacet.FilteredItems[1].Name);

            // Once flyout closes, "Zeta" moves to the top
            viewModel.TagsFacet.EndInteraction();

            Assert.AreEqual("Zeta", viewModel.TagsFacet.FilteredItems[0].Name);
            Assert.AreEqual("Alpha", viewModel.TagsFacet.FilteredItems[1].Name);
        }

        [TestMethod]
        public async Task MutationMessages_SynchronizeRatingTagsAndFacetCounts()
        {
            var mediaProvider = new StubMediaProvider
            {
                LibraryItemsToReturn =
                [
                    new() { MediaId = "M1", Title = "First", Rating = 2, UserTags = ["Existing"] },
                    new() { MediaId = "M2", Title = "Second", Rating = 5, UserTags = [] }
                ],
                UserTagsToReturn = ["Existing"]
            };

            var messenger = new WeakReferenceMessenger();
            var viewModel = new LibraryViewModel(mediaProvider, new StubMediaSceneProvider(), CreateServiceProvider(), messenger: messenger);
            await viewModel.LoadDataAsync();

            // Simulate navigating away to MediaItemPage
            await viewModel.SetPageActiveAsync(false);

            // Rating filter active -> rating update should mark pending refresh and apply on return
            viewModel.RatingMin = 4;
            viewModel.RatingMax = 5;
            viewModel.ApplyFilterDirect();
            Assert.AreEqual(1, viewModel.MediaItems.Count);

            messenger.Send(new MediaRatingChangedMessage("M1", 5));
            messenger.Send(new MediaUserTagAddedMessage("M1", "BrandNewTag"));
            messenger.Send(new MediaHighlightCountChangedMessage("M1", 7));

            // New tag should immediately appear in TagsFacet with count 1
            var newTagFilter = viewModel.TagsFacet.FilteredItems.FirstOrDefault(item => item.Name == "BrandNewTag");
            Assert.IsNotNull(newTagFilter);
            Assert.AreEqual(1, newTagFilter.ItemCount);

            // Returning to LibraryPage applies pending filter refresh so M1 now matches 4..5 rating filter
            await viewModel.SetPageActiveAsync(true);
            Assert.AreEqual(2, viewModel.MediaItems.Count);
            var updatedM1 = viewModel.MediaItems.First(item => item.Id == "M1");
            Assert.AreEqual(5, updatedM1.Rating);
            Assert.AreEqual(7, updatedM1.HighlightCount);
            CollectionAssert.Contains(updatedM1.LibraryItem.UserTags.ToList(), "BrandNewTag");

            // Removing the tag decrements its count to 0
            messenger.Send(new MediaUserTagRemovedMessage("M1", "BrandNewTag"));
            Assert.AreEqual(0, newTagFilter.ItemCount);
        }

        private sealed class FakeDatabaseChangeMonitor : IDatabaseChangeMonitor
        {
            public event EventHandler? DatabaseChangedExternally;
            public void Start() { }
            public IDisposable BeginInternalWrite() => new NoOpDisposable();
            public void RaiseExternalChange() => DatabaseChangedExternally?.Invoke(this, EventArgs.Empty);
            public void Dispose() { }

            private sealed class NoOpDisposable : IDisposable
            {
                public void Dispose() { }
            }
        }

        [TestMethod]
        public async Task ExternalReload_PreservesActiveFiltersSortAndExistingItemInstances()
        {
            var mediaProvider = new StubMediaProvider
            {
                LibraryItemsToReturn =
                [
                    new() { MediaId = "M1", Title = "Alpha", Rating = 4, RuntimeMinutes = 60, UserTags = ["VR"] },
                    new() { MediaId = "M2", Title = "Beta", Rating = 3, RuntimeMinutes = 75, UserTags = ["2D"] }
                ],
                UserTagsToReturn = ["2D", "VR"]
            };

            var monitor = new FakeDatabaseChangeMonitor();
            var viewModel = new LibraryViewModel(mediaProvider, new StubMediaSceneProvider(), CreateServiceProvider(), monitor, new WeakReferenceMessenger());
            await viewModel.LoadDataAsync();

            var originalM1 = viewModel.MediaItems.First(item => item.Id == "M1");

            // Apply Include + Exclude facet filters, constrained runtime, and custom sort
            viewModel.TagsFacet.FilteredItems.First(item => item.Name == "VR").IsSelected = true;
            viewModel.TagsFacet.FilteredItems.First(item => item.Name == "2D").IsSelected = null;
            viewModel.RuntimeMin = 30;
            viewModel.RuntimeMax = 120;
            viewModel.SelectedSortField = viewModel.SortFieldOptions.First(option => option.Field == LibrarySortField.Rating);
            viewModel.ApplyFilterDirect();
            Assert.AreEqual(1, viewModel.MediaItems.Count);

            // External DB adds M3 (VR, 90m), removes M2, and updates M1 rating while page is inactive
            await viewModel.SetPageActiveAsync(false);
            mediaProvider.LibraryItemsToReturn =
            [
                new() { MediaId = "M1", Title = "Alpha Updated", Rating = 5, RuntimeMinutes = 60, UserTags = ["VR"] },
                new() { MediaId = "M3", Title = "Gamma", Rating = 4, RuntimeMinutes = 90, UserTags = ["VR"] },
                new() { MediaId = "M4", Title = "Long Unmatched", Rating = 4, RuntimeMinutes = 240, UserTags = ["2D", "VR"] }
            ];
            monitor.RaiseExternalChange();

            // Returning triggers deferred reload
            await viewModel.SetPageActiveAsync(true);

            // M2 removed, M4 excluded by -2D and runtime > 120, M1 and M3 kept
            Assert.AreEqual(2, viewModel.MediaItems.Count);
            Assert.AreEqual("M1", viewModel.MediaItems[0].Id);
            Assert.AreEqual("M3", viewModel.MediaItems[1].Id);
            Assert.AreSame(originalM1, viewModel.MediaItems[0]);
            Assert.AreEqual(5, originalM1.Rating);
            Assert.AreEqual(LibrarySortField.Rating, viewModel.SelectedSortField.Field);
            Assert.IsTrue(viewModel.TagsFacet.RequiredValues.Contains("VR"));
            Assert.IsTrue(viewModel.TagsFacet.ExcludedValues.Contains("2D"));
            Assert.AreEqual(240, viewModel.RuntimeMaxBound);
            Assert.AreEqual(120, viewModel.RuntimeMax);
        }

        private sealed class BlockingMediaProvider : IMediaProvider
        {
            public TaskCompletionSource? FirstCallStarted { get; set; }
            public TaskCompletionSource? ReleaseFirstCall { get; set; }
            public int CallCount { get; private set; }
            public List<LibraryItem> SecondPassItems { get; set; } = [];

            public async Task<IEnumerable<LibraryItem>> GetLibraryItemsAsync()
            {
                CallCount++;
                if (CallCount == 1 && FirstCallStarted != null && ReleaseFirstCall != null)
                {
                    FirstCallStarted.TrySetResult();
                    await ReleaseFirstCall.Task;
                    return [new LibraryItem { MediaId = "M1", Title = "Pass 1" }];
                }

                return SecondPassItems;
            }

            public Task<IEnumerable<string>> GetUserTagsAsync() => Task.FromResult<IEnumerable<string>>([]);
            public Task<IEnumerable<string>> GetGenresAsync() => Task.FromResult<IEnumerable<string>>([]);
            public Task<IEnumerable<string>> GetCastAsync() => Task.FromResult<IEnumerable<string>>([]);
            public Task<IEnumerable<string>> GetStudiosAsync() => Task.FromResult<IEnumerable<string>>([]);
            public Task<IEnumerable<string>> GetSeriesAsync() => Task.FromResult<IEnumerable<string>>([]);
            public Task<MediaItem> GetMediaItemAsync(string mediaId) => Task.FromResult(new MediaItem { MediaId = mediaId });
            public Task AddUserTagAsync(string mediaId, string tagName) => Task.CompletedTask;
            public Task RemoveUserTagAsync(string mediaId, string tagName) => Task.CompletedTask;
            public Task SetRatingAsync(string mediaId, int rating) => Task.CompletedTask;
        }

        [TestMethod]
        public async Task ReloadLibraryAsync_CoalescesConcurrentTriggers_AndSortOrderAppliedFiresOnlyOnSort()
        {
            var provider = new BlockingMediaProvider
            {
                FirstCallStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
                ReleaseFirstCall = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
                SecondPassItems =
                [
                    new LibraryItem { MediaId = "M1", Title = "Pass 2" },
                    new LibraryItem { MediaId = "M2", Title = "Added During Reload" }
                ]
            };

            var viewModel = new LibraryViewModel(provider, new StubMediaSceneProvider(), CreateServiceProvider(), messenger: new WeakReferenceMessenger());
            var sortAppliedCount = 0;
            viewModel.SortOrderApplied += (_, _) => sortAppliedCount++;

            var firstReload = viewModel.ReloadLibraryAsync();
            await provider.FirstCallStarted.Task;

            // Overlapping reload calls coalesce into a single follow-up pass
            var secondReload = viewModel.ReloadLibraryAsync();
            var thirdReload = viewModel.ReloadLibraryAsync();
            Assert.IsTrue(secondReload.IsCompleted);
            Assert.IsTrue(thirdReload.IsCompleted);

            provider.ReleaseFirstCall.TrySetResult();
            await firstReload;

            Assert.AreEqual(2, provider.CallCount);
            Assert.AreEqual(2, viewModel.MediaItems.Count);
            Assert.AreEqual(1, sortAppliedCount);

            // Filtering only must not trigger SortOrderApplied
            viewModel.SearchText = "Added";
            viewModel.ApplyFilterDirect();
            Assert.AreEqual(1, viewModel.MediaItems.Count);
            Assert.AreEqual(1, sortAppliedCount);

            // Changing sort field triggers SortOrderApplied
            viewModel.SelectedSortField = viewModel.SortFieldOptions.First(option => option.Field == LibrarySortField.Title);
            viewModel.ApplyFilterDirect();
            Assert.AreEqual(2, sortAppliedCount);
        }

        private sealed class StubLibraryPresetProvider : ILibraryPresetProvider
        {
            private int _nextId = 1;
            public List<LibraryPresetModel> Presets { get; } = [];

            public Task<IReadOnlyList<LibraryPresetModel>> GetPresetsAsync()
                => Task.FromResult<IReadOnlyList<LibraryPresetModel>>([.. Presets.Select(Clone)]);

            public Task<LibraryPresetModel> CreatePresetAsync(LibraryPresetModel preset)
            {
                preset.Id = _nextId++;
                Presets.Add(Clone(preset));
                return Task.FromResult(Clone(preset));
            }

            public Task UpdatePresetAsync(LibraryPresetModel preset)
            {
                var index = Presets.FindIndex(entry => entry.Id == preset.Id);
                if (index >= 0)
                {
                    Presets[index] = Clone(preset);
                }

                return Task.CompletedTask;
            }

            public Task DeletePresetAsync(int presetId)
            {
                Presets.RemoveAll(entry => entry.Id == presetId);
                return Task.CompletedTask;
            }

            private static LibraryPresetModel Clone(LibraryPresetModel source) => new()
            {
                Id = source.Id,
                Name = source.Name,
                IsCustomName = source.IsCustomName,
                SearchText = source.SearchText,
                SortField = source.SortField,
                IsSortDescending = source.IsSortDescending,
                RatingMin = source.RatingMin,
                RatingMax = source.RatingMax,
                RuntimeMin = source.RuntimeMin,
                RuntimeMax = source.RuntimeMax,
                CreatedAt = source.CreatedAt,
                Tags = new LibraryPresetFacetSelection { Included = [.. source.Tags.Included], Excluded = [.. source.Tags.Excluded] },
                Genres = new LibraryPresetFacetSelection { Included = [.. source.Genres.Included], Excluded = [.. source.Genres.Excluded] },
                Cast = new LibraryPresetFacetSelection { Included = [.. source.Cast.Included], Excluded = [.. source.Cast.Excluded] },
                Studios = new LibraryPresetFacetSelection { Included = [.. source.Studios.Included], Excluded = [.. source.Studios.Excluded] },
                Series = new LibraryPresetFacetSelection { Included = [.. source.Series.Included], Excluded = [.. source.Series.Excluded] }
            };
        }

        [TestMethod]
        public async Task Presets_OneClickSaveApplyRenameUpdateDelete_AndOrdinalIgnoreCaseConsistency()
        {
            var mediaProvider = new StubMediaProvider
            {
                LibraryItemsToReturn =
                [
                    new() { MediaId = "M1", Title = "Alpha", Rating = 5, RuntimeMinutes = 60, UserTags = ["VR"], Genres = ["Sci-Fi"] },
                    new() { MediaId = "M2", Title = "Beta", Rating = 3, RuntimeMinutes = 90, UserTags = ["2D"], Genres = ["Drama"] }
                ],
                UserTagsToReturn = ["2D", "VR"],
                GenresToReturn = ["Drama", "Sci-Fi"]
            };

            var presetProvider = new StubLibraryPresetProvider();
            var monitor = new FakeDatabaseChangeMonitor();
            var viewModel = new LibraryViewModel(
                mediaProvider,
                new StubMediaSceneProvider(),
                CreateServiceProvider(),
                monitor,
                new WeakReferenceMessenger(),
                presetProvider);

            await viewModel.LoadDataAsync();

            Assert.IsFalse(viewModel.ShowPresetsBar);
            Assert.IsFalse(viewModel.ShowFilterOrPresetsBar);

            // Apply Include + Exclude + Rating + Search + Sort
            viewModel.SearchText = "Alpha";
            viewModel.TagsFacet.FilteredItems.First(item => item.Name == "VR").IsSelected = true;
            viewModel.TagsFacet.FilteredItems.First(item => item.Name == "2D").IsSelected = null;
            viewModel.RatingMin = 4;
            viewModel.SelectedSortField = viewModel.SortFieldOptions.First(option => option.Field == LibrarySortField.Rating);
            viewModel.ApplyFilterDirect();

            Assert.AreEqual(1, viewModel.MediaItems.Count);
            Assert.IsTrue(viewModel.HasActiveFilters);
            Assert.IsTrue(viewModel.CanSaveCurrentPreset);
            Assert.AreEqual("Save preset", viewModel.SavePresetButtonText);

            // One-click save
            await viewModel.SaveCurrentAsPresetCommand.ExecuteAsync(null);
            Assert.AreEqual(1, viewModel.Presets.Count);
            Assert.IsTrue(viewModel.IsCurrentFilterSavedAsPreset);
            Assert.IsFalse(viewModel.CanSaveCurrentPreset);
            Assert.AreEqual("Saved", viewModel.SavePresetButtonText);
            Assert.IsFalse(viewModel.ShowPresetsBar);

            // Clear filters reveals Presets bar
            viewModel.ClearAllFiltersCommand.Execute(null);
            Assert.IsFalse(viewModel.HasActiveFilters);
            Assert.IsTrue(viewModel.ShowPresetsBar);
            Assert.IsTrue(viewModel.ShowFilterOrPresetsBar);
            Assert.AreEqual(2, viewModel.MediaItems.Count);

            // Apply preset restores everything in a single pass
            var savedPreset = viewModel.Presets[0];
            viewModel.ApplyPresetCommand.Execute(savedPreset);
            Assert.AreEqual(1, viewModel.MediaItems.Count);
            Assert.AreEqual("M1", viewModel.MediaItems[0].Id);
            Assert.AreEqual("Alpha", viewModel.SearchText);
            Assert.AreEqual(4, viewModel.RatingMin);
            Assert.AreEqual(LibrarySortField.Rating, viewModel.SelectedSortField.Field);
            Assert.IsTrue(viewModel.TagsFacet.RequiredValues.Contains("VR"));
            Assert.IsTrue(viewModel.TagsFacet.ExcludedValues.Contains("2D"));
            Assert.IsTrue(savedPreset.IsActive);

            // Modifying a filter resets saved state and allows saving collision "(2)"
            viewModel.SearchText = string.Empty;
            viewModel.ApplyFilterDirect();
            Assert.IsFalse(viewModel.IsCurrentFilterSavedAsPreset);
            await viewModel.SaveCurrentAsPresetCommand.ExecuteAsync(null);
            Assert.AreEqual(2, viewModel.Presets.Count);

            // Rename preset + duplicate validation
            Assert.IsFalse(viewModel.TryValidatePresetName("   ", savedPreset.Id, out _));
            Assert.IsTrue(await viewModel.RenamePresetAsync(savedPreset, "Favorites"));
            Assert.AreEqual("Favorites", savedPreset.DisplayName);
            Assert.IsTrue(savedPreset.Model.IsCustomName);
            Assert.IsFalse(viewModel.TryValidatePresetName("favorites", viewModel.Presets[1].Id, out _));

            // External reload updates auto-named preset display name if facet value renamed, keeps custom name
            presetProvider.Presets[1].Tags = new LibraryPresetFacetSelection { Included = ["Virtual Reality"] };
            await viewModel.ReloadLibraryAsync();
            Assert.AreEqual("Favorites", viewModel.Presets[0].DisplayName);
            StringAssert.Contains(viewModel.Presets[1].DisplayName, "+Virtual Reality");

            // Delete presets
            await viewModel.DeletePresetCommand.ExecuteAsync(viewModel.Presets[1]);
            await viewModel.DeletePresetCommand.ExecuteAsync(viewModel.Presets[0]);
            Assert.AreEqual(0, viewModel.Presets.Count);
            Assert.IsFalse(viewModel.ShowPresetsBar);
        }

        [TestMethod]
        public async Task FacetFilter_MatchesCaseInsensitively()
        {
            var mediaProvider = new StubMediaProvider
            {
                LibraryItemsToReturn =
                [
                    new() { MediaId = "M1", Genres = ["Sci-Fi"] },
                    new() { MediaId = "M2", Genres = ["Drama"] }
                ],
                GenresToReturn = ["sci-fi", "drama"]
            };

            var viewModel = new LibraryViewModel(mediaProvider, new StubMediaSceneProvider(), CreateServiceProvider(), messenger: new WeakReferenceMessenger());
            await viewModel.LoadDataAsync();

            var sciFiFilter = viewModel.GenresFacet.FilteredItems.First(item => item.Name == "sci-fi");
            Assert.AreEqual(1, sciFiFilter.ItemCount);
            sciFiFilter.IsSelected = true;
            viewModel.ApplyFilterDirect();

            Assert.AreEqual(1, viewModel.MediaItems.Count);
            Assert.AreEqual("M1", viewModel.MediaItems[0].Id);
        }

        [TestMethod]
        public async Task RandomSort_PreservesOrderAndAvoidsReset_WhenReloadOrPendingFilterRunsOnReturn()
        {
            var items = Enumerable.Range(1, 20)
                .Select(index => new LibraryItem { MediaId = $"M{index:D2}", Title = $"Title {index:D2}", Rating = 4 })
                .ToList();

            var mediaProvider = new StubMediaProvider { LibraryItemsToReturn = items };
            var monitor = new FakeDatabaseChangeMonitor();
            var messenger = new WeakReferenceMessenger();
            var viewModel = new LibraryViewModel(mediaProvider, new StubMediaSceneProvider(), CreateServiceProvider(), monitor, messenger);
            await viewModel.LoadDataAsync();

            viewModel.ShuffleCommand.Execute(null);
            viewModel.RatingMin = 1;
            viewModel.RatingMax = 5;
            viewModel.ApplyFilterDirect();

            var initialOrder = viewModel.MediaItems.Select(item => item.Id).ToList();
            var resetCount = 0;
            viewModel.MediaItems.CollectionChanged += (_, eventArgs) =>
            {
                if (eventArgs.Action == NotifyCollectionChangedAction.Reset)
                {
                    resetCount++;
                }
            };

            // Simulate navigating away, editing an item within active rating filter + external DB signal, then returning
            await viewModel.SetPageActiveAsync(false);
            messenger.Send(new MediaRatingChangedMessage("M05", 5));
            monitor.RaiseExternalChange();
            await viewModel.SetPageActiveAsync(true);

            var currentOrder = viewModel.MediaItems.Select(item => item.Id).ToList();
            CollectionAssert.AreEqual(initialOrder, currentOrder, "Random order should remain stable across pending filter refresh and reload on Back navigation.");
            Assert.AreEqual(0, resetCount, "Unchanged membership under Random sort should not emit a Reset notification.");
        }

        [TestMethod]
        public async Task ExplicitShuffleCommand_ProducesNewRandomOrder()
        {
            var items = Enumerable.Range(1, 20)
                .Select(index => new LibraryItem { MediaId = $"M{index:D2}", Title = $"Title {index:D2}" })
                .ToList();

            var viewModel = new LibraryViewModel(
                new StubMediaProvider { LibraryItemsToReturn = items },
                new StubMediaSceneProvider(),
                CreateServiceProvider(),
                messenger: new WeakReferenceMessenger());
            await viewModel.LoadDataAsync();

            viewModel.ShuffleCommand.Execute(null);
            viewModel.ApplyFilterDirect();
            var firstOrder = viewModel.MediaItems.Select(item => item.Id).ToList();

            var sortAppliedCount = 0;
            viewModel.SortOrderApplied += (_, _) => sortAppliedCount++;

            viewModel.ShuffleCommand.Execute(null);
            viewModel.ApplyFilterDirect();
            var secondOrder = viewModel.MediaItems.Select(item => item.Id).ToList();

            Assert.IsFalse(firstOrder.SequenceEqual(secondOrder), "Invoking ShuffleCommand again should generate a new random order.");
            Assert.IsTrue(sortAppliedCount >= 1);
        }

        [TestMethod]
        public async Task NoOpMessagesWhileInactive_DoNotTriggerResetOnReturn()
        {
            var items = Enumerable.Range(1, 20)
                .Select(index => new LibraryItem { MediaId = $"M{index:D2}", Title = $"Title {index:D2}", Rating = 4, HighlightCount = 2 })
                .ToList();

            var messenger = new WeakReferenceMessenger();
            var viewModel = new LibraryViewModel(
                new StubMediaProvider { LibraryItemsToReturn = items },
                new StubMediaSceneProvider(),
                CreateServiceProvider(),
                messenger: messenger);
            await viewModel.LoadDataAsync();

            viewModel.ShuffleCommand.Execute(null);
            viewModel.RatingMin = 1;
            viewModel.ApplyFilterDirect();

            var initialOrder = viewModel.MediaItems.Select(item => item.Id).ToList();
            var resetCount = 0;
            viewModel.MediaItems.CollectionChanged += (_, eventArgs) =>
            {
                if (eventArgs.Action == NotifyCollectionChangedAction.Reset)
                {
                    resetCount++;
                }
            };

            await viewModel.SetPageActiveAsync(false);
            messenger.Send(new MediaRatingChangedMessage("M01", 4));
            messenger.Send(new MediaHighlightCountChangedMessage("M01", 2));
            await viewModel.SetPageActiveAsync(true);

            CollectionAssert.AreEqual(initialOrder, viewModel.MediaItems.Select(item => item.Id).ToList());
            Assert.AreEqual(0, resetCount, "No-op rating and highlight messages should not trigger a GridView Reset on return.");
        }
    }
}
