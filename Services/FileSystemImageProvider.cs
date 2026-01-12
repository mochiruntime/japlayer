using Japlayer.Contracts;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Japlayer.Services
{
    public class FileSystemImageProvider : IImageProvider
    {
        private readonly ISettingsService _settingsService;
        private readonly ReadOnlyCollection<string> _allowedExtensions;

        public FileSystemImageProvider(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _allowedExtensions = new ReadOnlyCollection<string>(new string[] { ".webp", ".jpg", ".jpeg", ".png" });
        }

        public string GetCoverPath(string id)
        {
            var path = _settingsService.ImagePath;
            foreach (var extension in _allowedExtensions)
            {
                var coverPath = Path.Combine(path, $"{id}_cover{extension}");
                if (File.Exists(coverPath)) return coverPath;
            }
            return null; // Or return a placeholder path
        }

        public string GetThumbPath(string id)
        {
            var path = _settingsService.ImagePath;
            foreach (var extension in _allowedExtensions)
            {
                var thumbPath = Path.Combine(path, $"{id}_thumb{extension}");
                if (File.Exists(thumbPath)) return thumbPath;
            }
            return null;
        }

        public IEnumerable<string> GetGalleryPaths(string id)
        {
            var path = _settingsService.ImagePath;
            if (!Directory.Exists(path)) return Enumerable.Empty<string>();

            // naming convention: ID_0.jpg, ID_1.jpg ...

            // Build regex to match: id_ + digits + allowed extension
            string pattern = $@"^{Regex.Escape(id)}_\d+({string.Join("|", _allowedExtensions.Select(e => Regex.Escape(e)))})$";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);

            // Use prefix wildcard to reduce number of files enumerated
            var images = _allowedExtensions
                .SelectMany(ext => Directory.EnumerateFiles(path, $"{id}_*{ext}"))
                .Where(file => regex.IsMatch(Path.GetFileName(file)));
            return images;
        }
    }
}
