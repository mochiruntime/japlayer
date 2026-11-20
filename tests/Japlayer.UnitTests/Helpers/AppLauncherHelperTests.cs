#nullable enable
using System.Threading.Tasks;
using Japlayer.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.Helpers
{
    [TestClass]
    public class AppLauncherHelperTests
    {
        [TestMethod]
        public async Task LoadForFileAsync_WhenFilePathHasNoExtension_ReturnsNull()
        {
            // Arrange
            var filePathWithoutExtension = @"C:\NonExistentPath\somefile";

            // Act
            var result = await AppHandlers.LoadForFileAsync(filePathWithoutExtension);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task LoadForFileAsync_WhenFileDoesNotExistOnDisk_DoesNotThrowAndLoadsHandlers()
        {
            // Arrange - A path on a non-existent drive/file that would fail if accessed via StorageFile.GetFileFromPathAsync
            var nonExistentFilePath = @"Z:\NonExistentDrive\Media\test_video_file.mp4";

            // Act
            var result = await AppHandlers.LoadForFileAsync(nonExistentFilePath);

            // Assert - Handlers should resolve successfully based purely on file extension without accessing the disk
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task LoadForFileAsync_WhenCalledMultipleTimesWithSameExtension_ReturnsCachedInstance()
        {
            // Arrange
            var firstFilePath = @"C:\SamplePath\movie.mkv";
            var secondFilePathWithDifferentCasing = @"D:\AnotherPath\different_movie.MKV";

            // Act
            var firstResult = await AppHandlers.LoadForFileAsync(firstFilePath);
            var secondResult = await AppHandlers.LoadForFileAsync(secondFilePathWithDifferentCasing);

            // Assert
            Assert.IsNotNull(firstResult);
            Assert.AreSame(firstResult, secondResult);
        }

        [TestMethod]
        public void ParseCommand_WhenCommandHasQuotesAndPercentOne_ReplacesPercentOneWithQuotedFilePath()
        {
            // Arrange
            var command = "\"C:\\Program Files\\Player\\player.exe\" \"%1\"";
            var filePath = @"D:\Media\test.mp4";

            // Act
            var (executable, arguments) = Win32FileHandler.ParseCommand(command, filePath);

            // Assert
            Assert.AreEqual(@"C:\Program Files\Player\player.exe", executable);
            Assert.AreEqual("\"\"D:\\Media\\test.mp4\"\"", arguments);
        }

        [TestMethod]
        public void ParseCommand_WhenCommandHasNoPercentOne_AppendsQuotedFilePath()
        {
            // Arrange
            var command = "\"C:\\Program Files\\Player\\player.exe\" /play";
            var filePath = @"D:\Media\test.mp4";

            // Act
            var (executable, arguments) = Win32FileHandler.ParseCommand(command, filePath);

            // Assert
            Assert.AreEqual(@"C:\Program Files\Player\player.exe", executable);
            Assert.AreEqual("/play \"D:\\Media\\test.mp4\"", arguments);
        }
    }
}
