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

        public ObservableCollection<LibraryItemViewModel> MediaItems { get; } = [];

        [ObservableProperty]
        public partial bool IsDataLoaded { get; set; }

        public async Task LoadDataAsync()
        {
            var items = await _mediaProvider.GetLibraryItemsAsync();
            _allMediaItems = [.. items.Select(item => ActivatorUtilities.CreateInstance<LibraryItemViewModel>(_serviceProvider, item))];

            ApplyFilter();
            IsDataLoaded = true;
        }

        private void ApplyFilter()
        {
            MediaItems.Clear();
            foreach (var item in _allMediaItems)
            {
                MediaItems.Add(item);
            }
        }
    }
}
