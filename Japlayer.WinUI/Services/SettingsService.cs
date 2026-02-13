using System;
using System.IO;
using System.Text.Json;
using Japlayer.Contracts;

namespace Japlayer.Services
{
    public class SettingsService : ISettingsService
    {
        private const string ConfigFileName = "appsettings.json";
        private const string LocalConfigFileName = "appsettings.json.local";

        public string ImagePath { get; private set; }
        public string SqliteDatabasePath { get; private set; }

        public SettingsService()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);

            if (!File.Exists(configPath))
            {
                configPath = FindLocalConfig(AppContext.BaseDirectory);
            }

            if (configPath == null || !File.Exists(configPath))
            {
                throw new FileNotFoundException($"Configuration file '{ConfigFileName}' not found in '{AppContext.BaseDirectory}' or as '{LocalConfigFileName}' in parent directories.", ConfigFileName);
            }

            var json = File.ReadAllText(configPath);
            try
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    if (!string.IsNullOrWhiteSpace(settings.ImagePath)) ImagePath = settings.ImagePath;
                    if (!string.IsNullOrWhiteSpace(settings.SqliteDatabasePath)) SqliteDatabasePath = settings.SqliteDatabasePath;
                }
            }
            catch (JsonException ex)
            {
                throw new Exception($"Error parsing configuration file at '{configPath}': {ex.Message}", ex);
            }
        }

        private string FindLocalConfig(string startPath)
        {
            var currentDir = new DirectoryInfo(startPath);
            while (currentDir != null)
            {
                var localPath = Path.Combine(currentDir.FullName, LocalConfigFileName);
                if (File.Exists(localPath))
                {
                    return localPath;
                }
                currentDir = currentDir.Parent;
            }
            return null;
        }

        private class AppSettings
        {
            public string ImagePath { get; set; }
            public string SqliteDatabasePath { get; set; }
        }
    }
}
