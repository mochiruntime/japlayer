#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Japlayer.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Japlayer.Controls
{
    public sealed partial class MediaCoverView : UserControl
    {
        private const int ThumbnailCacheCapacity = 512;
        private const int CoverCacheCapacity = 512;
        private static readonly BoundedLruCache<string, BitmapImage> ThumbnailCache = new(ThumbnailCacheCapacity, StringComparer.OrdinalIgnoreCase);
        private static readonly BoundedLruCache<string, BitmapImage> CoverCache = new(CoverCacheCapacity, StringComparer.OrdinalIgnoreCase);

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(object), typeof(MediaCoverView), new PropertyMetadata(null, OnSourceChanged));

        public object Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.Register("Stretch", typeof(Stretch), typeof(MediaCoverView), new PropertyMetadata(Stretch.Uniform));

        public Stretch Stretch
        {
            get => (Stretch)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        private Image? _activeImage;
        private Image? _inactiveImage;
        private readonly ThumbnailDecodeCoordinator _thumbnailCoordinator = new();
        private DispatcherQueueTimer? _coalesceTimer;

        public MediaCoverView()
        {
            this.InitializeComponent();
            Unloaded += OnUnloaded;
            UpdateImageSource(Source);
        }

        public static async Task PrewarmThumbnailsAsync(
            IReadOnlyList<string> thumbnailPaths,
            double cardWidth,
            CancellationToken cancellationToken)
        {
            if (thumbnailPaths.Count == 0)
            {
                return;
            }

            var decodePixelWidth = ThumbnailDecodeCoordinator.CalculateDecodePixelWidth(cardWidth);
            var keyframeIndices = ThumbnailDecodeCoordinator.SelectPrewarmIndices(thumbnailPaths.Count);

            foreach (var index in keyframeIndices)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var path = thumbnailPaths[index];
                if (string.IsNullOrEmpty(path) || ThumbnailCache.TryGetValue(path, out _) || !File.Exists(path))
                {
                    continue;
                }

                try
                {
                    await using var fileStream = File.OpenRead(path);
                    using var randomAccessStream = fileStream.AsRandomAccessStream();

                    var bitmap = new BitmapImage
                    {
                        DecodePixelWidth = decodePixelWidth,
                        DecodePixelType = DecodePixelType.Logical
                    };
                    await bitmap.SetSourceAsync(randomAccessStream);

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        ThumbnailCache.Set(path, bitmap);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to prewarm thumbnail {path}: {exception.Message}");
                }
            }
        }

        private static void OnSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            if (dependencyObject is MediaCoverView control)
            {
                control.UpdateImageSource(args.NewValue);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _coalesceTimer?.Stop();
            _thumbnailCoordinator.Reset();
        }

        private void InitializeImages()
        {
            if (CoverImage1 != null && CoverImage2 != null && _activeImage == null)
            {
                _activeImage = CoverImage1;
                _inactiveImage = CoverImage2;

                Canvas.SetZIndex(_activeImage, 1);
                Canvas.SetZIndex(_inactiveImage, 0);

                CoverImage1.ImageOpened += OnImageOpened;
                CoverImage2.ImageOpened += OnImageOpened;

                CoverImage1.ImageFailed += OnImageFailed;
                CoverImage2.ImageFailed += OnImageFailed;
            }
        }

        private void EnsureCoalesceTimer()
        {
            if (_coalesceTimer != null)
            {
                return;
            }

            var dispatcherQueue = DispatcherQueue;
            if (dispatcherQueue == null)
            {
                return;
            }

            _coalesceTimer = dispatcherQueue.CreateTimer();
            _coalesceTimer.Interval = TimeSpan.FromMilliseconds(ThumbnailDecodeCoordinator.DefaultMinDecodeIntervalMs);
            _coalesceTimer.IsRepeating = false;
            _coalesceTimer.Tick += OnCoalesceTimerTick;
        }

        private void OnCoalesceTimerTick(DispatcherQueueTimer sender, object args)
        {
            var nextPath = _thumbnailCoordinator.ConsumePending(Environment.TickCount64, Source as string);
            if (!string.IsNullOrEmpty(nextPath) && Uri.TryCreate(nextPath, UriKind.Absolute, out var uri))
            {
                StartThumbnailDecode(nextPath, uri);
            }
        }

        private void OnImageOpened(object sender, RoutedEventArgs e)
        {
            if (sender is not Image image || image.Source is not BitmapImage openedBitmap)
            {
                return;
            }

            if (image == _activeImage && image.Tag is string coverCacheKey && !ThumbnailDecodeCoordinator.IsThumbnailPath(coverCacheKey))
            {
                CoverCache.Set(coverCacheKey, openedBitmap);
                return;
            }

            if (image == _inactiveImage && _activeImage != null && image.Tag is string openedPath)
            {
                ThumbnailCache.Set(openedPath, openedBitmap);

                // Show any decoded thumbnail as long as the control is still in thumbnail-scrubbing mode
                // (do not drop intermediate frames while the pointer is moving fast).
                if (Source is string currentSource && ThumbnailDecodeCoordinator.IsThumbnailPath(currentSource))
                {
                    _inactiveImage.Opacity = 1;
                    Canvas.SetZIndex(_inactiveImage, 1);
                    Canvas.SetZIndex(_activeImage, 0);

                    (_inactiveImage, _activeImage) = (_activeImage, _inactiveImage);
                }
            }
        }

        private void OnImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            // Intentionally do not clear Source inside the callback to avoid re-entrant WinUI image teardown.
        }

        private void StartThumbnailDecode(string pathString, Uri uri)
        {
            if (_inactiveImage == null)
            {
                return;
            }

            _inactiveImage.Tag = pathString;
            var bitmap = new BitmapImage
            {
                DecodePixelWidth = ThumbnailDecodeCoordinator.CalculateDecodePixelWidth(ActualWidth),
                DecodePixelType = DecodePixelType.Logical,
                UriSource = uri
            };
            _inactiveImage.Source = bitmap;
        }

        private void ResetImages()
        {
            _coalesceTimer?.Stop();
            _thumbnailCoordinator.Reset();
            if (_activeImage == null || _inactiveImage == null)
            {
                return;
            }

            _activeImage.Source = null;
            _inactiveImage.Source = null;
            _activeImage.Tag = null;
            _inactiveImage.Tag = null;
            _activeImage.Opacity = 1;
            _inactiveImage.Opacity = 0;
        }

        private void UpdateImageSource(object newValue)
        {
            InitializeImages();

            if (_activeImage == null || _inactiveImage == null)
            {
                return;
            }

            if (newValue == null)
            {
                ResetImages();
                return;
            }

            if (newValue is ImageSource imageSource)
            {
                _coalesceTimer?.Stop();
                _thumbnailCoordinator.Reset();
                _activeImage.Source = imageSource;
                _inactiveImage.Source = null;
                _activeImage.Tag = null;
                _inactiveImage.Tag = null;
                _activeImage.Opacity = 1;
                _inactiveImage.Opacity = 0;
                return;
            }

            if (newValue is string pathString)
            {
                if (string.IsNullOrEmpty(pathString))
                {
                    ResetImages();
                    return;
                }

                try
                {
                    var uri = new Uri(pathString);
                    var isThumbnail = ThumbnailDecodeCoordinator.IsThumbnailPath(pathString);

                    if (!isThumbnail)
                    {
                        _coalesceTimer?.Stop();
                        _thumbnailCoordinator.Reset();

                        // Keep previous frame visible underneath while resetting to cover art
                        if (_inactiveImage.Source == null && _activeImage.Source != null)
                        {
                            _inactiveImage.Source = _activeImage.Source;
                            _inactiveImage.Opacity = 1;
                        }

                        var targetWidth = ActualWidth > 0 ? ActualWidth : (double.IsNaN(Width) ? 0 : Width);
                        var decodePixelWidth = ThumbnailDecodeCoordinator.CalculateDecodePixelWidth(targetWidth);
                        var coverCacheKey = $"{pathString}|{decodePixelWidth}";

                        if (CoverCache.TryGetValue(coverCacheKey, out var cachedCover))
                        {
                            _activeImage.Source = cachedCover;
                            _activeImage.Tag = coverCacheKey;
                        }
                        else
                        {
                            var bitmap = new BitmapImage
                            {
                                DecodePixelWidth = decodePixelWidth,
                                DecodePixelType = DecodePixelType.Logical,
                                UriSource = uri
                            };
                            _activeImage.Tag = coverCacheKey;
                            _activeImage.Source = bitmap;
                        }

                        _activeImage.Opacity = 1;
                        Canvas.SetZIndex(_activeImage, 1);
                        Canvas.SetZIndex(_inactiveImage, 0);
                    }
                    else if (ThumbnailCache.TryGetValue(pathString, out var cachedBitmap))
                    {
                        _coalesceTimer?.Stop();

                        // Keep the previous frame visible underneath at ZIndex 0 so swapping _activeImage
                        // never exposes the placeholder icon between frames.
                        if (_activeImage.Source != null && !ReferenceEquals(_activeImage.Source, cachedBitmap))
                        {
                            _inactiveImage.Source = _activeImage.Source;
                            _inactiveImage.Opacity = 1;
                        }

                        _activeImage.Source = cachedBitmap;
                        _activeImage.Tag = pathString;
                        _activeImage.Opacity = 1;
                        Canvas.SetZIndex(_activeImage, 1);
                        Canvas.SetZIndex(_inactiveImage, 0);
                    }
                    else
                    {
                        if (_thumbnailCoordinator.RequestOrCoalesce(pathString, Environment.TickCount64))
                        {
                            _coalesceTimer?.Stop();
                            StartThumbnailDecode(pathString, uri);
                        }
                        else
                        {
                            EnsureCoalesceTimer();
                            if (_coalesceTimer != null && !_coalesceTimer.IsRunning)
                            {
                                _coalesceTimer.Start();
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load image uri: {exception.Message}");
                    ResetImages();
                }
            }
            else
            {
                ResetImages();
            }
        }
    }
}
