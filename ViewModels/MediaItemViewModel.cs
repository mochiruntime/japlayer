using Japlayer.Contracts;
using Japlayer.Models;

namespace Japlayer.ViewModels
{
    public class MediaItemViewModel
    {
        private readonly MediaItem _mediaItem;
        private readonly IImageProvider _imageProvider;

        public MediaItemViewModel(MediaItem mediaItem, IImageProvider imageProvider)
        {
            _mediaItem = mediaItem;
            _imageProvider = imageProvider;
        }

        public string Id => _mediaItem.Id;
        public string Title => _mediaItem.Id + " " + _mediaItem.Title;
        public string CoverPath => _imageProvider.GetCoverPath(_mediaItem.Id);
        public string MdbId => _mediaItem.MdbId;
        
        // Helper for UI binding if path is null (fallback image could be handled here or in XAML)
        public string DisplayCover => CoverPath ?? "ms-appx:///Assets/StoreLogo.png"; 
    }
}
