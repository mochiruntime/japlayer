using System.Text.Json;
using Japlayer.Models;
using Japlayer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.Services
{
    [TestClass]
    public class SettingsServiceTests
    {
        private string _temporaryDirectory = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), "JaplayerSettingsTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, true);
            }
        }

        [TestMethod]
        public void Constructor_WithCustomSettingsFile_LoadsSettingsFromCustomFile()
        {
            // Arrange
            var customSettingsPath = Path.Combine(_temporaryDirectory, "custom.settings.json");
            var expectedImagePath = @"C:\TestImages";
            var expectedDatabasePath = @"C:\TestDatabase\medias.db";

            var settingsModel = new AppSettings
            {
                ImagePath = expectedImagePath,
                SqliteDatabasePath = expectedDatabasePath
            };
            File.WriteAllText(customSettingsPath, JsonSerializer.Serialize(settingsModel));

            // Act
            var settingsService = new SettingsService(customSettingsPath);

            // Assert
            Assert.AreEqual(expectedImagePath, settingsService.ImagePath);
            Assert.AreEqual(expectedDatabasePath, settingsService.SqliteDatabasePath);
        }

        [TestMethod]
        public void Constructor_WhenNoFileExists_InitializesWithDefaultPaths()
        {
            // Act
            var nonExistentPath = Path.Combine(_temporaryDirectory, "nonexistent.json");
            var settingsService = new SettingsService(nonExistentPath);

            // Assert
            Assert.IsFalse(string.IsNullOrWhiteSpace(settingsService.ImagePath));
            Assert.IsFalse(string.IsNullOrWhiteSpace(settingsService.SqliteDatabasePath));
            Assert.IsTrue(settingsService.SqliteDatabasePath.EndsWith("medias.db", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public async Task SaveAsync_PersistsSettingsToFile()
        {
            // Arrange
            var customSettingsPath = Path.Combine(_temporaryDirectory, "save_test.json");
            var expectedImagePath = @"D:\Media\Images";
            var expectedDatabasePath = @"D:\Media\database.db";

            var settingsService = new SettingsService(customSettingsPath)
            {
                ImagePath = expectedImagePath,
                SqliteDatabasePath = expectedDatabasePath
            };

            // Act
            await settingsService.SaveAsync();

            // Assert
            Assert.IsTrue(File.Exists(customSettingsPath), "Custom settings file should have been created.");
            var savedJson = await File.ReadAllTextAsync(customSettingsPath);
            var savedSettings = JsonSerializer.Deserialize<AppSettings>(savedJson);
            Assert.IsNotNull(savedSettings);
            Assert.AreEqual(expectedImagePath, savedSettings.ImagePath);
            Assert.AreEqual(expectedDatabasePath, savedSettings.SqliteDatabasePath);
        }
    }
}
