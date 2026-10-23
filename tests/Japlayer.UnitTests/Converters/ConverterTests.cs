using Japlayer.Converters;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.Converters
{
    [TestClass]
    public class ConverterTests
    {
        [TestMethod]
        public void StringToVisibilityConverter_WhenValueIsNull_ReturnsCollapsed()
        {
            // Arrange
            var converter = new StringToVisibilityConverter();

            // Act
            var result = converter.Convert(null!, typeof(Visibility), null!, string.Empty);

            // Assert
            Assert.AreEqual(Visibility.Collapsed, result);
        }

        [TestMethod]
        public void StringToVisibilityConverter_WhenValueIsEmptyString_ReturnsCollapsed()
        {
            // Arrange
            var converter = new StringToVisibilityConverter();

            // Act
            var result = converter.Convert(string.Empty, typeof(Visibility), null!, string.Empty);

            // Assert
            Assert.AreEqual(Visibility.Collapsed, result);
        }

        [TestMethod]
        public void StringToVisibilityConverter_WhenValueIsWhitespace_ReturnsCollapsed()
        {
            // Arrange
            var converter = new StringToVisibilityConverter();

            // Act
            var result = converter.Convert("   ", typeof(Visibility), null!, string.Empty);

            // Assert
            Assert.AreEqual(Visibility.Collapsed, result);
        }

        [TestMethod]
        public void StringToVisibilityConverter_WhenValueHasContent_ReturnsVisible()
        {
            // Arrange
            var converter = new StringToVisibilityConverter();

            // Act
            var result = converter.Convert("Hello World", typeof(Visibility), null!, string.Empty);

            // Assert
            Assert.AreEqual(Visibility.Visible, result);
        }

        [TestMethod]
        [ExpectedException(typeof(NotImplementedException))]
        public void StringToVisibilityConverter_ConvertBack_ThrowsNotImplementedException()
        {
            // Arrange
            var converter = new StringToVisibilityConverter();

            // Act
            converter.ConvertBack(Visibility.Visible, typeof(string), null!, string.Empty);
        }

        [TestMethod]
        public void PathToMediaSourceConverter_WhenValueIsNull_ReturnsNull()
        {
            // Arrange
            var converter = new PathToMediaSourceConverter();

            // Act
            var result = converter.Convert(null!, typeof(object), null!, string.Empty);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void PathToMediaSourceConverter_WhenValueIsWhitespace_ReturnsNull()
        {
            // Arrange
            var converter = new PathToMediaSourceConverter();

            // Act
            var result = converter.Convert("   ", typeof(object), null!, string.Empty);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        [ExpectedException(typeof(NotImplementedException))]
        public void PathToMediaSourceConverter_ConvertBack_ThrowsNotImplementedException()
        {
            // Arrange
            var converter = new PathToMediaSourceConverter();

            // Act
            converter.ConvertBack(null!, typeof(string), null!, string.Empty);
        }
    }
}
