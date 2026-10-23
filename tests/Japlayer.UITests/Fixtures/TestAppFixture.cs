using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Japlayer.Data.Context;
using Japlayer.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Japlayer.UITests.Fixtures
{
    public class TestAppFixture : IDisposable
    {
        private sealed class TestAppSettings
        {
            public string? ImagePath { get; set; }
            public string? SqliteDatabasePath { get; set; }
        }

        public string TempDirectory { get; }
        public string DatabasePath { get; }
        public string ImageDirectory { get; }
        public string SettingsFilePath { get; }
        public string DummyVideoPath { get; }

        public Application? App { get; private set; }
        public UIA3Automation? Automation { get; private set; }
        public Window? MainWindow { get; private set; }

        public TestAppFixture()
        {
            var uniqueId = Guid.NewGuid().ToString("N");
            TempDirectory = Path.Combine(Path.GetTempPath(), "JaplayerUITests", uniqueId);
            Directory.CreateDirectory(TempDirectory);

            ImageDirectory = Path.Combine(TempDirectory, "imgs");
            Directory.CreateDirectory(ImageDirectory);

            DatabasePath = Path.Combine(TempDirectory, "test_medias.db");
            DummyVideoPath = Path.Combine(TempDirectory, "dummy_video.mp4");
            File.WriteAllBytes(DummyVideoPath, new byte[1024]); // 1KB placeholder file

            SettingsFilePath = Path.Combine(TempDirectory, "japlayer.test.settings.json");

            SeedDatabase();
            WriteSettingsFile();
        }

        private void SeedDatabase()
        {
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseSqlite($"Data Source={DatabasePath}")
                .Options;

            using var context = new DatabaseContext(options);
            context.Database.EnsureCreated();

            // Create solid 1x1 test image files
            var cover1Path = Path.Combine(ImageDirectory, "test001_cover.jpg");
            var thumb1Path = Path.Combine(ImageDirectory, "test001_t10.jpg");
            var thumb2Path = Path.Combine(ImageDirectory, "test001_t30.jpg");
            var gallery1Path = Path.Combine(ImageDirectory, "test001_gallery1.jpg");

            File.WriteAllBytes(cover1Path, CreateMinimalJpegBytes());
            File.WriteAllBytes(thumb1Path, CreateMinimalJpegBytes());
            File.WriteAllBytes(thumb2Path, CreateMinimalJpegBytes());
            File.WriteAllBytes(gallery1Path, CreateMinimalJpegBytes());

            // Shared tags and genres (to satisfy unique constraint)
            var vrTag = new UserTag { Name = "VR" };
            var favoritesTag = new UserTag { Name = "Favorites" };
            var twoDTag = new UserTag { Name = "2D" };
            var actionGenre = new MediaGenre { Name = "Action" };
            var sciFiGenre = new MediaGenre { Name = "Sci-Fi" };
            var documentaryGenre = new MediaGenre { Name = "Documentary" };

            // 1. TEST-001
            var media1 = new Media { MediaId = "TEST-001" };
            media1.MediaLocations.Add(new MediaLocation
            {
                MediaId = "TEST-001",
                Scene = 1,
                Hostname = "localhost",
                Path = DummyVideoPath,
                CreatedAt = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc)
            });
            media1.MediaImages.Add(new MediaImage { MediaId = "TEST-001", Filepath = "test001_cover.jpg", Url = "https://example.com/cover1" });
            media1.MediaMetadata.Add(new MediaMetadata
            {
                MediaId = "TEST-001",
                MetadataUrl = "https://example.com/meta1",
                Title = "Alpha Cyber Adventure",
                ContentId = "CID-001",
                Cover = "test001_cover.jpg",
                ReleaseDate = new DateOnly(2024, 5, 1),
                RuntimeMinutes = 90
            });
            media1.UserTags.Add(vrTag);
            media1.UserTags.Add(favoritesTag);
            media1.Genres.Add(sciFiGenre);
            media1.Genres.Add(actionGenre);
            media1.MediaThumbnails.Add(new MediaThumbnail { MediaId = "TEST-001", Scene = 1, Timestamp = 10, Path = "test001_t10.jpg", OriginalHostname = "localhost", OriginalPath = "/tmp/t10.jpg" });
            media1.MediaThumbnails.Add(new MediaThumbnail { MediaId = "TEST-001", Scene = 1, Timestamp = 30, Path = "test001_t30.jpg", OriginalHostname = "localhost", OriginalPath = "/tmp/t30.jpg" });
            media1.MediaHighlights.Add(new MediaHighlight { MediaId = "TEST-001", Scene = 1, Timestamp = 10 });
            media1.MediaHighlights.Add(new MediaHighlight { MediaId = "TEST-001", Scene = 1, Timestamp = 30 });

            // 2. TEST-002
            var media2 = new Media { MediaId = "TEST-002" };
            media2.MediaLocations.Add(new MediaLocation
            {
                MediaId = "TEST-002",
                Scene = 1,
                Hostname = "localhost",
                Path = DummyVideoPath,
                CreatedAt = new DateTime(2023, 6, 1, 10, 0, 0, DateTimeKind.Utc)
            });
            media2.MediaMetadata.Add(new MediaMetadata
            {
                MediaId = "TEST-002",
                MetadataUrl = "https://example.com/meta2",
                Title = "Beta Deep Ocean",
                ContentId = "CID-002",
                ReleaseDate = new DateOnly(2023, 8, 15),
                RuntimeMinutes = 60
            });
            media2.UserTags.Add(vrTag);
            media2.Genres.Add(documentaryGenre);

            // 3. TEST-003
            var media3 = new Media { MediaId = "TEST-003" };
            media3.MediaLocations.Add(new MediaLocation
            {
                MediaId = "TEST-003",
                Scene = 1,
                Hostname = "localhost",
                Path = DummyVideoPath,
                CreatedAt = new DateTime(2022, 1, 1, 10, 0, 0, DateTimeKind.Utc)
            });
            media3.MediaMetadata.Add(new MediaMetadata
            {
                MediaId = "TEST-003",
                MetadataUrl = "https://example.com/meta3",
                Title = "Gamma Mountain Trek",
                ContentId = "CID-003",
                ReleaseDate = new DateOnly(2022, 1, 20),
                RuntimeMinutes = 110
            });
            media3.UserTags.Add(twoDTag);
            media3.Genres.Add(actionGenre);
            media3.MediaHighlights.Add(new MediaHighlight { MediaId = "TEST-003", Scene = 1, Timestamp = 25 });

            context.Media.AddRange(media1, media2, media3);
            context.SaveChanges();
        }

        private void WriteSettingsFile()
        {
            var settings = new TestAppSettings
            {
                ImagePath = ImageDirectory,
                SqliteDatabasePath = DatabasePath
            };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }

        public void StartApp()
        {
            var solutionRoot = FindSolutionRoot();
            var appExePath = Path.Combine(solutionRoot, "Japlayer.WinUI", "bin", "x64", "Debug", "net8.0-windows10.0.19041.0", "Japlayer.exe");

            if (!File.Exists(appExePath))
            {
                // Fallback to bin\Debug if built without explicit x64 subfolder
                appExePath = Path.Combine(solutionRoot, "Japlayer.WinUI", "bin", "Debug", "net8.0-windows10.0.19041.0", "Japlayer.exe");
            }

            if (!File.Exists(appExePath))
            {
                throw new FileNotFoundException($"Japlayer.exe could not be found at '{appExePath}'. Ensure Japlayer.WinUI is built before running UI tests.");
            }

            Automation = new UIA3Automation();

            var processStartInfo = new ProcessStartInfo
            {
                FileName = appExePath,
                Arguments = $"--settings \"{SettingsFilePath}\"",
                UseShellExecute = false
            };

            App = Application.Launch(processStartInfo);

            // Wait for main window to appear
            MainWindow = Retry.WhileNull(
                () => FindMainWindow(),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(250)).Result;

            if (MainWindow == null)
            {
                throw new TimeoutException("Main window of Japlayer could not be obtained within timeout.");
            }

            // Bring window to focus
            MainWindow.SetForeground();
            MainWindow.Focus();
            Wait.UntilInputIsProcessed();
        }

        private Window? FindMainWindow()
        {
            if (App == null || Automation == null)
            {
                return null;
            }

            var desktop = Automation.GetDesktop();
            var processElements = desktop.FindAllChildren(conditionFactory =>
                conditionFactory.ByProcessId(App.ProcessId));

            // Prefer window that contains AppTitleTextBlock or MediaGrid
            foreach (var element in processElements)
            {
                if (element.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("AppTitleTextBlock")) != null ||
                    element.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("MediaGrid")) != null)
                {
                    return element.AsWindow();
                }
            }

            // Fallback: window with width > 100
            var largeWindow = processElements.FirstOrDefault(element => element.BoundingRectangle.Width > 100);
            if (largeWindow != null)
            {
                return largeWindow.AsWindow();
            }

            return null;
        }

        private static string FindSolutionRoot()
        {
            var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            while (currentDirectory != null)
            {
                if (File.Exists(Path.Combine(currentDirectory.FullName, "Japlayer.slnx")) ||
                    File.Exists(Path.Combine(currentDirectory.FullName, "Japlayer.sln")))
                {
                    return currentDirectory.FullName;
                }
                currentDirectory = currentDirectory.Parent;
            }
            return AppContext.BaseDirectory;
        }

        private static byte[] CreateMinimalJpegBytes()
        {
            // Standard valid 1x1 baseline JPEG image bytes
            return Convert.FromBase64String(
                "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD8qqKKKAP/2Q==");
        }

        public void Dispose()
        {
            try
            {
                MainWindow?.Close();
            }
            catch { }

            try
            {
                App?.Close();
                App?.Dispose();
            }
            catch { }

            try
            {
                Automation?.Dispose();
            }
            catch { }

            // Release SQLite connection pools before attempting directory deletion
            SqliteConnection.ClearAllPools();
            Thread.Sleep(200);

            try
            {
                if (Directory.Exists(TempDirectory))
                {
                    Directory.Delete(TempDirectory, true);
                }
            }
            catch { }
        }
    }
}
