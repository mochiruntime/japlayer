using Japlayer.Data.Contracts;
using Japlayer.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Japlayer.ViewModels
{
    public class LibraryViewModel(IMediaProvider mediaProvider, IServiceProvider serviceProvider)
    {
        private readonly IMediaProvider _mediaProvider = mediaProvider;
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private List<MediaItemViewModel> _allMediaItems = [];

        public ObservableCollection<MediaItemViewModel> MediaItems { get; } = [];
        public bool IsDataLoaded { get; private set; }

        public async Task LoadDataAsync()
        {
            var items = await _mediaProvider.GetLibraryItemsAsync();
            _allMediaItems = [.. items.Select(item => ActivatorUtilities.CreateInstance<MediaItemViewModel>(_serviceProvider, item))];

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
