#nullable enable
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Japlayer.Controls
{
    /// <summary>
    /// Arranges children in an equal-sized grid that fills available viewport space,
    /// falling back to <see cref="MinItemHeight"/> when content exceeds the viewport.
    /// </summary>
    public sealed class EqualGridPanel : Panel
    {
        public static readonly DependencyProperty MaxColumnsProperty =
            DependencyProperty.Register(
                nameof(MaxColumns),
                typeof(int),
                typeof(EqualGridPanel),
                new PropertyMetadata(2, OnLayoutPropertyChanged));

        public static readonly DependencyProperty HorizontalSpacingProperty =
            DependencyProperty.Register(
                nameof(HorizontalSpacing),
                typeof(double),
                typeof(EqualGridPanel),
                new PropertyMetadata(0d, OnLayoutPropertyChanged));

        public static readonly DependencyProperty VerticalSpacingProperty =
            DependencyProperty.Register(
                nameof(VerticalSpacing),
                typeof(double),
                typeof(EqualGridPanel),
                new PropertyMetadata(0d, OnLayoutPropertyChanged));

        public static readonly DependencyProperty MinItemHeightProperty =
            DependencyProperty.Register(
                nameof(MinItemHeight),
                typeof(double),
                typeof(EqualGridPanel),
                new PropertyMetadata(110d, OnLayoutPropertyChanged));

        public static readonly DependencyProperty MaxVisibleRowsProperty =
            DependencyProperty.Register(
                nameof(MaxVisibleRows),
                typeof(int),
                typeof(EqualGridPanel),
                new PropertyMetadata(2, OnLayoutPropertyChanged));

        private ScrollViewer? _parentScrollViewer;
        private double _lastViewportHeight;

        public EqualGridPanel()
        {
            Loaded += EqualGridPanel_Loaded;
            Unloaded += EqualGridPanel_Unloaded;
        }

        public int MaxColumns
        {
            get => (int)GetValue(MaxColumnsProperty);
            set => SetValue(MaxColumnsProperty, value);
        }

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

        public double MinItemHeight
        {
            get => (double)GetValue(MinItemHeightProperty);
            set => SetValue(MinItemHeightProperty, value);
        }

        public int MaxVisibleRows
        {
            get => (int)GetValue(MaxVisibleRowsProperty);
            set => SetValue(MaxVisibleRowsProperty, value);
        }

        private static void OnLayoutPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
        {
            if (dependencyObject is EqualGridPanel panel)
            {
                panel.InvalidateMeasure();
            }
        }

        private void EqualGridPanel_Loaded(object sender, RoutedEventArgs eventArgs) => AttachToParentScrollViewer();

        private void EqualGridPanel_Unloaded(object sender, RoutedEventArgs eventArgs) => DetachFromParentScrollViewer();

        private void AttachToParentScrollViewer()
        {
            DetachFromParentScrollViewer();

            var parent = VisualTreeHelper.GetParent(this);
            while (parent != null)
            {
                if (parent is ScrollViewer scrollViewer)
                {
                    _parentScrollViewer = scrollViewer;
                    _lastViewportHeight = GetScrollViewerViewportHeight(scrollViewer);
                    _parentScrollViewer.SizeChanged += ParentScrollViewer_SizeChanged;
                    break;
                }

                parent = VisualTreeHelper.GetParent(parent);
            }
        }

        private void DetachFromParentScrollViewer()
        {
            if (_parentScrollViewer != null)
            {
                _parentScrollViewer.SizeChanged -= ParentScrollViewer_SizeChanged;
                _parentScrollViewer = null;
            }
        }

        private void ParentScrollViewer_SizeChanged(object sender, SizeChangedEventArgs eventArgs)
        {
            if (_parentScrollViewer == null)
            {
                return;
            }

            var currentHeight = GetScrollViewerViewportHeight(_parentScrollViewer);
            if (Math.Abs(currentHeight - _lastViewportHeight) > 1.0)
            {
                _lastViewportHeight = currentHeight;
                InvalidateMeasure();
            }
        }

        private static double GetScrollViewerViewportHeight(ScrollViewer scrollViewer) => scrollViewer.ViewportHeight > 0 ? scrollViewer.ViewportHeight : scrollViewer.ActualHeight;

        private (int ColumnCount, int RowCount, double ItemWidth, double ItemHeight) ComputeGridMetrics(Size availableSize)
        {
            var childCount = Children.Count;
            if (childCount == 0)
            {
                return (1, 0, 0, 0);
            }

            var columnCount = Math.Max(1, Math.Min(MaxColumns, childCount));
            var rowCount = (int)Math.Ceiling(childCount / (double)columnCount);

            var availableWidth = double.IsInfinity(availableSize.Width) ? 0d : availableSize.Width;
            var itemWidth = availableWidth > 0
                ? Math.Max(1d, Math.Floor((availableWidth - (HorizontalSpacing * (columnCount - 1))) / columnCount))
                : 300d;

            var viewportHeight = !double.IsInfinity(availableSize.Height) && availableSize.Height > 0
                ? availableSize.Height
                : (_parentScrollViewer != null ? GetScrollViewerViewportHeight(_parentScrollViewer) : 0d);

            var visibleRows = MaxVisibleRows > 0 ? Math.Min(rowCount, MaxVisibleRows) : rowCount;
            var itemHeight = viewportHeight > 0
                ? Math.Max(MinItemHeight, Math.Floor((viewportHeight - (VerticalSpacing * Math.Max(0, visibleRows - 1))) / visibleRows))
                : MinItemHeight;

            return (columnCount, rowCount, itemWidth, itemHeight);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Children.Count == 0)
            {
                return new Size(0, 0);
            }

            var (columnCount, rowCount, itemWidth, itemHeight) = ComputeGridMetrics(availableSize);
            var cellSize = new Size(itemWidth, itemHeight);

            foreach (var child in Children)
            {
                child.Measure(cellSize);
            }

            var totalWidth = (columnCount * itemWidth) + (HorizontalSpacing * Math.Max(0, columnCount - 1));
            var totalHeight = (rowCount * itemHeight) + (VerticalSpacing * Math.Max(0, rowCount - 1));
            return new Size(totalWidth, totalHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Children.Count == 0)
            {
                return finalSize;
            }

            var (columnCount, _, itemWidth, itemHeight) = ComputeGridMetrics(finalSize);

            for (var index = 0; index < Children.Count; index++)
            {
                var row = index / columnCount;
                var column = index % columnCount;
                var arrangeX = column * (itemWidth + HorizontalSpacing);
                var arrangeY = row * (itemHeight + VerticalSpacing);
                Children[index].Arrange(new Rect(arrangeX, arrangeY, itemWidth, itemHeight));
            }

            return finalSize;
        }
    }
}
