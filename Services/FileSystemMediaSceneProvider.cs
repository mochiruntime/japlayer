using Japlayer.Contracts;
using Japlayer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Japlayer.Services
{
    public class FileSystemMediaSceneProvider : IMediaSceneProvider
    {
        private readonly SettingsService _settings;
        private List<MediaScene> _cachedScenes;

        public FileSystemMediaSceneProvider(SettingsService settings)
        {
            _settings = settings;
        }

        private void EnsureLoaded()
        {
            if (_cachedScenes != null) return;

            var path = _settings.LocalMediaStorage;
            var json = File.ReadAllText(path);
            _cachedScenes = JsonSerializer.Deserialize<List<MediaScene>>(json) ?? new List<MediaScene>();
        }

        public IEnumerable<MediaScene> GetScenes(string mediaId)
        {
            EnsureLoaded();
            return _cachedScenes.Where(s => s.Id == mediaId);
        }
    }
}
