using Japlayer.Data.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.Helpers
{
    [TestClass]
    public class ThumbnailFallbackSelectorTests
    {
        [TestMethod]
        public void SelectFallbackIndex_HandlesEmptyAndMultiItemCounts()
        {
            Assert.AreEqual(-1, ThumbnailFallbackSelector.SelectFallbackIndex(0));
            Assert.AreEqual(0, ThumbnailFallbackSelector.SelectFallbackIndex(1));
            Assert.AreEqual(2, ThumbnailFallbackSelector.SelectFallbackIndex(4));
            Assert.AreEqual(3, ThumbnailFallbackSelector.SelectFallbackIndex(5));
        }

        [TestMethod]
        public void SelectFallbackItem_ReturnsNullWhenEmptyAnd75PercentItemOtherwise()
        {
            Assert.IsNull(ThumbnailFallbackSelector.SelectFallbackItem<string>([]));
            Assert.AreEqual("c.jpg", ThumbnailFallbackSelector.SelectFallbackItem(["a.jpg", "b.jpg", "c.jpg", "d.jpg"]));
        }
    }
}
