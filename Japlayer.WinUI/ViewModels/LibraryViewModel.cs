#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Japlayer.Data.Contracts;
using Japlayer.Data.Models;
using Japlayer.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace Japlayer.ViewModels
{
    public partial class LibraryViewModel : ObservableObject
    {
        private readonly IMediaProvider _mediaProvider;
        private readonly IMediaSceneProvider _sceneProvider;
        private readonly IServiceProvider _serviceProvider;
        private List<LibraryItemViewModel> _allMediaItems = [];
        private DispatcherTimer? _filterDebounceTimer;
        // Flag to track if the sort order changed. Used to choose between
        // fast collection replacement (for sorting) and smooth collection reconciliation (for filtering).
        private bool _isSortOrderChanging = true;
        private bool _isSyncingSort;

        public LibraryViewModel(IMediaProvider mediaProvider, IMediaSceneProvider sceneProvider, IServiceProvider serviceProvider)
        {
            _mediaProvider = mediaProvider;
            _sceneProvider = sceneProvider;
            _serviceProvider = serviceProvider;

            TagsFacet = new FacetFilterGroup("Tags", "Tag", QueueApplyFilter);
            GenresFacet = new FacetFilterGroup("Genres", "Genre", QueueApplyFilter);
            CastFacet = new FacetFilterGroup("Cast", "Cast", QueueApplyFilter);
            StudiosFacet = new FacetFilterGroup("Studios", "Studio", QueueApplyFilter);
            SeriesFacet = new FacetFilterGroup("Series", "Series", QueueApplyFilter);

            AllFacets = [TagsFacet, GenresFacet, CastFacet, StudiosFacet, SeriesFacet];
            SelectedSortField = SortFieldOptions.First(option => option.Field == LibrarySortField.AddedDate);
        }

        public FacetFilterGroup TagsFacet { get; }
        public FacetFilterGroup GenresFacet { get; }
        public FacetFilterGroup CastFacet { get; }
        public FacetFilterGroup StudiosFacet { get; }
        public FacetFilterGroup SeriesFacet { get; }
        public IReadOnlyList<FacetFilterGroup> AllFacets { get; }

        // Compatibility accessors for existing tests and bindings
        public ObservableCollection<FilterItem> TagFilterItems => TagsFacet.FilteredItems;
        public ObservableCollection<FilterItem> GenreFilterItems => GenresFacet.FilteredItems;

        public string TagSearchText
        {
            get => TagsFacet.SearchText;
            set => TagsFacet.SearchText = value;
        }

        public string GenreSearchText
        {
            get => GenresFacet.SearchText;
            set => GenresFacet.SearchText = value;
        }

        [ObservableProperty]
        public partial ObservableCollection<LibraryItemViewModel> MediaItems { get; set; } = [];

        public ObservableCollection<ActiveFilterChip> ActiveFilterChips { get; } = [];

        public IReadOnlyList<LibrarySortOption> SortOptions { get; } = LibrarySortOption.All;
        public IReadOnlyList<LibrarySortFieldOption> SortFieldOptions { get; } = LibrarySortOption.FieldOptions;

        public Task<IEnumerable<MediaScene>> GetMediaScenesAsync(string mediaId) => _sceneProvider.GetMediaScenesAsync(mediaId);

        [ObservableProperty]
        public partial bool IsDataLoaded { get; set; }

        [ObservableProperty]
        public partial string SearchText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial LibrarySortOption SortOrder { get; set; } = LibrarySortOption.AddedDateDescending;

        [ObservableProperty]
        public partial LibrarySortFieldOption SelectedSortField { get; set; }

        [ObservableProperty]
        public partial bool IsSortDescending { get; set; } = true;

        public bool IsNotRandomSort => SelectedSortField.Field != LibrarySortField.Random;
        public string SortDirectionGlyph => IsSortDescending ? "\uE74B" : "\uE74A";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRatingFilterActive), nameof(RatingButtonLabel), nameof(RatingRangeDisplay))]
        public partial double RatingMin { get; set; } = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRatingFilterActive), nameof(RatingButtonLabel), nameof(RatingRangeDisplay))]
        public partial double RatingMax { get; set; } = 5;

        public bool IsRatingFilterActive => RatingMin > 0 || RatingMax < 5;
        public string RatingButtonLabel => IsRatingFilterActive ? $"{(int)RatingMin}–{(int)RatingMax}" : "Rating";
        public string RatingRangeDisplay => $"★ {(int)RatingMin} – {(int)RatingMax}";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRuntimeFilterActive), nameof(RuntimeButtonLabel), nameof(RuntimeRangeDisplay))]
        public partial double RuntimeMaxBound { get; set; } = 180;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRuntimeFilterActive), nameof(RuntimeButtonLabel), nameof(RuntimeRangeDisplay))]
        public partial double RuntimeMin { get; set; } = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRuntimeFilterActive), nameof(RuntimeButtonLabel), nameof(RuntimeRangeDisplay))]
        public partial double RuntimeMax { get; set; } = 180;

        public bool IsRuntimeFilterActive => RuntimeMin > 0 || RuntimeMax < RuntimeMaxBound;
        public string RuntimeButtonLabel => IsRuntimeFilterActive ? $"{(int)RuntimeMin}–{(int)RuntimeMax}m" : "Runtime";
        public string RuntimeRangeDisplay => $"{(int)RuntimeMin}m – {(int)RuntimeMax}m";

        public string ItemCountText => MediaItems.Count == _allMediaItems.Count
            ? $"{_allMediaItems.Count} items"
            : $"{MediaItems.Count} of {_allMediaItems.Count}";

        public bool HasActiveFilters => ActiveFilterChips.Count > 0;
        public bool IsEmptyResult => IsDataLoaded && MediaItems.Count == 0;

        partial void OnSearchTextChanged(string value) => QueueApplyFilter();
        partial void OnRatingMinChanged(double value) => QueueApplyFilter();
        partial void OnRatingMaxChanged(double value) => QueueApplyFilter();
        partial void OnRuntimeMinChanged(double value) => QueueApplyFilter();
        partial void OnRuntimeMaxChanged(double value) => QueueApplyFilter();

        partial void OnSortOrderChanged(LibrarySortOption value)
        {
            if (!_isSyncingSort)
            {
                _isSyncingSort = true;
                (var field, var descending) = DecomposeSortOption(value);
                SelectedSortField = SortFieldOptions.First(option => option.Field == field);
                IsSortDescending = descending;
                _isSyncingSort = false;
            }

            OnPropertyChanged(nameof(IsNotRandomSort));
            OnPropertyChanged(nameof(SortDirectionGlyph));
            _isSortOrderChanging = true;
            QueueApplyFilter();
        }

        partial void OnSelectedSortFieldChanged(LibrarySortFieldOption value)
        {
            OnPropertyChanged(nameof(IsNotRandomSort));
            if (_isSyncingSort)
            {
                return;
            }

            _isSyncingSort = true;
            SortOrder = ComposeSortOption(value.Field, IsSortDescending);
            _isSyncingSort = false;
            _isSortOrderChanging = true;
            QueueApplyFilter();
        }

        partial void OnIsSortDescendingChanged(bool value)
        {
            OnPropertyChanged(nameof(SortDirectionGlyph));
            if (_isSyncingSort)
            {
                return;
            }

            _isSyncingSort = true;
            SortOrder = ComposeSortOption(SelectedSortField.Field, value);
            _isSyncingSort = false;
            _isSortOrderChanging = true;
            QueueApplyFilter();
        }

        [RelayCommand]
        private void ToggleSortDirection() => IsSortDescending = !IsSortDescending;

        [RelayCommand]
        private void SelectSortField(LibrarySortFieldOption option) => SelectedSortField = option;

        [RelayCommand]
        private void Shuffle()
        {
            _isSortOrderChanging = true;
            if (SelectedSortField.Field != LibrarySortField.Random)
            {
                SelectedSortField = SortFieldOptions.First(option => option.Field == LibrarySortField.Random);
            }
            else
            {
                QueueApplyFilter();
            }
        }

        [RelayCommand]
        private void ResetRatingFilter()
        {
            RatingMin = 0;
            RatingMax = 5;
        }

        [RelayCommand]
        private void ResetRuntimeFilter()
        {
            RuntimeMin = 0;
            RuntimeMax = RuntimeMaxBound;
        }

        [RelayCommand]
        private void ClearAllFilters()
        {
            SearchText = string.Empty;
            foreach (var facet in AllFacets)
            {
                facet.ResetSelectionsSilently();
            }

            RatingMin = 0;
            RatingMax = 5;
            RuntimeMin = 0;
            RuntimeMax = RuntimeMaxBound;
            QueueApplyFilter();
        }

        public async Task LoadDataAsync()
        {
            var itemsTask = _mediaProvider.GetLibraryItemsAsync();
            var tagsTask = _mediaProvider.GetUserTagsAsync();
            var genresTask = _mediaProvider.GetGenresAsync();
            var castTask = _mediaProvider.GetCastAsync();
            var studiosTask = _mediaProvider.GetStudiosAsync();
            var seriesTask = _mediaProvider.GetSeriesAsync();

            await Task.WhenAll(itemsTask, tagsTask, genresTask, castTask, studiosTask, seriesTask);

            var items = await itemsTask;
            _allMediaItems = [.. items.Select(item => ActivatorUtilities.CreateInstance<LibraryItemViewModel>(_serviceProvider, item))];

            var maxRuntime = _allMediaItems
                .Where(item => item.RuntimeMinutes.HasValue)
                .Select(item => item.RuntimeMinutes!.Value)
                .DefaultIfEmpty(180)
                .Max();
            var roundedMaxRuntime = Math.Max(180, ((maxRuntime + 14) / 15) * 15);
            RuntimeMaxBound = roundedMaxRuntime;
            RuntimeMax = roundedMaxRuntime;

            TagsFacet.Populate(await tagsTask, name => _allMediaItems.Count(item => item.LibraryItem.UserTags.Contains(name)));
            GenresFacet.Populate(await genresTask, name => _allMediaItems.Count(item => item.LibraryItem.Genres.Contains(name)));
            CastFacet.Populate(await castTask, name => _allMediaItems.Count(item => item.LibraryItem.Cast.Contains(name)));
            StudiosFacet.Populate(await studiosTask, name => _allMediaItems.Count(item => item.LibraryItem.Studios.Contains(name)));
            SeriesFacet.Populate(await seriesTask, name => _allMediaItems.Count(item => item.LibraryItem.Series.Contains(name)));

            ApplyFilter();
            IsDataLoaded = true;
            OnPropertyChanged(nameof(ItemCountText));
            OnPropertyChanged(nameof(IsEmptyResult));
        }

        public void ApplyFilterDirect() => ApplyFilter();

        private void QueueApplyFilter()
        {
            if (Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread() == null)
            {
                ApplyFilter();
                return;
            }

            try
            {
                if (_filterDebounceTimer == null)
                {
                    _filterDebounceTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(150)
                    };
                    _filterDebounceTimer.Tick += (sender, e) =>
                    {
                        _filterDebounceTimer.Stop();
                        ApplyFilter();
                    };
                }

                _filterDebounceTimer.Stop();
                _filterDebounceTimer.Start();
            }
            catch
            {
                ApplyFilter();
            }
        }

        private void ApplyFilter()
        {
            var filteredItems = _allMediaItems.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchTerms = SearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var term in searchTerms)
                {
                    var normalizedTerm = NormalizeForSearch(term);
                    filteredItems = filteredItems.Where(item =>
                        NormalizeForSearch(item.Id).Contains(normalizedTerm) ||
                        (item.OriginalTitle != null && item.OriginalTitle.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        item.LibraryItem.UserTags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        item.LibraryItem.Genres.Any(genre => genre.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        item.LibraryItem.Cast.Any(person => person.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        item.LibraryItem.Studios.Any(studio => studio.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        item.LibraryItem.Series.Any(series => series.Contains(term, StringComparison.OrdinalIgnoreCase))
                    );
                }
            }

            filteredItems = ApplyFacetFilter(filteredItems, TagsFacet, item => item.LibraryItem.UserTags);
            filteredItems = ApplyFacetFilter(filteredItems, GenresFacet, item => item.LibraryItem.Genres);
            filteredItems = ApplyFacetFilter(filteredItems, CastFacet, item => item.LibraryItem.Cast);
            filteredItems = ApplyFacetFilter(filteredItems, StudiosFacet, item => item.LibraryItem.Studios);
            filteredItems = ApplyFacetFilter(filteredItems, SeriesFacet, item => item.LibraryItem.Series);

            if (IsRatingFilterActive)
            {
                var minRating = (int)RatingMin;
                var maxRating = (int)RatingMax;
                filteredItems = filteredItems.Where(item => item.Rating >= minRating && item.Rating <= maxRating);
            }

            if (IsRuntimeFilterActive)
            {
                var minRuntime = (int)RuntimeMin;
                var maxRuntime = (int)RuntimeMax;
                filteredItems = filteredItems.Where(item =>
                    item.RuntimeMinutes.HasValue &&
                    item.RuntimeMinutes.Value >= minRuntime &&
                    item.RuntimeMinutes.Value <= maxRuntime);
            }

            filteredItems = SortOrder switch
            {
                _ when SortOrder == LibrarySortOption.AlphabeticalAscending => filteredItems.OrderBy(item => item.Title),
                _ when SortOrder == LibrarySortOption.AlphabeticalDescending => filteredItems.OrderByDescending(item => item.Title),
                _ when SortOrder == LibrarySortOption.ReleaseDateAscending => filteredItems.OrderBy(item => item.ReleaseDate ?? DateOnly.MinValue),
                _ when SortOrder == LibrarySortOption.ReleaseDateDescending => filteredItems.OrderByDescending(item => item.ReleaseDate ?? DateOnly.MinValue),
                _ when SortOrder == LibrarySortOption.AddedDateAscending => filteredItems.OrderBy(item => item.LibraryItem.CreatedAt ?? DateTime.MinValue),
                _ when SortOrder == LibrarySortOption.AddedDateDescending => filteredItems.OrderByDescending(item => item.LibraryItem.CreatedAt ?? DateTime.MinValue),
                _ when SortOrder == LibrarySortOption.HighlightsAscending => filteredItems.OrderBy(item => item.HighlightCount),
                _ when SortOrder == LibrarySortOption.HighlightsDescending => filteredItems.OrderByDescending(item => item.HighlightCount),
                _ when SortOrder == LibrarySortOption.RatingAscending => filteredItems.OrderBy(item => item.Rating),
                _ when SortOrder == LibrarySortOption.RatingDescending => filteredItems.OrderByDescending(item => item.Rating),
                _ when SortOrder == LibrarySortOption.RuntimeAscending => filteredItems.OrderBy(item => item.RuntimeMinutes ?? 0),
                _ when SortOrder == LibrarySortOption.RuntimeDescending => filteredItems.OrderByDescending(item => item.RuntimeMinutes ?? 0),
                _ when SortOrder == LibrarySortOption.Random => filteredItems.OrderBy(item => Random.Shared.Next()),
                _ => filteredItems.OrderBy(item => item.Title)
            };

            var targetList = filteredItems.ToList();

            if (_isSortOrderChanging)
            {
                _isSortOrderChanging = false;
                MediaItems = new ObservableCollection<LibraryItemViewModel>(targetList);
            }
            else
            {
                var targetSet = new HashSet<LibraryItemViewModel>(targetList);

                for (var index = MediaItems.Count - 1; index >= 0; index--)
                {
                    if (!targetSet.Contains(MediaItems[index]))
                    {
                        MediaItems.RemoveAt(index);
                    }
                }

                for (var index = 0; index < targetList.Count; index++)
                {
                    var targetItem = targetList[index];
                    if (index >= MediaItems.Count)
                    {
                        MediaItems.Add(targetItem);
                    }
                    else if (MediaItems[index] != targetItem)
                    {
                        var oldIndex = MediaItems.IndexOf(targetItem);
                        if (oldIndex >= 0)
                        {
                            MediaItems.Move(oldIndex, index);
                        }
                        else
                        {
                            MediaItems.Insert(index, targetItem);
                        }
                    }
                }
            }

            foreach (var facet in AllFacets)
            {
                facet.RefreshFilteredItems();
            }

            RebuildActiveFilterChips();
            OnPropertyChanged(nameof(ItemCountText));
            OnPropertyChanged(nameof(IsEmptyResult));
        }

        private static IEnumerable<LibraryItemViewModel> ApplyFacetFilter(
            IEnumerable<LibraryItemViewModel> items,
            FacetFilterGroup facet,
            Func<LibraryItemViewModel, IReadOnlyList<string>> selector)
        {
            var required = facet.RequiredValues;
            if (required.Count > 0)
            {
                items = items.Where(item => required.All(value => selector(item).Contains(value)));
            }

            var excluded = facet.ExcludedValues;
            if (excluded.Count > 0)
            {
                items = items.Where(item => excluded.All(value => !selector(item).Contains(value)));
            }

            return items;
        }

        private void RebuildActiveFilterChips()
        {
            ActiveFilterChips.Clear();

            foreach (var facet in AllFacets)
            {
                foreach (var filterItem in facet.ActiveItems)
                {
                    var isInclude = filterItem.IsSelected == true;
                    var prefix = isInclude ? "+" : "−";
                    ActiveFilterChips.Add(new ActiveFilterChip(
                        $"{prefix} {facet.CategoryPrefix}: {filterItem.Name}",
                        isInclude,
                        !isInclude,
                        false,
                        () => filterItem.IsSelected = false));
                }
            }

            if (IsRatingFilterActive)
            {
                ActiveFilterChips.Add(new ActiveFilterChip(
                    RatingRangeDisplay,
                    false,
                    false,
                    true,
                    ResetRatingFilter));
            }

            if (IsRuntimeFilterActive)
            {
                ActiveFilterChips.Add(new ActiveFilterChip(
                    $"⏱ {RuntimeRangeDisplay}",
                    false,
                    false,
                    true,
                    ResetRuntimeFilter));
            }

            OnPropertyChanged(nameof(HasActiveFilters));
        }

        private static (LibrarySortField Field, bool Descending) DecomposeSortOption(LibrarySortOption option)
        {
            if (option == LibrarySortOption.AlphabeticalAscending)
            {
                return (LibrarySortField.Title, false);
            }

            if (option == LibrarySortOption.AlphabeticalDescending)
            {
                return (LibrarySortField.Title, true);
            }

            if (option == LibrarySortOption.ReleaseDateAscending)
            {
                return (LibrarySortField.ReleaseDate, false);
            }

            if (option == LibrarySortOption.ReleaseDateDescending)
            {
                return (LibrarySortField.ReleaseDate, true);
            }

            if (option == LibrarySortOption.AddedDateAscending)
            {
                return (LibrarySortField.AddedDate, false);
            }

            if (option == LibrarySortOption.AddedDateDescending)
            {
                return (LibrarySortField.AddedDate, true);
            }

            if (option == LibrarySortOption.HighlightsAscending)
            {
                return (LibrarySortField.Highlights, false);
            }

            if (option == LibrarySortOption.HighlightsDescending)
            {
                return (LibrarySortField.Highlights, true);
            }

            if (option == LibrarySortOption.RatingAscending)
            {
                return (LibrarySortField.Rating, false);
            }

            if (option == LibrarySortOption.RatingDescending)
            {
                return (LibrarySortField.Rating, true);
            }

            if (option == LibrarySortOption.RuntimeAscending)
            {
                return (LibrarySortField.Runtime, false);
            }

            if (option == LibrarySortOption.RuntimeDescending)
            {
                return (LibrarySortField.Runtime, true);
            }

            return (LibrarySortField.Random, true);
        }

        private static LibrarySortOption ComposeSortOption(LibrarySortField field, bool descending) => field switch
        {
            LibrarySortField.Title => descending ? LibrarySortOption.AlphabeticalDescending : LibrarySortOption.AlphabeticalAscending,
            LibrarySortField.ReleaseDate => descending ? LibrarySortOption.ReleaseDateDescending : LibrarySortOption.ReleaseDateAscending,
            LibrarySortField.AddedDate => descending ? LibrarySortOption.AddedDateDescending : LibrarySortOption.AddedDateAscending,
            LibrarySortField.Highlights => descending ? LibrarySortOption.HighlightsDescending : LibrarySortOption.HighlightsAscending,
            LibrarySortField.Rating => descending ? LibrarySortOption.RatingDescending : LibrarySortOption.RatingAscending,
            LibrarySortField.Runtime => descending ? LibrarySortOption.RuntimeDescending : LibrarySortOption.RuntimeAscending,
            LibrarySortField.Random => LibrarySortOption.Random,
            _ => LibrarySortOption.AddedDateDescending
        };

        private static string NormalizeForSearch(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            return new string([.. input.Where(char.IsLetterOrDigit)]).ToLowerInvariant();
        }
    }

    public partial class FacetFilterGroup(string label, string categoryPrefix, Action onChanged) : ObservableObject
    {
        private readonly List<FilterItem> _allItems = [];
        private readonly Action _onChanged = onChanged;
        private bool _suppressCallbacks;
        private bool _isFlyoutOpen;

        public string Label { get; } = label;
        public string CategoryPrefix { get; } = categoryPrefix;
        public ObservableCollection<FilterItem> FilteredItems { get; } = [];

        [ObservableProperty]
        public partial string SearchText { get; set; } = string.Empty;

        public int ActiveCount => _allItems.Count(item => item.IsSelected != false);
        public bool HasActiveFilters => ActiveCount > 0;
        public string ActiveCountText => $"{ActiveCount} active";

        public IReadOnlyList<string> RequiredValues => [.. _allItems.Where(item => item.IsSelected == true).Select(item => item.Name)];
        public IReadOnlyList<string> ExcludedValues => [.. _allItems.Where(item => item.IsSelected == null).Select(item => item.Name)];
        public IEnumerable<FilterItem> ActiveItems => _allItems.Where(item => item.IsSelected != false);

        partial void OnSearchTextChanged(string value) => RebuildFilteredItemsFromCurrentOrder();

        public void BeginInteraction() => _isFlyoutOpen = true;

        public void EndInteraction()
        {
            _isFlyoutOpen = false;
            CommitDisplayOrder();
        }

        public void Populate(IEnumerable<string> names, Func<string, int> countSelector)
        {
            _allItems.Clear();
            foreach (var name in names)
            {
                _allItems.Add(new FilterItem(name, countSelector(name), HandleItemChanged));
            }

            CommitDisplayOrder();
            NotifyActiveStateChanged();
        }

        [RelayCommand]
        public void Clear()
        {
            ResetSelectionsSilently();
            _onChanged();
        }

        public void ResetSelectionsSilently()
        {
            _suppressCallbacks = true;
            foreach (var item in _allItems)
            {
                item.IsSelected = false;
            }
            _suppressCallbacks = false;
            RefreshFilteredItems();
            NotifyActiveStateChanged();
        }

        public void RefreshFilteredItems()
        {
            if (_isFlyoutOpen)
            {
                return;
            }

            CommitDisplayOrder();
        }

        private void CommitDisplayOrder()
        {
            var orderedItems = _allItems
                .OrderByDescending(item => item.IsSelected != false)
                .ThenByDescending(item => item.IsSelected == true)
                .ThenBy(item => item.Name)
                .ToList();

            _allItems.Clear();
            _allItems.AddRange(orderedItems);
            RebuildFilteredItemsFromCurrentOrder();
        }

        private void RebuildFilteredItemsFromCurrentOrder()
        {
            FilteredItems.Clear();
            var matchingItems = _allItems
                .Where(item => string.IsNullOrEmpty(SearchText) || item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            foreach (var item in matchingItems)
            {
                FilteredItems.Add(item);
            }
        }

        private void HandleItemChanged()
        {
            if (_suppressCallbacks)
            {
                return;
            }

            NotifyActiveStateChanged();
            _onChanged();
        }

        private void NotifyActiveStateChanged()
        {
            OnPropertyChanged(nameof(ActiveCount));
            OnPropertyChanged(nameof(HasActiveFilters));
            OnPropertyChanged(nameof(ActiveCountText));
        }
    }

    public partial class FilterItem(string name, int itemCount, Action onChanged) : ObservableObject
    {
        public FilterItem(string name, Action onChanged) : this(name, 0, onChanged)
        {
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsIncluded), nameof(IsExcluded), nameof(StateSymbol))]
        public partial bool? IsSelected { get; set; } = false;

        public string Name { get; } = name;
        public int ItemCount { get; } = itemCount;
        public bool IsIncluded => IsSelected == true;
        public bool IsExcluded => IsSelected == null;
        public string StateSymbol => IsSelected switch
        {
            true => "+",
            null => "−",
            false => string.Empty
        };

        [RelayCommand]
        private void CycleState()
        {
            IsSelected = IsSelected switch
            {
                false => true,
                true => null,
                null => false
            };
        }

        partial void OnIsSelectedChanged(bool? value) => onChanged();
    }

    public sealed class ActiveFilterChip(string label, bool isInclude, bool isExclude, bool isRange, Action removeAction)
    {
        public string Label { get; } = label;
        public bool IsInclude { get; } = isInclude;
        public bool IsExclude { get; } = isExclude;
        public bool IsRange { get; } = isRange;
        public RelayCommand RemoveCommand { get; } = new(removeAction);
    }
}
