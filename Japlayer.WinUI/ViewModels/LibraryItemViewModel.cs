#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Japlayer.Contracts;
using Japlayer.Data.Contracts;
using Japlayer.Data.Helpers;
using Japlayer.Data.Models;

namespace Japlayer.ViewModels
{
    public partial class LibraryItemViewModel : ObservableObject
    {
        private LibraryItem _libraryItem;
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
            Rating = libraryItem.Rating;
            NormalizedId = NormalizeSearchText(libraryItem.MediaId);

            if (string.IsNullOrEmpty(_libraryItem.CoverImagePath))
            {
                if (!string.IsNullOrEmpty(_libraryItem.FallbackThumbnailPath))
                {
                    _fallbackThumbnailPath = Path.Combine(_settingsService.ImagePath, _libraryItem.FallbackThumbnailPath);
                }
                else
                {
                    _ = LoadFallbackThumbnailAsync();
                }
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

                    var fallbackThumbnail = ThumbnailFallbackSelector.SelectFallbackItem(sortedThumbnails);
                    if (fallbackThumbnail != null)
                    {
                        _fallbackThumbnailPath = Path.Combine(_settingsService.ImagePath, fallbackThumbnail.Path);
                    }

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
        [NotifyPropertyChangedFor(nameof(HasHighlights))]
        public partial int HighlightCount { get; set; }

        public bool HasHighlights => HighlightCount > 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasRating), nameof(RatingDisplay))]
        public partial int Rating { get; set; }

        partial void OnRatingChanged(int value) => _libraryItem.Rating = value;

        public bool HasRating => Rating > 0;
        public string RatingDisplay => Rating.ToString();

        public int SceneCount => _libraryItem.SceneCount;
        public bool HasScenes => _libraryItem.SceneCount > 0;

        public int? RuntimeMinutes => _libraryItem.RuntimeMinutes;
        public string NormalizedId { get; private set; }

        public bool AddUserTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName) ||
                _libraryItem.UserTags.Any(tag => string.Equals(tag, tagName, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            _libraryItem.UserTags = [.. _libraryItem.UserTags, tagName];
            return true;
        }

        public bool RemoveUserTag(string tagName)
        {
            if (!_libraryItem.UserTags.Any(tag => string.Equals(tag, tagName, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            _libraryItem.UserTags = [.. _libraryItem.UserTags.Where(tag => !string.Equals(tag, tagName, StringComparison.OrdinalIgnoreCase))];
            return true;
        }

        /// <summary>
        /// Refreshes metadata and persisted state from a reloaded LibraryItem while preserving cached hover thumbnails.
        /// </summary>
        public void UpdateFrom(LibraryItem updatedItem)
        {
            _libraryItem = updatedItem;
            NormalizedId = NormalizeSearchText(updatedItem.MediaId);
            Rating = updatedItem.Rating;
            HighlightCount = updatedItem.HighlightCount;

            if (string.IsNullOrEmpty(updatedItem.CoverImagePath) && !string.IsNullOrEmpty(updatedItem.FallbackThumbnailPath))
            {
                _fallbackThumbnailPath = Path.Combine(_settingsService.ImagePath, updatedItem.FallbackThumbnailPath);
            }

            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(OriginalTitle));
            OnPropertyChanged(nameof(ReleaseDate));
            OnPropertyChanged(nameof(FormattedReleaseDate));
            OnPropertyChanged(nameof(SceneCount));
            OnPropertyChanged(nameof(HasScenes));
            OnPropertyChanged(nameof(RuntimeMinutes));
            OnPropertyChanged(nameof(DisplayCover));
            OnPropertyChanged(nameof(DisplayImageSource));
        }

        private static string NormalizeSearchText(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            return new string([.. input.Where(char.IsLetterOrDigit)]).ToLowerInvariant();
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayImageSource))]
        public partial string? CurrentImageSource { get; set; }

        public string DisplayImageSource => CurrentImageSource ?? DisplayCover;

        public string Id => _libraryItem.MediaId;
        public string Title => _libraryItem.MediaId + " " + (_libraryItem.Title ?? string.Empty);
        public string? OriginalTitle => _libraryItem.Title;
        public DateOnly? ReleaseDate => _libraryItem.ReleaseDate;
        public string FormattedReleaseDate => _libraryItem.ReleaseDate?.ToString("MMM d, yyyy") ?? string.Empty;

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

        public IReadOnlyList<string> ThumbnailPaths => _thumbnails;

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
