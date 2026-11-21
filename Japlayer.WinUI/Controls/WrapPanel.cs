#nullable enable
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Japlayer.Controls
{
    public sealed class WrapPanel : Panel
    {
        public static readonly DependencyProperty HorizontalSpacingProperty =
            DependencyProperty.Register(
                nameof(HorizontalSpacing),
                typeof(double),
                typeof(WrapPanel),
                new PropertyMetadata(0d, OnSpacingChanged));

        public static readonly DependencyProperty VerticalSpacingProperty =
            DependencyProperty.Register(
                nameof(VerticalSpacing),
                typeof(double),
                typeof(WrapPanel),
                new PropertyMetadata(0d, OnSpacingChanged));

        public double HorizontalSpacing
        {
            get => (double)GetValue(HorizontalSpacingProperty);
            set => SetValue(HorizontalSpacingProperty, value);
        }

        public double VerticalSpacing
        {
            get => (double)GetValue(VerticalSpacingProperty);
            set => SetValue(VerticalSpacingProperty, value);
        }

        private static void OnSpacingChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
        {
            if (dependencyObject is WrapPanel panel)
            {
                panel.InvalidateMeasure();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var currentX = 0d;
            var currentY = 0d;
            var rowHeight = 0d;
            var maxWidth = 0d;
            var hasRowItems = false;

            foreach (var child in Children)
            {
                if (child.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                child.Measure(availableSize);
                var desired = child.DesiredSize;
                if (desired.Width <= 0 && desired.Height <= 0)
                {
                    continue;
                }

                var nextX = hasRowItems ? currentX + HorizontalSpacing + desired.Width : desired.Width;

                if (hasRowItems && !double.IsInfinity(availableSize.Width) && nextX > availableSize.Width)
                {
                    maxWidth = Math.Max(maxWidth, currentX);
                    currentY += rowHeight + VerticalSpacing;
                    currentX = desired.Width;
                    rowHeight = desired.Height;
                }
                else
                {
                    currentX = nextX;
                    rowHeight = Math.Max(rowHeight, desired.Height);
                    hasRowItems = true;
                }
            }

            maxWidth = Math.Max(maxWidth, currentX);
            var totalHeight = hasRowItems ? currentY + rowHeight : 0d;
            return new Size(maxWidth, totalHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var currentX = 0d;
            var currentY = 0d;
            var rowHeight = 0d;
            var hasRowItems = false;

            foreach (var child in Children)
            {
                if (child.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                var desired = child.DesiredSize;
                if (desired.Width <= 0 && desired.Height <= 0)
                {
                    continue;
                }

                var nextX = hasRowItems ? currentX + HorizontalSpacing + desired.Width : desired.Width;

                if (hasRowItems && nextX > finalSize.Width)
                {
                    currentY += rowHeight + VerticalSpacing;
                    currentX = 0d;
                    rowHeight = 0d;
                    hasRowItems = false;
                }

                var arrangeX = hasRowItems ? currentX + HorizontalSpacing : 0d;
                child.Arrange(new Rect(arrangeX, currentY, desired.Width, desired.Height));

                currentX = arrangeX + desired.Width;
                rowHeight = Math.Max(rowHeight, desired.Height);
                hasRowItems = true;
            }

            return finalSize;
        }
    }
}
