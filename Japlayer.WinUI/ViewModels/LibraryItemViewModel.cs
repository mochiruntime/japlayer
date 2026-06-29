#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Japlayer.Contracts;
using Japlayer.Data.Contracts;
using Japlayer.Data.Models;

namespace Japlayer.ViewModels
{
    public partial class LibraryItemViewModel : ObservableObject
    {
        private readonly LibraryItem _libraryItem;
        private readonly ISettingsService _settingsService;
        private readonly IMediaThumbnailProvider _thumbnailProvider;

        private List<string> _thumbnails = [];
        private bool _areThumbnailsLoaded;
        private string? _fallbackThumbnailPath;

        public LibraryItemViewModel(LibraryItem libraryItem, ISettingsService settingsService, IMediaThumbnailProvider thumbnailProvider)
        {
            _libraryItem = libraryItem;
            _settingsService = settingsService;
            _thumbnailProvider = thumbnailProvider;
            HighlightCount = libraryItem.HighlightCount;

            if (string.IsNullOrEmpty(_libraryItem.CoverImagePath))
            {
                _ = LoadFallbackThumbnailAsync();
            }
        }

        private async Task LoadFallbackThumbnailAsync()
        {
            try
            {
                var thumbnails = await _thumbnailProvider.GetThumbnailsAsync(Id);
                var sortedThumbnails = thumbnails.OrderBy(thumbnail => thumbnail.Scene).ThenBy(thumbnail => thumbnail.Timestamp).ToList();
                if (sortedThumbnails.Count > 0)
                {
                    // Cache the loaded thumbnails so we don't reload them on hover
                    _thumbnails = [.. sortedThumbnails.Select(thumbnail => Path.Combine(_settingsService.ImagePath, thumbnail.Path))];
                    _areThumbnailsLoaded = true;

                    var index = Math.Clamp((int)Math.Round((sortedThumbnails.Count - 1) * 0.75), 0, sortedThumbnails.Count - 1);
                    var fallbackThumbnail = sortedThumbnails[index];
                    _fallbackThumbnailPath = Path.Combine(_settingsService.ImagePath, fallbackThumbnail.Path);

                    OnPropertyChanged(nameof(DisplayCover));
                    OnPropertyChanged(nameof(DisplayImageSource));
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load fallback cover for {Id}: {exception.Message}");
            }
        }

        [ObservableProperty]
        public partial int HighlightCount { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayImageSource))]
        public partial string? CurrentImageSource { get; set; }

        public string DisplayImageSource => CurrentImageSource ?? DisplayCover;

        public string Id => _libraryItem.MediaId;
        public string Title => _libraryItem.MediaId + " " + (_libraryItem.Title ?? string.Empty);
        public string? OriginalTitle => _libraryItem.Title;
        public DateOnly? ReleaseDate => _libraryItem.ReleaseDate;

        public string? CoverPath
        {
            get
            {
                if (string.IsNullOrEmpty(_libraryItem.CoverImagePath))
                {
                    return null;
                }

                return Path.Combine(_settingsService.ImagePath, _libraryItem.CoverImagePath);
            }
        }

        public string DisplayCover => CoverPath ?? _fallbackThumbnailPath ?? "ms-appx:///Assets/NoCover.png";

        public async Task LoadThumbnailsAsync()
        {
            if (_areThumbnailsLoaded)
            {
                return;
            }

            try
            {
                var thumbs = await _thumbnailProvider.GetThumbnailsAsync(Id);
                _thumbnails = [.. thumbs.Select(t => Path.Combine(_settingsService.ImagePath, t.Path))];
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load thumbnails for {Id}: {ex.Message}");
            }
            finally
            {
                _areThumbnailsLoaded = true;
            }
        }

        public void ScrubPreview(double percentage)
        {
            if (_thumbnails.Count == 0)
            {
                return;
            }

            var index = Math.Clamp((int)(percentage * _thumbnails.Count), 0, _thumbnails.Count - 1);
            CurrentImageSource = _thumbnails[index];
        }

        public void ResetPreview() => CurrentImageSource = null;

        // For convenience if we need the original item later
        public LibraryItem LibraryItem => _libraryItem;
    }
}
