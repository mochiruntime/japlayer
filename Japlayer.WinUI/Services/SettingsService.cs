#nullable enable
using System;
using System.IO;
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
        public partial string ImagePath { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string SqliteDatabasePath { get; set; } = string.Empty;

        public SettingsService()
        {
            LoadSettings();
            InitializeDefaults();
        }

        private string BaseAppDataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Japlayer");

        private void InitializeDefaults()
        {
            EnsureDirectoryExists(BaseAppDataPath);

            if (string.IsNullOrWhiteSpace(ImagePath))
            {
                ImagePath = Path.Combine(BaseAppDataPath, "images");
                EnsureDirectoryExists(ImagePath);
            }

            if (string.IsNullOrWhiteSpace(SqliteDatabasePath))
            {
                SqliteDatabasePath = Path.Combine(BaseAppDataPath, "medias.db");
            }
        }

        public async Task LoadAsync()
        {
            await Task.Run(LoadSettings);
            InitializeDefaults();
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
                return;
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
            if (localDevConfig != null)
            {
                return localDevConfig;
            }

            // 2. Check in AppData (User specific)
            var appDataPath = Path.Combine(BaseAppDataPath, ConfigFileName);
            if (File.Exists(appDataPath))
            {
                return appDataPath;
            }

            // 3. Check in BaseDirectory
            var baseConfig = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
            if (File.Exists(baseConfig))
            {
                return baseConfig;
            }

            return null;
        }

        private string GetWritableConfigPath()
        {
            EnsureDirectoryExists(BaseAppDataPath);
            return Path.Combine(BaseAppDataPath, ConfigFileName);
        }

        private void EnsureDirectoryExists(string? path)
        {
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
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

