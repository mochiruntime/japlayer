#nullable enable
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Japlayer.Contracts;
using Japlayer.Models;

namespace Japlayer.Services
{
    public partial class SettingsService : ObservableObject, ISettingsService
    {
        private const string ConfigFileName = "japlayer.settings.json";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsConfigured))]
        public partial string ImagePath { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsConfigured))]
        public partial string SqliteDatabasePath { get; set; } = string.Empty;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ImagePath) &&
                                   !string.IsNullOrWhiteSpace(SqliteDatabasePath) &&
                                   File.Exists(SqliteDatabasePath);

        public SettingsService()
        {
            // Initial load Attempt (Sync for DI if needed, but LoadAsync is preferred)
            LoadSettings();
        }

        public async Task LoadAsync()
        {
            await Task.Run(LoadSettings);
        }

        public async Task SaveAsync()
        {
            var settings = new AppSettings
            {
                ImagePath = ImagePath,
                SqliteDatabasePath = SqliteDatabasePath
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            var configPath = GetWritableConfigPath();

            await File.WriteAllTextAsync(configPath, json);
        }

        private void LoadSettings()
        {
            var configPath = GetReadableConfigPath();

            if (configPath == null || !File.Exists(configPath))
            {
                return; // Settings not found, IsConfigured will be false
            }

            try
            {
                var json = File.ReadAllText(configPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    ImagePath = settings.ImagePath;
                    SqliteDatabasePath = settings.SqliteDatabasePath;
                }
            }
            catch (JsonException)
            {
                // Log or handle error - for now, we just don't load
            }
        }

        private string? GetReadableConfigPath()
        {
            // 1. Check for local dev config in parent directories
            var localDevConfig = FindLocalConfig(AppContext.BaseDirectory);
            if (localDevConfig != null) return localDevConfig;

            // 2. Check in AppData (User specific)
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Japlayer", ConfigFileName);
            if (File.Exists(appDataPath)) return appDataPath;

            // 3. Check in BaseDirectory
            var baseConfig = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
            if (File.Exists(baseConfig)) return baseConfig;

            return null;
        }

        private string GetWritableConfigPath()
        {
            // Prefer saving to AppData to avoid permission issues in Program Files
            var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Japlayer");
            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }
            return Path.Combine(appDataDir, ConfigFileName);
        }

        private string? FindLocalConfig(string startPath)
        {
            var currentDir = new DirectoryInfo(startPath);
            while (currentDir != null)
            {
                var localPath = Path.Combine(currentDir.FullName, ConfigFileName);
                if (File.Exists(localPath))
                {
                    return localPath;
                }
                currentDir = currentDir.Parent;
            }
            return null;
        }
    }
}

