using Japlayer.Contracts;
using Japlayer.Models;
using System.Collections.Generic;

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
        public string OriginalTitle => _mediaItem.Title; // Explicit title without ID prefix
        public string CoverPath => _imageProvider.GetCoverPath(_mediaItem.Id);
        public string MdbId => _mediaItem.MdbId;
        public string ReleaseDate => _mediaItem.ReleaseDate;
        public string Runtime => _mediaItem.Runtime;
        
        public IReadOnlyList<string> Genres => _mediaItem.Genres;
        public IReadOnlyList<string> Series => _mediaItem.Series;
        public IReadOnlyList<string> Studios => _mediaItem.Studios;
        public IReadOnlyList<string> Staff => _mediaItem.Staff;
        public IReadOnlyList<string> Cast => _mediaItem.Cast;

        public IEnumerable<string> GalleryImages
        {
            get
            {
                var list = new List<string>();
                var thumb = _imageProvider.GetThumbPath(_mediaItem.Id);
                if (!string.IsNullOrEmpty(thumb)) list.Add(thumb);
                
                var gallery = _imageProvider.GetGalleryPaths(_mediaItem.Id);
                if (gallery != null) list.AddRange(gallery);
                
                return list;
            }
        }

        // Helper for UI binding
        public string DisplayCover => CoverPath ?? "ms-appx:///Assets/StoreLogo.png";  
    }
}
