using Japlayer.Converters;
using Japlayer.Helpers;
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

        [TestMethod]
        public void PathToImageSourceConverter_WhenValueIsNull_ReturnsNull()
        {
            // Arrange
            var converter = new PathToImageSourceConverter();

            // Act
            var result = converter.Convert(null!, typeof(object), null!, string.Empty);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void PathToImageSourceConverter_WhenValueIsWhitespace_ReturnsNull()
        {
            // Arrange
            var converter = new PathToImageSourceConverter();

            // Act
            var result = converter.Convert("   ", typeof(object), null!, string.Empty);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void PathToImageSourceConverter_WhenValueIsRelativePath_ReturnsNull()
        {
            // Arrange
            var converter = new PathToImageSourceConverter();

            // Act
            var result = converter.Convert("relative/poster.jpg", typeof(object), null!, string.Empty);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        [ExpectedException(typeof(NotImplementedException))]
        public void PathToImageSourceConverter_ConvertBack_ThrowsNotImplementedException()
        {
            // Arrange
            var converter = new PathToImageSourceConverter();

            // Act
            converter.ConvertBack(null!, typeof(string), null!, string.Empty);
        }

        [TestMethod]
        public void DecodedImageCache_TryParseDecodeSpecification_ParsesValidInputs()
        {
            Assert.IsTrue(DecodedImageCache.TryParseDecodeSpecification(null, out var nullAxis, out var nullSize));
            Assert.AreEqual(DecodedImageAxis.Original, nullAxis);
            Assert.AreEqual(0, nullSize);

            Assert.IsTrue(DecodedImageCache.TryParseDecodeSpecification(120.0, out var doubleAxis, out var doubleSize));
            Assert.AreEqual(DecodedImageAxis.Height, doubleAxis);
            Assert.AreEqual(120, doubleSize);

            Assert.IsTrue(DecodedImageCache.TryParseDecodeSpecification(120, out var intAxis, out var intSize));
            Assert.AreEqual(DecodedImageAxis.Height, intAxis);
            Assert.AreEqual(120, intSize);

            Assert.IsTrue(DecodedImageCache.TryParseDecodeSpecification("120", out var bareAxis, out var bareSize));
            Assert.AreEqual(DecodedImageAxis.Height, bareAxis);
            Assert.AreEqual(120, bareSize);

            Assert.IsTrue(DecodedImageCache.TryParseDecodeSpecification("h:120", out var shortHeightAxis, out var shortHeightSize));
            Assert.AreEqual(DecodedImageAxis.Height, shortHeightAxis);
            Assert.AreEqual(120, shortHeightSize);

            Assert.IsTrue(DecodedImageCache.TryParseDecodeSpecification("height:120", out var longHeightAxis, out var longHeightSize));
            Assert.AreEqual(DecodedImageAxis.Height, longHeightAxis);
            Assert.AreEqual(120, longHeightSize);

            Assert.IsTrue(DecodedImageCache.TryParseDecodeSpecification("w:100", out var shortWidthAxis, out var shortWidthSize));
            Assert.AreEqual(DecodedImageAxis.Width, shortWidthAxis);
            Assert.AreEqual(100, shortWidthSize);

            Assert.IsTrue(DecodedImageCache.TryParseDecodeSpecification("width:100", out var longWidthAxis, out var longWidthSize));
            Assert.AreEqual(DecodedImageAxis.Width, longWidthAxis);
            Assert.AreEqual(100, longWidthSize);
        }

        [TestMethod]
        public void DecodedImageCache_TryParseDecodeSpecification_RejectsInvalidInputs()
        {
            object[] invalidParameters = [0, -5, double.NaN, double.PositiveInfinity, "w:0", "abc", string.Empty];
            foreach (var parameter in invalidParameters)
            {
                Assert.IsFalse(
                    DecodedImageCache.TryParseDecodeSpecification(parameter, out _, out _),
                    $"Expected '{parameter}' to fail parsing.");
            }
        }

        [TestMethod]
        public void DecodedImageCacheKey_UsesOrdinalIgnoreCasePathAndDistinguishesAxisAndSize()
        {
            var upperKey = new DecodedImageCacheKey(@"C:\Images\Gallery\Photo.JPG", DecodedImageAxis.Height, 120);
            var lowerKey = new DecodedImageCacheKey(@"c:\images\gallery\photo.jpg", DecodedImageAxis.Height, 120);
            var widthKey = new DecodedImageCacheKey(@"C:\Images\Gallery\Photo.JPG", DecodedImageAxis.Width, 120);
            var largerKey = new DecodedImageCacheKey(@"C:\Images\Gallery\Photo.JPG", DecodedImageAxis.Height, 660);

            Assert.AreEqual(upperKey, lowerKey);
            Assert.AreEqual(upperKey.GetHashCode(), lowerKey.GetHashCode());
            Assert.AreNotEqual(upperKey, widthKey);
            Assert.AreNotEqual(upperKey, largerKey);
        }

        [TestMethod]
        public void DecodedImageCache_TryCreateAbsoluteUriAndIsThumbnailBucket_BehaveAsExpected()
        {
            Assert.IsFalse(DecodedImageCache.TryCreateAbsoluteUri(null, out _, out _));
            Assert.IsFalse(DecodedImageCache.TryCreateAbsoluteUri("   ", out _, out _));
            Assert.IsFalse(DecodedImageCache.TryCreateAbsoluteUri("relative/poster.jpg", out _, out _));

            Assert.IsTrue(DecodedImageCache.TryCreateAbsoluteUri(@"C:\Images\Sub\..\poster.jpg", out var fileUri, out var normalizedFilePath));
            Assert.AreEqual(@"C:\Images\poster.jpg", normalizedFilePath);
            Assert.IsTrue(fileUri.IsAbsoluteUri);

            Assert.IsTrue(DecodedImageCache.TryCreateAbsoluteUri("ms-appx:///Assets/NoCover.png", out var appxUri, out var normalizedAppxPath));
            Assert.AreEqual("ms-appx:///Assets/NoCover.png", normalizedAppxPath);
            Assert.IsTrue(appxUri.IsAbsoluteUri);

            Assert.IsFalse(DecodedImageCache.IsThumbnailBucket(0));
            Assert.IsTrue(DecodedImageCache.IsThumbnailBucket(120));
            Assert.IsTrue(DecodedImageCache.IsThumbnailBucket(240));
            Assert.IsFalse(DecodedImageCache.IsThumbnailBucket(241));
            Assert.IsFalse(DecodedImageCache.IsThumbnailBucket(660));
        }
    }
}
