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
        private List<UserTagFilterItem> _allTagFilters = [];

        public ObservableCollection<LibraryItemViewModel> MediaItems { get; } = [];
        public ObservableCollection<UserTagFilterItem> TagFilterItems { get; } = [];

        [ObservableProperty]
        public partial bool IsDataLoaded { get; set; }

        [ObservableProperty]
        public partial string TagSearchText { get; set; } = string.Empty;

        partial void OnTagSearchTextChanged(string value)
        {
            UpdateTagFilterItems();
        }

        public async Task LoadDataAsync()
        {
            var itemsTask = _mediaProvider.GetLibraryItemsAsync();
            var tagsTask = _mediaProvider.GetUserTagsAsync();

            await Task.WhenAll(itemsTask, tagsTask);

            var items = await itemsTask;
            var tags = await tagsTask;

            _allMediaItems = [.. items.Select(item => ActivatorUtilities.CreateInstance<LibraryItemViewModel>(_serviceProvider, item))];
            _allTagFilters = [.. tags.Select(tag => new UserTagFilterItem(tag, ApplyFilter))];

            UpdateTagFilterItems();
            ApplyFilter();
            IsDataLoaded = true;
        }

        private void UpdateTagFilterItems()
        {
            TagFilterItems.Clear();
            var filteredTags = _allTagFilters
                .Where(t => string.IsNullOrEmpty(TagSearchText) || t.Name.Contains(TagSearchText, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => t.IsSelected != false) // Put both checked and indefinite at top
                .ThenByDescending(t => t.IsSelected == true) // Put checked above indefinite
                .ThenBy(t => t.Name);

            foreach (var tag in filteredTags)
            {
                TagFilterItems.Add(tag);
            }
        }

        private void ApplyFilter()
        {
            var requiredTags = _allTagFilters.Where(t => t.IsSelected == true).Select(t => t.Name).ToList();
            var excludedTags = _allTagFilters.Where(t => t.IsSelected == null).Select(t => t.Name).ToList();

            MediaItems.Clear();
            var filteredItems = _allMediaItems.AsEnumerable();

            if (requiredTags.Count > 0)
            {
                // AND logic: item must have ALL required tags
                filteredItems = filteredItems.Where(item => requiredTags.All(tag => item.LibraryItem.UserTags.Contains(tag)));
            }

            if (excludedTags.Count > 0)
            {
                // AND logic: item must NOT have ANY of the excluded tags
                filteredItems = filteredItems.Where(item => excludedTags.All(tag => !item.LibraryItem.UserTags.Contains(tag)));
            }

            foreach (var item in filteredItems)
            {
                MediaItems.Add(item);
            }

            // Also update the tag order if they changed selection
            UpdateTagFilterItems();
        }
    }

    public partial class UserTagFilterItem(string name, Action onChanged) : ObservableObject
    {
        [ObservableProperty]
        public partial bool? IsSelected { get; set; } = false;

        public string Name { get; } = name;

        partial void OnIsSelectedChanged(bool? value) => onChanged();
    }
}
