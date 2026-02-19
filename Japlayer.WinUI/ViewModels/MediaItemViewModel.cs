#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Japlayer.Contracts;
using Japlayer.Data.Contracts;
using Japlayer.Data.Models;

namespace Japlayer.ViewModels
{
    public partial class MediaItemViewModel(LibraryItem libraryItem, IImageProvider imageProvider, IMediaSceneProvider sceneProvider, IMediaProvider mediaProvider, ISettingsService settingsService) : ObservableObject
    {
        private readonly LibraryItem _libraryItem = libraryItem;
        private readonly IImageProvider _imageProvider = imageProvider;
        private readonly IMediaSceneProvider _sceneProvider = sceneProvider;
        private readonly IMediaProvider _mediaProvider = mediaProvider;
        private readonly ISettingsService _settingsService = settingsService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContentId))]
        [NotifyPropertyChangedFor(nameof(ReleaseDate))]
        [NotifyPropertyChangedFor(nameof(Runtime))]
        [NotifyPropertyChangedFor(nameof(Genres))]
        [NotifyPropertyChangedFor(nameof(Series))]
        [NotifyPropertyChangedFor(nameof(Studios))]
        [NotifyPropertyChangedFor(nameof(Staff))]
        [NotifyPropertyChangedFor(nameof(Cast))]
        [NotifyPropertyChangedFor(nameof(IsDetailsLoaded))]
        public partial MediaItem? MediaItemData { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<MediaSceneViewModel>? Scenes { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<string>? GalleryImages { get; set; }

        public string Id => _libraryItem.MediaId;
        public string Title => _libraryItem.MediaId + " " + _libraryItem.Title;
        public string DisplayCover
        {
            get
            {
                if (string.IsNullOrEmpty(_libraryItem.CoverImagePath)) return "ms-appx:///Assets/NoCover.png";
                return Path.Combine(_settingsService.ImagePath, _libraryItem.CoverImagePath);
            }
        }

        public string? ContentId => MediaItemData?.ContentId;
        public string? ReleaseDate => MediaItemData?.ReleaseDate?.ToString();
        public string? Runtime => MediaItemData?.Runtime?.ToString();

        public IReadOnlyList<string> Genres => MediaItemData?.Genres ?? [];
        public IReadOnlyList<string> Series => MediaItemData?.Series ?? [];
        public IReadOnlyList<string> Studios => MediaItemData?.Studios ?? [];
        public IReadOnlyList<string> Staff => MediaItemData?.Staff ?? [];
        public IReadOnlyList<string> Cast => MediaItemData?.Cast ?? [];

        public bool IsDetailsLoaded => MediaItemData != null;

        public async Task LoadDetailsAsync()
        {
            if (IsDetailsLoaded) return;

            MediaItemData = await _mediaProvider.GetMediaItemAsync(Id);

            var scenes = await _sceneProvider.GetMediaScenesAsync(Id);
            var viewModels = scenes.Select(s => new MediaSceneViewModel(s));
            Scenes = new ObservableCollection<MediaSceneViewModel>(viewModels);

            var gallery = await _imageProvider.GetGalleryPathsAsync(Id);
            // Combine with ImagePath
            var fullPaths = gallery.Select(p => Path.Combine(_settingsService.ImagePath, p));
            GalleryImages = new ObservableCollection<string>(fullPaths);
        }
    }
}
