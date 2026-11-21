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
