#nullable enable
using System;
using System.Collections.Generic;
using Japlayer.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Japlayer.Controls
{
    internal readonly record struct WrapPanelLayout(Size MeasuredSize, IReadOnlyList<Rect> ItemBounds);

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

        internal static WrapPanelLayout ComputeLayout(
            IReadOnlyList<Size> itemSizes,
            double availableWidth,
            double horizontalSpacing,
            double verticalSpacing,
            double wrapTolerance = 0d)
        {
            var placements = new List<Rect>(itemSizes.Count);
            var currentX = 0d;
            var currentY = 0d;
            var rowHeight = 0d;
            var maxWidth = 0d;
            var hasRowItems = false;

            foreach (var desired in itemSizes)
            {
                if (desired.Width <= 0 && desired.Height <= 0)
                {
                    placements.Add(new Rect(0, 0, 0, 0));
                    continue;
                }

                var nextX = hasRowItems ? currentX + horizontalSpacing + desired.Width : desired.Width;

                if (hasRowItems && !double.IsInfinity(availableWidth) && nextX > availableWidth + wrapTolerance)
                {
                    maxWidth = Math.Max(maxWidth, currentX);
                    currentY += rowHeight + verticalSpacing;
                    currentX = 0d;
                    rowHeight = 0d;
                    hasRowItems = false;
                }

                var arrangeX = hasRowItems ? currentX + horizontalSpacing : 0d;
                placements.Add(new Rect(arrangeX, currentY, desired.Width, desired.Height));

                currentX = arrangeX + desired.Width;
                rowHeight = Math.Max(rowHeight, desired.Height);
                hasRowItems = true;
            }

            maxWidth = Math.Max(maxWidth, currentX);
            var totalHeight = hasRowItems ? currentY + rowHeight : 0d;
            return new WrapPanelLayout(new Size(maxWidth, totalHeight), placements);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var visibleItemSizes = new List<Size>(Children.Count);

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

                visibleItemSizes.Add(desired);
            }

            var layout = ComputeLayout(visibleItemSizes, availableSize.Width, HorizontalSpacing, VerticalSpacing);
            var scale = LayoutHelper.GetRasterizationScale(this);
            return new Size(
                LayoutHelper.CeilingToLayoutPixel(layout.MeasuredSize.Width, scale),
                LayoutHelper.CeilingToLayoutPixel(layout.MeasuredSize.Height, scale));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var visibleChildren = new List<UIElement>(Children.Count);
            var visibleItemSizes = new List<Size>(Children.Count);

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

                visibleChildren.Add(child);
                visibleItemSizes.Add(desired);
            }

            var scale = LayoutHelper.GetRasterizationScale(this);
            var tolerance = LayoutHelper.GetLayoutRoundingTolerance(scale);
            var layout = ComputeLayout(visibleItemSizes, finalSize.Width, HorizontalSpacing, VerticalSpacing, tolerance);

            for (var index = 0; index < visibleChildren.Count; index++)
            {
                visibleChildren[index].Arrange(layout.ItemBounds[index]);
            }

            return finalSize;
        }
    }
}
