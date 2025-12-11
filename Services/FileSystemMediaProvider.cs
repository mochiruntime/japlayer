using Japlayer.Contracts;
using Japlayer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Japlayer.Services
{
    public class FileSystemMediaProvider : IMediaProvider
    {
        private readonly SettingsService _settingsService;

        public FileSystemMediaProvider(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task<IEnumerable<MediaItem>> GetAllItemsAsync()
        {
            var path = _settingsService.MediaPath;
            var items = new List<MediaItem>();

            if (!Directory.Exists(path))
            {
                // Return empty if path doesn't exist, maybe log or handle gracefully
                return items;
            }

            // Using Task.Run for IO bound work to avoid blocking UI thread on synchronous file enumeration/reading if it takes time
            return await Task.Run(() =>
            {
                var files = Directory.EnumerateFiles(path, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var item = JsonSerializer.Deserialize<MediaItem>(json);
                        if (item != null)
                        {
                            items.Add(item);
                        }
                    }
                    catch
                    {
                        // Ignore malformed files
                    }
                }
                return items;
            });
        }

        public async Task<MediaItem> GetItemAsync(string id)
        {
            // Naive implementation: scan all. Optimization would be caching or specific file lookup if ID maps to filename exactly.
            // Based on user prompt: filename = "ABF-054.json", ID = "ABF-054".
            // So we can try to look up directly.
            
            var path = _settingsService.MediaPath;
            var filePath = Path.Combine(path, $"{id}.json");

            if (File.Exists(filePath))
            {
                try
                {
                    using var stream = File.OpenRead(filePath);
                    return await JsonSerializer.DeserializeAsync<MediaItem>(stream);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }
    }
}
