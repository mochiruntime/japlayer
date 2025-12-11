using Japlayer.Contracts;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Japlayer.Services
{
    public class FileSystemImageProvider : IImageProvider
    {
        private readonly SettingsService _settingsService;

        public FileSystemImageProvider(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public string GetCoverPath(string id)
        {
            var path = _settingsService.ImagePath;
            // Convention: ID_cover.webp
            var coverPath = Path.Combine(path, $"{id}_cover.webp");
            if (File.Exists(coverPath)) return coverPath;
            return null; // Or return a placeholder path
        }

        public string GetThumbPath(string id)
        {
             var path = _settingsService.ImagePath;
            // Convention: ID_thumb.webp
            var thumbPath = Path.Combine(path, $"{id}_thumb.webp");
            if (File.Exists(thumbPath)) return thumbPath;
            return null;
        }

        public IEnumerable<string> GetGalleryPaths(string id)
        {
            var path = _settingsService.ImagePath;
            if (!Directory.Exists(path)) return Enumerable.Empty<string>();

            // naming convention: ID_0.jpg, ID_1.jpg ... 
            // We can search for pattern {id}_*.jpg (excluding cover/thumb if they match, but they are .webp usually)
            
            var images = Directory.EnumerateFiles(path, $"{id}_*.jpg");
            return images;
        }
    }
}
