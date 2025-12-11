using System;
using System.IO;
using System.Text.Json;

namespace Japlayer.Services
{
    public class SettingsService
    {
        private const string DefaultMediaType = "X:\\";
        private const string DefaultImageType = "Y:\\";
        private const string ConfigFileName = "appsettings.json";

        public string MediaPath { get; private set; }
        public string ImagePath { get; private set; }

        public SettingsService()
        {
            MediaPath = DefaultMediaType;
            ImagePath = DefaultImageType;
            LoadSettings();
        }

        private void LoadSettings()
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        if (!string.IsNullOrWhiteSpace(settings.MediaPath)) MediaPath = settings.MediaPath;
                        if (!string.IsNullOrWhiteSpace(settings.ImagePath)) ImagePath = settings.ImagePath;
                    }
                }
                catch
                {
                    // Ignore errors, use defaults
                }
            }
        }

        private class AppSettings
        {
            public string MediaPath { get; set; }
            public string ImagePath { get; set; }
        }
    }
}
