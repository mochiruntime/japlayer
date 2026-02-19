#nullable enable
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Japlayer.Contracts;
using Japlayer.Data.Models;

namespace Japlayer.ViewModels
{
    public partial class LibraryItemViewModel(LibraryItem libraryItem, ISettingsService settingsService) : ObservableObject
    {
        private readonly LibraryItem _libraryItem = libraryItem;
        private readonly ISettingsService _settingsService = settingsService;

        public string Id => _libraryItem.MediaId;
        public string Title => _libraryItem.MediaId + " " + (_libraryItem.Title ?? string.Empty);
        public string? OriginalTitle => _libraryItem.Title;

        public string? CoverPath
        {
            get
            {
                if (string.IsNullOrEmpty(_libraryItem.CoverImagePath)) return null;
                return Path.Combine(_settingsService.ImagePath, _libraryItem.CoverImagePath);
            }
        }

        public string DisplayCover => CoverPath ?? "ms-appx:///Assets/NoCover.png";

        // For convenience if we need the original item later
        public LibraryItem LibraryItem => _libraryItem;
    }
}
