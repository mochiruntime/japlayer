#nullable enable
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Japlayer.Controls
{
    public sealed partial class MediaCoverView : UserControl
    {
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

        public MediaCoverView()
        {
            this.InitializeComponent();
            UpdateImageSource(Source);
        }

        private static void OnSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            if (dependencyObject is MediaCoverView control)
            {
                control.UpdateImageSource(args.NewValue);
            }
        }

        private void InitializeImages()
        {
            if (CoverImage1 != null && CoverImage2 != null && _activeImage == null)
            {
                _activeImage = CoverImage1;
                _inactiveImage = CoverImage2;

                CoverImage1.ImageOpened += OnImageOpened;
                CoverImage2.ImageOpened += OnImageOpened;

                CoverImage1.ImageFailed += OnImageFailed;
                CoverImage2.ImageFailed += OnImageFailed;
            }
        }

        private void OnImageOpened(object sender, RoutedEventArgs e)
        {
            if (sender is Image image && image == _inactiveImage && _activeImage != null)
            {
                // Ensure this loaded image matches the currently requested source path
                if (image.Tag is string expectedPath && Source as string == expectedPath)
                {
                    // Transition: Swap active and inactive images
                    _inactiveImage.Opacity = 1;
                    _activeImage.Opacity = 0;

                    (_inactiveImage, _activeImage) = (_activeImage, _inactiveImage);

                    // Clear the now inactive image source and tag to release memory
                    _inactiveImage.Source = null;
                    _inactiveImage.Tag = null;
                }
            }
        }

        private void OnImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (sender is Image image && image == _inactiveImage)
            {
                _inactiveImage.Source = null;
                _inactiveImage.Tag = null;
            }
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
                _activeImage.Source = null;
                _inactiveImage.Source = null;
                _activeImage.Tag = null;
                _inactiveImage.Tag = null;
                _activeImage.Opacity = 1;
                _inactiveImage.Opacity = 0;
                return;
            }

            if (newValue is ImageSource imageSource)
            {
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
                    _activeImage.Source = null;
                    _inactiveImage.Source = null;
                    _activeImage.Tag = null;
                    _inactiveImage.Tag = null;
                    _activeImage.Opacity = 1;
                    _inactiveImage.Opacity = 0;
                    return;
                }

                try
                {
                    Uri uri;
                    if (pathString.StartsWith("ms-appx://") || pathString.StartsWith("ms-appdata://") || pathString.StartsWith("http://") || pathString.StartsWith("https://"))
                    {
                        uri = new Uri(pathString);
                    }
                    else
                    {
                        uri = new Uri(pathString);
                    }

                    // Check if it is a thumbnail path to apply background pre-loading
                    var isThumbnail = pathString.Contains("thumbs/", StringComparison.OrdinalIgnoreCase) ||
                                       pathString.Contains("thumbs\\", StringComparison.OrdinalIgnoreCase);

                    if (!isThumbnail)
                    {
                        // Reset or set normal cover art instantly to avoid lag on exiting hover
                        var bitmap = new BitmapImage(uri);
                        _activeImage.Source = bitmap;
                        _inactiveImage.Source = null;
                        _activeImage.Tag = null;
                        _inactiveImage.Tag = null;
                        _activeImage.Opacity = 1;
                        _inactiveImage.Opacity = 0;
                    }
                    else
                    {
                        // Load thumbnail in the background on the inactive image
                        _inactiveImage.Tag = pathString;
                        var bitmap = new BitmapImage(uri);
                        _inactiveImage.Source = bitmap;
                    }
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load image uri: {exception.Message}");
                    _activeImage.Source = null;
                    _inactiveImage.Source = null;
                    _activeImage.Tag = null;
                    _inactiveImage.Tag = null;
                    _activeImage.Opacity = 1;
                    _inactiveImage.Opacity = 0;
                }
            }
            else
            {
                _activeImage.Source = null;
                _inactiveImage.Source = null;
                _activeImage.Tag = null;
                _inactiveImage.Tag = null;
                _activeImage.Opacity = 1;
                _inactiveImage.Opacity = 0;
            }
        }
    }
}
