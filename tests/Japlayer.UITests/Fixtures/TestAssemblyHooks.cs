using Japlayer.Data.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UITests.Fixtures
{
    [TestClass]
    public static class TestAssemblyHooks
    {
        public static string? TemplateDirectory { get; private set; }
        public static string? TemplateDatabasePath { get; private set; }
        public static string? TemplateImageDirectory { get; private set; }
        public static string? TemplateDummyVideoPath { get; private set; }

        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext _)
        {
            var directory = Path.Combine(Path.GetTempPath(), "JaplayerUITests", $"Template_{Guid.NewGuid():N}");
            var imageDirectory = Path.Combine(directory, "imgs");
            Directory.CreateDirectory(imageDirectory);

            var databasePath = Path.Combine(directory, "template_medias.db");
            var dummyVideoPath = Path.Combine(directory, "dummy_video.mp4");
            File.WriteAllBytes(dummyVideoPath, new byte[1024]);

            TestAppFixture.WriteTestImages(imageDirectory);

            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            using (var context = new DatabaseContext(options))
            {
                context.Database.Migrate();
                TestAppFixture.SeedDefaultData(context, dummyVideoPath, extraSeedItemCount: 0);
                context.SaveChanges();
            }

            SqliteConnection.ClearAllPools();

            TemplateDirectory = directory;
            TemplateDatabasePath = databasePath;
            TemplateImageDirectory = imageDirectory;
            TemplateDummyVideoPath = dummyVideoPath;
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            SqliteConnection.ClearAllPools();

            if (string.IsNullOrEmpty(TemplateDirectory) || !Directory.Exists(TemplateDirectory))
            {
                return;
            }

            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Directory.Delete(TemplateDirectory, recursive: true);
                    return;
                }
                catch (IOException) when (attempt < 4)
                {
                    Thread.Sleep(50);
                }
                catch (UnauthorizedAccessException) when (attempt < 4)
                {
                    Thread.Sleep(50);
                }
                catch
                {
                    return;
                }
            }
        }
    }
}
