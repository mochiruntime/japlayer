using Japlayer.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.Helpers
{
    [TestClass]
    public class MediaPlaybackHelperTests
    {
        [TestMethod]
        public void GetNextHighlightTime_WhenHighlightsIsNull_ReturnsNull()
        {
            // Act
            var result = MediaPlaybackHelper.GetNextHighlightTime(10.0, null!);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetNextHighlightTime_WhenHighlightsIsEmpty_ReturnsNull()
        {
            // Act
            var result = MediaPlaybackHelper.GetNextHighlightTime(10.0, []);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetNextHighlightTime_WhenCurrentTimeIsBeforeAllHighlights_ReturnsFirstHighlight()
        {
            // Arrange
            var highlights = new List<int> { 20, 50, 100 };

            // Act
            var result = MediaPlaybackHelper.GetNextHighlightTime(10.0, highlights);

            // Assert
            Assert.AreEqual(20.0, result);
        }

        [TestMethod]
        public void GetNextHighlightTime_WhenCurrentTimeIsBetweenHighlights_ReturnsNextHighlight()
        {
            // Arrange
            var highlights = new List<int> { 20, 50, 100 };

            // Act
            var result = MediaPlaybackHelper.GetNextHighlightTime(25.0, highlights);

            // Assert
            Assert.AreEqual(50.0, result);
        }

        [TestMethod]
        public void GetNextHighlightTime_WhenCurrentTimeIsExactlyAtHighlight_AdvancesToNextBecauseOfBuffer()
        {
            // Arrange
            var highlights = new List<int> { 20, 50, 100 };

            // Act - timestamp > currentSeconds + 0.5
            var result = MediaPlaybackHelper.GetNextHighlightTime(20.0, highlights);

            // Assert
            Assert.AreEqual(50.0, result);
        }

        [TestMethod]
        public void GetNextHighlightTime_WhenCurrentTimeIsAfterAllHighlights_LoopsBackToFirstHighlight()
        {
            // Arrange
            var highlights = new List<int> { 20, 50, 100 };

            // Act
            var result = MediaPlaybackHelper.GetNextHighlightTime(105.0, highlights);

            // Assert
            Assert.AreEqual(20.0, result);
        }

        [TestMethod]
        public void GetNextHighlightTime_WhenHighlightsAreUnordered_OrdersCorrectly()
        {
            // Arrange
            var highlights = new List<int> { 100, 20, 50 };

            // Act
            var result = MediaPlaybackHelper.GetNextHighlightTime(25.0, highlights);

            // Assert
            Assert.AreEqual(50.0, result);
        }
    }
}
