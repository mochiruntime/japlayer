#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Japlayer.Data.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Japlayer.ViewModels
{
    public partial class LibraryViewModel(IMediaProvider mediaProvider, IServiceProvider serviceProvider) : ObservableObject
    {
        private readonly IMediaProvider _mediaProvider = mediaProvider;
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private List<LibraryItemViewModel> _allMediaItems = [];
        private List<FilterItem> _allTagFilters = [];
        private List<FilterItem> _allGenreFilters = [];

        public ObservableCollection<LibraryItemViewModel> MediaItems { get; } = [];
        public ObservableCollection<FilterItem> TagFilterItems { get; } = [];
        public ObservableCollection<FilterItem> GenreFilterItems { get; } = [];

        [ObservableProperty]
        public partial bool IsDataLoaded { get; set; }

        [ObservableProperty]
        public partial string TagSearchText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string GenreSearchText { get; set; } = string.Empty;

        partial void OnTagSearchTextChanged(string value) => UpdateTagFilterItems();
        partial void OnGenreSearchTextChanged(string value) => UpdateGenreFilterItems();

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
            _allTagFilters = [.. tags.Select(tag => new FilterItem(tag, ApplyFilter))];
            _allGenreFilters = [.. genres.Select(genre => new FilterItem(genre, ApplyFilter))];

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

        private void ApplyFilter()
        {
            var requiredTags = _allTagFilters.Where(t => t.IsSelected == true).Select(t => t.Name).ToList();
            var excludedTags = _allTagFilters.Where(t => t.IsSelected == null).Select(t => t.Name).ToList();
            var requiredGenres = _allGenreFilters.Where(g => g.IsSelected == true).Select(g => g.Name).ToList();
            var excludedGenres = _allGenreFilters.Where(g => g.IsSelected == null).Select(g => g.Name).ToList();

            MediaItems.Clear();
            var filteredItems = _allMediaItems.AsEnumerable();

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

            foreach (var item in filteredItems)
            {
                MediaItems.Add(item);
            }

            UpdateTagFilterItems();
            UpdateGenreFilterItems();
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
