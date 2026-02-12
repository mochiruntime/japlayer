using Japlayer.Contracts;
using Japlayer.Models;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Japlayer.ViewModels
{
    public class LibraryViewModel
    {
        private readonly IMediaProvider _mediaProvider;
        private readonly IServiceProvider _serviceProvider;
        private List<MediaItemViewModel> _allMediaItems = new();

        public ObservableCollection<MediaItemViewModel> MediaItems { get; } = new();
        public bool IsDataLoaded { get; private set; }

        public LibraryViewModel(IMediaProvider mediaProvider, IServiceProvider serviceProvider)
        {
            _mediaProvider = mediaProvider;
            _serviceProvider = serviceProvider;
        }

        public async Task LoadDataAsync()
        {
            var items = await _mediaProvider.GetAllItemsAsync();
            _allMediaItems = items.Select(item => ActivatorUtilities.CreateInstance<MediaItemViewModel>(_serviceProvider, item)).ToList();

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
