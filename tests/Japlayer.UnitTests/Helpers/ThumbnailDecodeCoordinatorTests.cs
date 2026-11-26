using Japlayer.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.Helpers
{
    [TestClass]
    public class ThumbnailDecodeCoordinatorTests
    {
        [TestMethod]
        public void RequestOrCoalesce_WithinInterval_CoalescesIntermediateThumbnailsToLatest()
        {
            var coordinator = new ThumbnailDecodeCoordinator();

            // First request dispatches immediately
            Assert.IsTrue(coordinator.RequestOrCoalesce(@"C:\Images\TEST-001\thumbs\01.jpg", nowTimestampMs: 1000));

            // Rapid pointer moves within 25 ms window coalesce to latest path
            Assert.IsFalse(coordinator.RequestOrCoalesce(@"C:\Images\TEST-001\thumbs\02.jpg", nowTimestampMs: 1005));
            Assert.IsFalse(coordinator.RequestOrCoalesce(@"C:\Images\TEST-001\thumbs\03.jpg", nowTimestampMs: 1012));
            Assert.IsFalse(coordinator.RequestOrCoalesce(@"C:\Images\TEST-001\thumbs\04.jpg", nowTimestampMs: 1020));
            Assert.AreEqual(@"C:\Images\TEST-001\thumbs\04.jpg", coordinator.PendingPath);

            // Timer flush dispatches only the latest coalesced path
            var nextPath = coordinator.ConsumePending(nowTimestampMs: 1025, currentSourcePath: @"C:\Images\TEST-001\thumbs\04.jpg");
            Assert.AreEqual(@"C:\Images\TEST-001\thumbs\04.jpg", nextPath);
            Assert.IsNull(coordinator.PendingPath);

            // Request after >= 25 ms dispatches immediately again
            Assert.IsTrue(coordinator.RequestOrCoalesce(@"C:\Images\TEST-001\thumbs\05.jpg", nowTimestampMs: 1050));
        }

        [TestMethod]
        public void Reset_WhenPointerExits_CancelsPendingCoalescedDecode()
        {
            var coordinator = new ThumbnailDecodeCoordinator();

            Assert.IsTrue(coordinator.RequestOrCoalesce(@"C:\Images\TEST-001\thumbs\01.jpg", nowTimestampMs: 1000));
            Assert.IsFalse(coordinator.RequestOrCoalesce(@"C:\Images\TEST-001\thumbs\02.jpg", nowTimestampMs: 1005));

            coordinator.Reset();

            Assert.IsNull(coordinator.PendingPath);
            var nextPath = coordinator.ConsumePending(nowTimestampMs: 1030, currentSourcePath: @"C:\Images\TEST-001\cover.jpg");
            Assert.IsNull(nextPath);
        }

        [TestMethod]
        public void ConsumePending_WhenSourceMovedAway_DoesNotReturnStaleThumbnail()
        {
            var coordinator = new ThumbnailDecodeCoordinator();

            Assert.IsTrue(coordinator.RequestOrCoalesce(@"C:\Images\TEST-001\thumbs\01.jpg", nowTimestampMs: 1000));
            Assert.IsFalse(coordinator.RequestOrCoalesce(@"C:\Images\TEST-001\thumbs\02.jpg", nowTimestampMs: 1005));

            var nextPath = coordinator.ConsumePending(nowTimestampMs: 1030, currentSourcePath: @"C:\Images\TEST-001\cover.jpg");
            Assert.IsNull(nextPath);
        }

        [TestMethod]
        public void SelectPrewarmIndices_ReturnsEvenlySpacedIndicesAcrossFullRange()
        {
            var indices = ThumbnailDecodeCoordinator.SelectPrewarmIndices(totalCount: 101, maxKeyframes: 12);

            Assert.AreEqual(12, indices.Count);
            Assert.AreEqual(0, indices[0]);
            Assert.AreEqual(100, indices[11]);

            for (var index = 1; index < indices.Count; index++)
            {
                Assert.IsTrue(indices[index] > indices[index - 1]);
            }
        }

        [TestMethod]
        public void SelectPrewarmIndices_WhenFewerThanMax_ReturnsAllIndices()
        {
            var indices = ThumbnailDecodeCoordinator.SelectPrewarmIndices(totalCount: 5, maxKeyframes: 12);

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, new List<int>(indices));
        }

        [TestMethod]
        public void SelectPrewarmIndices_WhenEmpty_ReturnsEmpty()
        {
            var indices = ThumbnailDecodeCoordinator.SelectPrewarmIndices(totalCount: 0, maxKeyframes: 12);

            Assert.AreEqual(0, indices.Count);
        }

        [TestMethod]
        [DataRow(0.0, 360)]
        [DataRow(double.NaN, 360)]
        [DataRow(300.0, 360)]
        [DataRow(400.0, 480)]
        [DataRow(1000.0, 720)]
        public void CalculateDecodePixelWidth_DerivesFromCardWidthWithBounds(double actualWidth, int expectedWidth) => Assert.AreEqual(expectedWidth, ThumbnailDecodeCoordinator.CalculateDecodePixelWidth(actualWidth));

        [TestMethod]
        public void BoundedLruCache_EvictsLeastRecentlyUsedAndPromotesHits()
        {
            var cache = new BoundedLruCache<string, int>(capacity: 2, StringComparer.OrdinalIgnoreCase);

            cache.Set("a", 1);
            cache.Set("b", 2);

            // Access "a" so "b" becomes least recently used
            Assert.IsTrue(cache.TryGetValue("a", out var valueA));
            Assert.AreEqual(1, valueA);

            // Adding "c" evicts "b", keeping "a" and "c"
            cache.Set("c", 3);

            Assert.AreEqual(2, cache.Count);
            Assert.IsFalse(cache.TryGetValue("b", out _));
            Assert.IsTrue(cache.TryGetValue("a", out _));
            Assert.IsTrue(cache.TryGetValue("c", out var valueC));
            Assert.AreEqual(3, valueC);
        }

        [TestMethod]
        [DataRow(50.0, 0.0, false, 0.0)]
        [DataRow(50.0, -10.0, false, 0.0)]
        [DataRow(50.0, double.NaN, false, 0.0)]
        [DataRow(double.NaN, 100.0, false, 0.0)]
        [DataRow(50.0, 200.0, true, 0.25)]
        [DataRow(-20.0, 200.0, true, 0.0)]
        [DataRow(250.0, 200.0, true, 1.0)]
        public void TryCalculateScrubPercent_GuardsInvalidDimensionsAndClampsRange(
            double pointerX,
            double actualWidth,
            bool expectedResult,
            double expectedPercent)
        {
            var result = ThumbnailDecodeCoordinator.TryCalculateScrubPercent(pointerX, actualWidth, out var percent);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedPercent, percent, 0.0001);
        }

        [TestMethod]
        [DataRow(@"C:\Images\TEST-001\thumbs\01.jpg", true)]
        [DataRow("file:///C:/Images/TEST-001/thumbs/01.jpg", true)]
        [DataRow(@"C:\Images\TEST-001\cover.jpg", false)]
        [DataRow("ms-appx:///Assets/NoCover.png", false)]
        public void IsThumbnailPath_DetectsThumbnailDirectoryAcrossPathSeparators(string path, bool expected) => Assert.AreEqual(expected, ThumbnailDecodeCoordinator.IsThumbnailPath(path));
    }
}
