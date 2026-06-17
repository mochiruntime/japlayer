#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Japlayer.Data.Contracts;
using Japlayer.Data.Models;
using Japlayer.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace Japlayer.ViewModels
{
    public partial class LibraryViewModel(IMediaProvider mediaProvider, IMediaSceneProvider sceneProvider, IServiceProvider serviceProvider) : ObservableObject
    {
        private readonly IMediaProvider _mediaProvider = mediaProvider;
        private readonly IMediaSceneProvider _sceneProvider = sceneProvider;
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private List<LibraryItemViewModel> _allMediaItems = [];
        private List<FilterItem> _allTagFilters = [];
        private List<FilterItem> _allGenreFilters = [];
        private DispatcherTimer? _filterDebounceTimer;

        [ObservableProperty]
        public partial ObservableCollection<LibraryItemViewModel> MediaItems { get; set; } = [];
        public ObservableCollection<FilterItem> TagFilterItems { get; } = [];
        public ObservableCollection<FilterItem> GenreFilterItems { get; } = [];
        public IReadOnlyList<LibrarySortOption> SortOptions { get; } = LibrarySortOption.All;

        public Task<IEnumerable<MediaScene>> GetMediaScenesAsync(string mediaId) => _sceneProvider.GetMediaScenesAsync(mediaId);

        [ObservableProperty]
        public partial bool IsDataLoaded { get; set; }

        [ObservableProperty]
        public partial string SearchText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string TagSearchText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string GenreSearchText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial LibrarySortOption SortOrder { get; set; } = LibrarySortOption.AddedDateDescending;

        partial void OnSearchTextChanged(string value) => QueueApplyFilter();
        partial void OnTagSearchTextChanged(string value) => UpdateTagFilterItems();
        partial void OnGenreSearchTextChanged(string value) => UpdateGenreFilterItems();
        partial void OnSortOrderChanged(LibrarySortOption value) => QueueApplyFilter();

        public async Task LoadDataAsync()
        {
            var itemsTask = _mediaProvider.GetLibraryItemsAsync();
            var tagsTask = _mediaProvider.GetUserTagsAsync();
            var genresTask = _mediaProvider.GetGenresAsync();

            await Task.WhenAll(itemsTask, tagsTask, genresTask);

            var items = await itemsTask;
            var tags = await tagsTask;
            var genres = await genresTask;

            _allMediaItems = [.. items.Select(item => ActivatorUtilities.CreateInstance<LibraryItemViewModel>(_serviceProvider, item))];
            _allTagFilters = [.. tags.Select(tag => new FilterItem(tag, QueueApplyFilter))];
            _allGenreFilters = [.. genres.Select(genre => new FilterItem(genre, QueueApplyFilter))];

            UpdateTagFilterItems();
            UpdateGenreFilterItems();
            ApplyFilter();
            IsDataLoaded = true;
        }

        private void UpdateTagFilterItems()
        {
            TagFilterItems.Clear();
            var filteredTags = _allTagFilters
                .Where(t => string.IsNullOrEmpty(TagSearchText) || t.Name.Contains(TagSearchText, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => t.IsSelected != false)
                .ThenByDescending(t => t.IsSelected == true)
                .ThenBy(t => t.Name);

            foreach (var tag in filteredTags)
            {
                TagFilterItems.Add(tag);
            }
        }

        private void UpdateGenreFilterItems()
        {
            GenreFilterItems.Clear();
            var filteredGenres = _allGenreFilters
                .Where(g => string.IsNullOrEmpty(GenreSearchText) || g.Name.Contains(GenreSearchText, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(g => g.IsSelected != false)
                .ThenByDescending(g => g.IsSelected == true)
                .ThenBy(g => g.Name);

            foreach (var genre in filteredGenres)
            {
                GenreFilterItems.Add(genre);
            }
        }

        private void QueueApplyFilter()
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

        private void ApplyFilter()
        {
            var requiredTags = _allTagFilters.Where(t => t.IsSelected == true).Select(t => t.Name).ToList();
            var excludedTags = _allTagFilters.Where(t => t.IsSelected == null).Select(t => t.Name).ToList();
            var requiredGenres = _allGenreFilters.Where(g => g.IsSelected == true).Select(g => g.Name).ToList();
            var excludedGenres = _allGenreFilters.Where(g => g.IsSelected == null).Select(g => g.Name).ToList();

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
                        item.LibraryItem.Genres.Any(genre => genre.Contains(term, StringComparison.OrdinalIgnoreCase))
                    );
                }
            }

            if (requiredTags.Count > 0)
            {
                filteredItems = filteredItems.Where(item => requiredTags.All(tag => item.LibraryItem.UserTags.Contains(tag)));
            }

            if (excludedTags.Count > 0)
            {
                filteredItems = filteredItems.Where(item => excludedTags.All(tag => !item.LibraryItem.UserTags.Contains(tag)));
            }

            if (requiredGenres.Count > 0)
            {
                filteredItems = filteredItems.Where(item => requiredGenres.All(genre => item.LibraryItem.Genres.Contains(genre)));
            }

            if (excludedGenres.Count > 0)
            {
                filteredItems = filteredItems.Where(item => excludedGenres.All(genre => !item.LibraryItem.Genres.Contains(genre)));
            }

            filteredItems = SortOrder switch
            {
                _ when SortOrder == LibrarySortOption.AlphabeticalAscending => filteredItems.OrderBy(item => item.Title),
                _ when SortOrder == LibrarySortOption.AlphabeticalDescending => filteredItems.OrderByDescending(item => item.Title),
                _ when SortOrder == LibrarySortOption.ReleaseDateAscending => filteredItems.OrderBy(item => item.ReleaseDate ?? DateOnly.MinValue),
                _ when SortOrder == LibrarySortOption.ReleaseDateDescending => filteredItems.OrderByDescending(item => item.ReleaseDate ?? DateOnly.MinValue),
                _ when SortOrder == LibrarySortOption.AddedDateAscending => filteredItems.OrderBy(item => item.LibraryItem.CreatedAt ?? DateTime.MinValue),
                _ when SortOrder == LibrarySortOption.AddedDateDescending => filteredItems.OrderByDescending(item => item.LibraryItem.CreatedAt ?? DateTime.MinValue),
                _ when SortOrder == LibrarySortOption.Random => filteredItems.OrderBy(item => Random.Shared.Next()),
                _ => filteredItems.OrderBy(item => item.Title)
            };

            var targetList = filteredItems.ToList();
            MediaItems = new ObservableCollection<LibraryItemViewModel>(targetList);

            UpdateTagFilterItems();
            UpdateGenreFilterItems();
        }

        private static string NormalizeForSearch(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            return new string([.. input.Where(char.IsLetterOrDigit)]).ToLowerInvariant();
        }
    }

    public partial class FilterItem(string name, Action onChanged) : ObservableObject
    {
        [ObservableProperty]
        public partial bool? IsSelected { get; set; } = false;

        public string Name { get; } = name;

        partial void OnIsSelectedChanged(bool? value) => onChanged();
    }
}
