#nullable enable
using Japlayer.Controls;
using Japlayer.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Japlayer.UnitTests.Helpers
{
    [TestClass]
    public class LayoutAndWrapPanelTests
    {
        [TestMethod]
        public void CeilingToLayoutPixel_At125PercentScale_RoundsHalfPixelUpToNextPhysicalPixel()
        {
            // Two 0.8-DIP pixel-aligned items (68 px = 54.4 DIPs, 53 px = 42.4 DIPs) + 6 DIP spacing (7.5 px) = 102.8 DIPs (128.5 px)
            const double scale = 1.25d;
            var itemSizes = new[] { new Size(54.4d, 26d), new Size(42.4d, 26d) };

            var layout = WrapPanel.ComputeLayout(itemSizes, double.PositiveInfinity, horizontalSpacing: 6d, verticalSpacing: 6d);
            var ceiledWidth = LayoutHelper.CeilingToLayoutPixel(layout.MeasuredSize.Width, scale);

            // 102.8 * 1.25 = 128.5 -> ceil to 129 px = 103.2 DIPs (never 102.4 DIPs / 128 px)
            Assert.AreEqual(102.8d, layout.MeasuredSize.Width, 1e-4d);
            Assert.AreEqual(103.2d, ceiledWidth, 1e-9d);
            Assert.IsTrue(ceiledWidth >= layout.MeasuredSize.Width);
        }

        [TestMethod]
        [DataRow(1.00d)]
        [DataRow(1.25d)]
        [DataRow(1.50d)]
        [DataRow(1.75d)]
        public void MeasureAndArrange_AcrossScales_PreserveRowCountWithoutOverflowRow(double scale)
        {
            var itemSets = new[]
            {
                new[] { new Size(54.4d, 26d), new Size(42.4d, 26d) },
                [new Size(48.0d, 26d), new Size(42.4d, 26d)],
                [new Size(80.0d, 26d), new Size(90.0d, 26d), new Size(70.0d, 26d), new Size(65.0d, 26d), new Size(75.0d, 26d)]
            };

            const double horizontalSpacing = 6d;
            const double verticalSpacing = 6d;
            var tolerance = LayoutHelper.GetLayoutRoundingTolerance(scale);

            foreach (var items in itemSets)
            {
                foreach (var availableWidth in new[] { double.PositiveInfinity, 180d, 250d })
                {
                    var measured = WrapPanel.ComputeLayout(items, availableWidth, horizontalSpacing, verticalSpacing);
                    var measuredRowCount = CountDistinctRows(measured.ItemBounds);

                    // Simulate WinUI UseLayoutRounding on the measured width (with CeilingToLayoutPixel applied)
                    var ceiledWidth = LayoutHelper.CeilingToLayoutPixel(measured.MeasuredSize.Width, scale);
                    var winUiRoundedWidth = Math.Round(ceiledWidth * scale) / scale;

                    var arranged = WrapPanel.ComputeLayout(items, winUiRoundedWidth, horizontalSpacing, verticalSpacing, tolerance);
                    var arrangedRowCount = CountDistinctRows(arranged.ItemBounds);

                    Assert.AreEqual(
                        measuredRowCount,
                        arrangedRowCount,
                        $"Row count mismatch at scale {scale} with availableWidth={availableWidth}, winUiRoundedWidth={winUiRoundedWidth}.");

                    // Also verify worst-case 0.5-px shrink in parent rounding is absorbed by tolerance
                    var shrunkParentWidth = measured.MeasuredSize.Width - (0.5d / scale);
                    if (shrunkParentWidth > 0d)
                    {
                        var arrangedShrunk = WrapPanel.ComputeLayout(items, shrunkParentWidth, horizontalSpacing, verticalSpacing, tolerance);
                        Assert.AreEqual(
                            measuredRowCount,
                            CountDistinctRows(arrangedShrunk.ItemBounds),
                            $"Row count mismatch after 0.5px parent shrink at scale {scale}.");
                    }
                }
            }
        }

        [TestMethod]
        public void ComputeLayout_WhenFinalWidthIsNarrowerThanRowBeyondTolerance_WrapsToNextRow()
        {
            const double scale = 1.25d;
            var itemSizes = new[] { new Size(54.4d, 26d), new Size(42.4d, 26d) };
            var tolerance = LayoutHelper.GetLayoutRoundingTolerance(scale);

            // Total width is 102.8 DIPs; make available width narrower by 2.0 DIPs (> tolerance ~0.401)
            var layout = WrapPanel.ComputeLayout(itemSizes, availableWidth: 100.8d, horizontalSpacing: 6d, verticalSpacing: 6d, wrapTolerance: tolerance);

            Assert.AreEqual(2, CountDistinctRows(layout.ItemBounds));
            Assert.AreEqual(0d, layout.ItemBounds[0].Y, 1e-9d);
            Assert.AreEqual(32d, layout.ItemBounds[1].Y, 1e-9d);
        }

        [TestMethod]
        public void ComputeLayout_AtExactBoundaryWithIeee754Drift_DoesNotSpuriouslyWrap()
        {
            const double scale = 1.25d;
            var itemSizes = new[] { new Size(54.4d, 26d), new Size(42.4d, 26d) };
            var tolerance = LayoutHelper.GetLayoutRoundingTolerance(scale);

            var layout = WrapPanel.ComputeLayout(itemSizes, availableWidth: 102.8d - 1e-12d, horizontalSpacing: 6d, verticalSpacing: 6d, wrapTolerance: tolerance);

            Assert.AreEqual(1, CountDistinctRows(layout.ItemBounds));
        }

        [TestMethod]
        public void LayoutHelper_EdgeCaseInputs_ReturnSafeValues()
        {
            Assert.AreEqual(0d, LayoutHelper.CeilingToLayoutPixel(0d, 1.25d));
            Assert.AreEqual(0d, LayoutHelper.CeilingToLayoutPixel(-5d, 1.25d));
            Assert.AreEqual(0d, LayoutHelper.CeilingToLayoutPixel(double.NaN, 1.25d));
            Assert.AreEqual(0d, LayoutHelper.CeilingToLayoutPixel(double.PositiveInfinity, 1.25d));

            // Invalid scale falls back to 1.0
            Assert.AreEqual(11d, LayoutHelper.CeilingToLayoutPixel(10.2d, 0d), 1e-9d);
            Assert.AreEqual(11d, LayoutHelper.CeilingToLayoutPixel(10.2d, double.NaN), 1e-9d);
            Assert.AreEqual(0.501d, LayoutHelper.GetLayoutRoundingTolerance(double.PositiveInfinity), 1e-9d);
            Assert.AreEqual(1.0d, LayoutHelper.GetRasterizationScale(null));
        }

        [TestMethod]
        public void ComputeCumulativeScrollTarget_AccumulatesRapidTicksAndClampsToBounds()
        {
            const double scrollableExtent = 1000d;

            // Tick 1 from rest (0) -> 300
            var firstTarget = LayoutHelper.ComputeCumulativeScrollTarget(
                currentOffset: 0d,
                pendingTargetOffset: null,
                scrollableExtent: scrollableExtent,
                wheelDelta: -120);
            Assert.AreEqual(300d, firstTarget, 1e-9d);

            // Tick 2 while animated currentOffset only reached 15 -> accumulates from 300 to 600 (not 315)
            var secondTarget = LayoutHelper.ComputeCumulativeScrollTarget(
                currentOffset: 15d,
                pendingTargetOffset: firstTarget,
                scrollableExtent: scrollableExtent,
                wheelDelta: -120);
            Assert.AreEqual(600d, secondTarget, 1e-9d);

            // Tick 3 and 4 clamp cleanly at scrollableExtent (1000)
            var thirdTarget = LayoutHelper.ComputeCumulativeScrollTarget(
                currentOffset: 40d,
                pendingTargetOffset: secondTarget,
                scrollableExtent: scrollableExtent,
                wheelDelta: -120);
            Assert.AreEqual(900d, thirdTarget, 1e-9d);

            var clampedEndTarget = LayoutHelper.ComputeCumulativeScrollTarget(
                currentOffset: 80d,
                pendingTargetOffset: thirdTarget,
                scrollableExtent: scrollableExtent,
                wheelDelta: -120);
            Assert.AreEqual(1000d, clampedEndTarget, 1e-9d);

            // Reverse direction mid-flight subtracts from pending target (1000 -> 700)
            var reversedTarget = LayoutHelper.ComputeCumulativeScrollTarget(
                currentOffset: 120d,
                pendingTargetOffset: clampedEndTarget,
                scrollableExtent: scrollableExtent,
                wheelDelta: 120);
            Assert.AreEqual(700d, reversedTarget, 1e-9d);

            // Scrolling past the start clamps cleanly at 0
            var clampedStartTarget = LayoutHelper.ComputeCumulativeScrollTarget(
                currentOffset: 50d,
                pendingTargetOffset: null,
                scrollableExtent: scrollableExtent,
                wheelDelta: 120);
            Assert.AreEqual(0d, clampedStartTarget, 1e-9d);

            // Non-scrollable extent always returns 0
            Assert.AreEqual(0d, LayoutHelper.ComputeCumulativeScrollTarget(0d, null, 0d, -120), 1e-9d);
        }

        private static int CountDistinctRows(IReadOnlyList<Rect> bounds)
        {
            var rows = new HashSet<double>();
            foreach (var rect in bounds)
            {
                rows.Add(Math.Round(rect.Y, 4));
            }

            return rows.Count;
        }
    }
}
