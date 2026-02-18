using Japlayer.Contracts;
using Japlayer.Data.Models;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace Japlayer.ViewModels
{
    public class LibraryItemViewModel(LibraryItem libraryItem, ISettingsService settingsService) : INotifyPropertyChanged
    {
        private readonly LibraryItem _libraryItem = libraryItem;
        private readonly ISettingsService _settingsService = settingsService;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id => _libraryItem.MediaId;
        public string Title => _libraryItem.MediaId + " " + _libraryItem.Title;
        public string OriginalTitle => _libraryItem.Title;

        public string CoverPath
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

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
