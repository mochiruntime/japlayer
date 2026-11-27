#nullable enable
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Japlayer.Helpers
{
    public static class LayoutHelper
    {
        public static readonly DependencyProperty MatchViewportHeightOfProperty =
            DependencyProperty.RegisterAttached(
                "MatchViewportHeightOf",
                typeof(ScrollViewer),
                typeof(LayoutHelper),
                new PropertyMetadata(null, OnMatchViewportHeightOfChanged));

        private static readonly DependencyProperty ViewportSyncSubscriptionProperty =
            DependencyProperty.RegisterAttached(
                "ViewportSyncSubscription",
                typeof(ViewportSyncSubscription),
                typeof(LayoutHelper),
                new PropertyMetadata(null));

        public static ScrollViewer? GetMatchViewportHeightOf(DependencyObject element)
            => (ScrollViewer?)element.GetValue(MatchViewportHeightOfProperty);

        public static void SetMatchViewportHeightOf(DependencyObject element, ScrollViewer? value)
            => element.SetValue(MatchViewportHeightOfProperty, value);

        public static bool HasSignificantChange(ref double lastValue, double newValue, double threshold = 2.0)
        {
            if (Math.Abs(newValue - lastValue) <= threshold)
            {
                return false;
            }

            lastValue = newValue;
            return true;
        }

        public static void SetHeightIfChanged(FrameworkElement element, double targetHeight, double threshold = 1.0)
        {
            var flooredHeight = Math.Floor(targetHeight);
            if (flooredHeight > 0 && (double.IsNaN(element.Height) || Math.Abs(element.Height - flooredHeight) > threshold))
            {
                element.Height = flooredHeight;
            }
        }

        public static double GetRasterizationScale(UIElement? element)
        {
            var scale = element?.XamlRoot?.RasterizationScale ?? 1.0d;
            return NormalizeScale(scale);
        }

        public static double CeilingToLayoutPixel(double value, double rasterizationScale)
        {
            if (value <= 0d || double.IsNaN(value) || double.IsInfinity(value))
            {
                return 0d;
            }

            var scale = NormalizeScale(rasterizationScale);
            const double fpGuard = 1e-6d;
            return Math.Ceiling((value * scale) - fpGuard) / scale;
        }

        public static double GetLayoutRoundingTolerance(double rasterizationScale)
        {
            var scale = NormalizeScale(rasterizationScale);
            return (0.5d / scale) + 1e-3d;
        }

        public static double ComputeCumulativeScrollTarget(
            double currentOffset,
            double? pendingTargetOffset,
            double scrollableExtent,
            int wheelDelta,
            double scrollMultiplier = 2.5d)
        {
            if (scrollableExtent <= 0d)
            {
                return 0d;
            }

            var baseOffset = pendingTargetOffset ?? currentOffset;
            return Math.Clamp(baseOffset - (wheelDelta * scrollMultiplier), 0d, scrollableExtent);
        }

        private static double NormalizeScale(double rasterizationScale)
            => rasterizationScale > 0d && !double.IsNaN(rasterizationScale) && !double.IsInfinity(rasterizationScale)
                ? rasterizationScale
                : 1.0d;

        private static void OnMatchViewportHeightOfChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
        {
            if (dependencyObject is not FrameworkElement targetElement)
            {
                return;
            }

            if (targetElement.GetValue(ViewportSyncSubscriptionProperty) is ViewportSyncSubscription existingSubscription)
            {
                existingSubscription.Dispose();
                targetElement.SetValue(ViewportSyncSubscriptionProperty, null);
            }

            if (eventArgs.NewValue is ScrollViewer scrollViewer)
            {
                var subscription = new ViewportSyncSubscription(targetElement, scrollViewer);
                targetElement.SetValue(ViewportSyncSubscriptionProperty, subscription);
            }
        }

        private sealed class ViewportSyncSubscription : IDisposable
        {
            private readonly FrameworkElement _targetElement;
            private readonly ScrollViewer _scrollViewer;

            public ViewportSyncSubscription(FrameworkElement targetElement, ScrollViewer scrollViewer)
            {
                _targetElement = targetElement;
                _scrollViewer = scrollViewer;

                _scrollViewer.SizeChanged += ScrollViewer_SizeChanged;
                _targetElement.Unloaded += TargetElement_Unloaded;
                UpdateHeight();
            }

            private void ScrollViewer_SizeChanged(object sender, SizeChangedEventArgs eventArgs) => UpdateHeight();

            private void TargetElement_Unloaded(object sender, RoutedEventArgs eventArgs) => Dispose();

            private void UpdateHeight()
            {
                var viewportHeight = _scrollViewer.ViewportHeight > 0
                    ? _scrollViewer.ViewportHeight
                    : _scrollViewer.ActualHeight;

                SetHeightIfChanged(_targetElement, viewportHeight);
            }

            public void Dispose()
            {
                _scrollViewer.SizeChanged -= ScrollViewer_SizeChanged;
                _targetElement.Unloaded -= TargetElement_Unloaded;
            }
        }
    }
}
