using Japlayer.Contracts;
using Japlayer.Data.Contracts;
using Japlayer.Data.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Japlayer.ViewModels
{
    public class MediaItemViewModel(LibraryItem libraryItem, IImageProvider imageProvider, IMediaSceneProvider sceneProvider, IMediaProvider mediaProvider, ISettingsService settingsService) : INotifyPropertyChanged
    {
        private readonly LibraryItem _libraryItem = libraryItem;
        private readonly IImageProvider _imageProvider = imageProvider;
        private readonly IMediaSceneProvider _sceneProvider = sceneProvider;
        private readonly IMediaProvider _mediaProvider = mediaProvider;
        private readonly ISettingsService _settingsService = settingsService;

        private MediaItem _mediaItem;
        private ObservableCollection<MediaSceneViewModel> _scenes;
        private ObservableCollection<string> _galleryImages;

        public event PropertyChangedEventHandler PropertyChanged;

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

        public string ContentId => _mediaItem?.ContentId;
        public string ReleaseDate => _mediaItem?.ReleaseDate?.ToString();
        public string Runtime => _mediaItem?.Runtime?.ToString();

        public IReadOnlyList<string> Genres => _mediaItem?.Genres ?? [];
        public IReadOnlyList<string> Series => _mediaItem?.Series ?? [];
        public IReadOnlyList<string> Studios => _mediaItem?.Studios ?? [];
        public IReadOnlyList<string> Staff => _mediaItem?.Staff ?? [];
        public IReadOnlyList<string> Cast => _mediaItem?.Cast ?? [];

        public ObservableCollection<MediaSceneViewModel> Scenes
        {
            get => _scenes;
            private set => SetProperty(ref _scenes, value);
        }

        public ObservableCollection<string> GalleryImages
        {
            get => _galleryImages;
            private set => SetProperty(ref _galleryImages, value);
        }

        public bool IsDetailsLoaded => _mediaItem != null;

        public async Task LoadDetailsAsync()
        {
            if (IsDetailsLoaded) return;

            _mediaItem = await _mediaProvider.GetMediaItemAsync(Id);
            OnPropertyChanged(nameof(ContentId));
            OnPropertyChanged(nameof(ReleaseDate));
            OnPropertyChanged(nameof(Runtime));
            OnPropertyChanged(nameof(Genres));
            OnPropertyChanged(nameof(Series));
            OnPropertyChanged(nameof(Studios));
            OnPropertyChanged(nameof(Staff));
            OnPropertyChanged(nameof(Cast));
            OnPropertyChanged(nameof(IsDetailsLoaded));

            var scenes = await _sceneProvider.GetMediaScenesAsync(Id);
            var viewModels = scenes.Select(s => new MediaSceneViewModel(s));
            Scenes = new ObservableCollection<MediaSceneViewModel>(viewModels);

            var gallery = await _imageProvider.GetGalleryPathsAsync(Id);
            // Combine with ImagePath
            var fullPaths = gallery.Select(p => Path.Combine(_settingsService.ImagePath, p));
            GalleryImages = new ObservableCollection<string>(fullPaths);
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
