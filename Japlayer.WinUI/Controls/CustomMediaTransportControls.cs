#nullable enable
using System;
using System.Linq;
using Japlayer.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI.Core;

namespace Japlayer.Controls
{
    public class CustomMediaTransportControls : MediaTransportControls
    {
        private readonly ILogger<CustomMediaTransportControls> _logger;
        private MediaPlayerElement? _parentMpe;
        private Slider? _progressSlider;
        private Button? _slowSeekBackwardButton;
        private Button? _slowSeekForwardButton;
        private Button? _fastSeekBackwardButton;
        private Button? _fastSeekForwardButton;
        private Button? _fullWindowButton;
        private Canvas? _highlightsCanvas;
        private Button? _saveHighlightButton;
        private Button? _skipToNextHighlightButton;

        public static readonly DependencyProperty SlowSeekIntervalProperty =
            DependencyProperty.Register(nameof(SlowSeekInterval), typeof(double), typeof(CustomMediaTransportControls), new PropertyMetadata(10.0));

        public static readonly DependencyProperty FastSeekIntervalProperty =
            DependencyProperty.Register(nameof(FastSeekInterval), typeof(double), typeof(CustomMediaTransportControls), new PropertyMetadata(60.0));

        public static readonly DependencyProperty NormalSeekResolutionProperty =
            DependencyProperty.Register(nameof(NormalSeekResolution), typeof(double), typeof(CustomMediaTransportControls), new PropertyMetadata(10.0));

        public static readonly DependencyProperty ModifierSeekResolutionProperty =
            DependencyProperty.Register(nameof(ModifierSeekResolution), typeof(double), typeof(CustomMediaTransportControls), new PropertyMetadata(60.0));

        public double SlowSeekInterval
        {
            get => (double)GetValue(SlowSeekIntervalProperty);
            set => SetValue(SlowSeekIntervalProperty, value);
        }

        public double FastSeekInterval
        {
            get => (double)GetValue(FastSeekIntervalProperty);
            set => SetValue(FastSeekIntervalProperty, value);
        }

        public double NormalSeekResolution
        {
            get => (double)GetValue(NormalSeekResolutionProperty);
            set => SetValue(NormalSeekResolutionProperty, value);
        }

        public double ModifierSeekResolution
        {
            get => (double)GetValue(ModifierSeekResolutionProperty);
            set => SetValue(ModifierSeekResolutionProperty, value);
        }

        public CustomMediaTransportControls()
        {
            _logger = App.GetService<ILogger<CustomMediaTransportControls>>();
            DefaultStyleKey = typeof(CustomMediaTransportControls);
            Loaded += CustomMediaTransportControls_Loaded;
            Unloaded += CustomMediaTransportControls_Unloaded;
            DataContextChanged += CustomMediaTransportControls_DataContextChanged;

            IsVolumeButtonVisible = true;

            // Register PointerPressed with handledEventsToo = true to track this as active player on click
            this.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(CustomMediaTransportControls_PointerPressed), true);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _logger.LogInformation("OnApplyTemplate started");

            // Unsubscribe from previous events if template is reapplied
            if (_slowSeekBackwardButton != null)
            {
                _slowSeekBackwardButton.Click -= SlowSeekBackward_Click;
            }

            if (_slowSeekForwardButton != null)
            {
                _slowSeekForwardButton.Click -= SlowSeekForward_Click;
            }

            if (_fastSeekBackwardButton != null)
            {
                _fastSeekBackwardButton.Click -= FastSeekBackward_Click;
            }

            if (_fastSeekForwardButton != null)
            {
                _fastSeekForwardButton.Click -= FastSeekForward_Click;
            }
            if (_fullWindowButton != null)
            {
                _fullWindowButton.Click -= FullWindowButton_Click;
            }
            if (_saveHighlightButton != null)
            {
                _saveHighlightButton.Click -= SaveHighlightButton_Click;
            }
            if (_skipToNextHighlightButton != null)
            {
                _skipToNextHighlightButton.Click -= SkipToNextHighlightButton_Click;
            }
            if (_highlightsCanvas != null)
            {
                _highlightsCanvas.SizeChanged -= HighlightsCanvas_SizeChanged;
            }

            // Find elements
            _slowSeekBackwardButton = GetTemplateChild("SlowSeekBackwardButton") as Button;
            _slowSeekForwardButton = GetTemplateChild("SlowSeekForwardButton") as Button;
            _fastSeekBackwardButton = GetTemplateChild("FastSeekBackwardButton") as Button;
            _fastSeekForwardButton = GetTemplateChild("FastSeekForwardButton") as Button;
            _progressSlider = GetTemplateChild("ProgressSlider") as Slider;
            _fullWindowButton = GetTemplateChild("FullWindowButton") as Button;
            _saveHighlightButton = GetTemplateChild("SaveHighlightButton") as Button;
            _skipToNextHighlightButton = GetTemplateChild("SkipToNextHighlightButton") as Button;
            _highlightsCanvas = GetTemplateChild("HighlightsCanvas") as Canvas;

            _logger.LogInformation("Buttons found: slowBack={SlowBack}, slowForward={SlowForward}, fastBack={FastBack}, fastForward={FastForward}, slider={Slider}",
                _slowSeekBackwardButton != null,
                _slowSeekForwardButton != null,
                _fastSeekBackwardButton != null,
                _fastSeekForwardButton != null,
                _progressSlider != null);

            // Subscribe to events
            if (_slowSeekBackwardButton != null)
            {
                _slowSeekBackwardButton.Click += SlowSeekBackward_Click;
            }

            if (_slowSeekForwardButton != null)
            {
                _slowSeekForwardButton.Click += SlowSeekForward_Click;
            }

            if (_fastSeekBackwardButton != null)
            {
                _fastSeekBackwardButton.Click += FastSeekBackward_Click;
            }

            if (_fastSeekForwardButton != null)
            {
                _fastSeekForwardButton.Click += FastSeekForward_Click;
            }
            if (_fullWindowButton != null)
            {
                _fullWindowButton.Click += FullWindowButton_Click;
            }
            if (_saveHighlightButton != null)
            {
                _saveHighlightButton.Click += SaveHighlightButton_Click;
            }
            if (_skipToNextHighlightButton != null)
            {
                _skipToNextHighlightButton.Click += SkipToNextHighlightButton_Click;
            }
            if (_highlightsCanvas != null)
            {
                _highlightsCanvas.SizeChanged += HighlightsCanvas_SizeChanged;
            }

        }


        private void CustomMediaTransportControls_Loaded(object sender, RoutedEventArgs e)
        {
            _parentMpe = GetMediaPlayerElement();
            UpdateActivePlayer();

            var player = _parentMpe?.MediaPlayer;
            if (player != null)
            {
                player.MediaOpened += Player_MediaOpened;
            }
        }

        private void CustomMediaTransportControls_Unloaded(object sender, RoutedEventArgs e)
        {
            var player = _parentMpe?.MediaPlayer;
            if (player != null)
            {
                player.MediaOpened -= Player_MediaOpened;
            }
            _parentMpe = null;
        }

        private void UpdateActivePlayer()
        {
            var page = GetMediaItemPage();
            var mediaPlayerElement = GetParentMediaPlayerElement();
            if (page != null && mediaPlayerElement != null)
            {
                page.ActivePlayer = mediaPlayerElement;
            }
        }

        private void CustomMediaTransportControls_PointerPressed(object sender, PointerRoutedEventArgs e) => UpdateActivePlayer();

        private void SlowSeekBackward_Click(object sender, RoutedEventArgs e) => Seek(-SlowSeekInterval);
        private void SlowSeekForward_Click(object sender, RoutedEventArgs e) => Seek(SlowSeekInterval);

        private void FullWindowButton_Click(object sender, RoutedEventArgs e)
        {
            var page = GetMediaItemPage();
            var mediaPlayerElement = GetParentMediaPlayerElement();
            if (page != null && mediaPlayerElement != null)
            {
                page.ToggleFullScreen(mediaPlayerElement);
            }
        }
        private void FastSeekBackward_Click(object sender, RoutedEventArgs e) => Seek(-FastSeekInterval);
        private void FastSeekForward_Click(object sender, RoutedEventArgs e) => Seek(FastSeekInterval);

        private bool IsCtrlPressed()
        {
            try
            {
                return InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            }
            catch
            {
                return false;
            }
        }

        private void OnPlayerPreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var mediaPlayerElement = GetParentMediaPlayerElement();
            if (mediaPlayerElement?.MediaPlayer == null)
            {
                return;
            }

            if (e.Key == VirtualKey.Left || e.Key == VirtualKey.Right)
            {
                var seekAmount = IsCtrlPressed() ? ModifierSeekResolution : NormalSeekResolution;

                var player = mediaPlayerElement.MediaPlayer;
                var position = player.Position;
                var duration = player.PlaybackSession.NaturalDuration;

                if (e.Key == VirtualKey.Left)
                {
                    player.Position = TimeSpan.FromSeconds(Math.Max(0, position.TotalSeconds - seekAmount));
                }
                else
                {
                    player.Position = TimeSpan.FromSeconds(Math.Min(duration.TotalSeconds, position.TotalSeconds + seekAmount));
                }

                e.Handled = true;
            }
        }

        private void Seek(double seconds)
        {
            var mediaPlayerElement = GetParentMediaPlayerElement();
            _logger.LogInformation("Seek called: seconds={Seconds}, parentMpeExists={ParentMpeExists}, playerExists={PlayerExists}",
                seconds,
                mediaPlayerElement != null,
                mediaPlayerElement?.MediaPlayer != null);

            if (mediaPlayerElement?.MediaPlayer == null)
            {
                return;
            }

            var player = mediaPlayerElement.MediaPlayer;
            var position = player.Position;
            var duration = player.PlaybackSession.NaturalDuration;

            _logger.LogInformation("Seek values: position={PositionSeconds}, duration={DurationSeconds}",
                position.TotalSeconds,
                duration.TotalSeconds);

            player.Position = TimeSpan.FromSeconds(Math.Clamp(position.TotalSeconds + seconds, 0, duration.TotalSeconds));
        }

        private void CustomMediaTransportControls_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs eventArgs)
        {
            if (eventArgs.NewValue is ViewModels.MediaSceneViewModel newSceneViewModel)
            {
                newSceneViewModel.Highlights.CollectionChanged += Highlights_CollectionChanged;
                UpdateHighlightControlsVisibility();
                RedrawHighlights();
            }

            if (sender.DataContext is ViewModels.MediaSceneViewModel oldSceneViewModel && oldSceneViewModel != eventArgs.NewValue)
            {
                oldSceneViewModel.Highlights.CollectionChanged -= Highlights_CollectionChanged;
            }
        }

        private void Highlights_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs eventArgs)
        {
            UpdateHighlightControlsVisibility();
            RedrawHighlights();
        }

        private void UpdateHighlightControlsVisibility()
        {
            _skipToNextHighlightButton?.Visibility = (DataContext is ViewModels.MediaSceneViewModel sceneViewModel && sceneViewModel.HasHighlights)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void HighlightsCanvas_SizeChanged(object sender, SizeChangedEventArgs eventArgs) => RedrawHighlights();

        private void Player_MediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RedrawHighlights();
            });
        }

        private void RedrawHighlights()
        {
            if (_highlightsCanvas == null)
            {
                return;
            }

            _highlightsCanvas.Children.Clear();

            var mediaPlayerElement = GetParentMediaPlayerElement();
            if (mediaPlayerElement?.MediaPlayer == null)
            {
                return;
            }

            var player = mediaPlayerElement.MediaPlayer;
            var duration = player.PlaybackSession.NaturalDuration.TotalSeconds;
            if (duration <= 0)
            {
                return;
            }

            if (DataContext is not ViewModels.MediaSceneViewModel sceneViewModel)
            {
                return;
            }

            if (_progressSlider == null || _progressSlider.ActualWidth <= 0)
            {
                return;
            }

            var canvasWidth = _highlightsCanvas.ActualWidth;
            if (canvasWidth <= 0)
            {
                return;
            }

            // Find slider coordinates relative to the full-width canvas
            double sliderLeft = 0;
            double sliderCenterY = 0;
            double sliderActualWidth = 0;
            try
            {
                var transform = _progressSlider.TransformToVisual(_highlightsCanvas);
                var sliderStartPoint = transform.TransformPoint(new Windows.Foundation.Point(0, _progressSlider.ActualHeight / 2));
                sliderLeft = sliderStartPoint.X;
                sliderCenterY = sliderStartPoint.Y;
                sliderActualWidth = _progressSlider.ActualWidth;
            }
            catch
            {
                return; // Elements not ready or not loaded in the visual tree yet
            }

            const double thumbnailWidth = 96;
            const double thumbnailHeight = 54;

            var settingsService = App.GetService<ISettingsService>();

            var count = sceneViewModel.Highlights.Count;
            if (count == 0)
            {
                return;
            }

            // Calculate tick X positions relative to the full-width canvas
            const double thumbRadius = 9.0;
            var tickXPositions = new double[count];
            var sliderActiveWidth = sliderActualWidth - (2 * thumbRadius);
            for (var index = 0; index < count; index++)
            {
                var timestampSeconds = sceneViewModel.Highlights[index];
                var ratio = (double)timestampSeconds / duration;
                tickXPositions[index] = sliderLeft + thumbRadius + (ratio * sliderActiveWidth);
            }

            // Calculate bubble X positions relative to the full-width canvas
            var bubbleLeftPositions = new double[count];
            for (var index = 0; index < count; index++)
            {
                bubbleLeftPositions[index] = tickXPositions[index] - (thumbnailWidth / 2);
            }

            // Clamping bounds: 0 to canvasWidth (full width of screen)
            double leftLimit = 0;
            var rightLimit = canvasWidth;
            var maxRight = rightLimit - thumbnailWidth;

            for (var index = 0; index < count; index++)
            {
                bubbleLeftPositions[index] = Math.Clamp(bubbleLeftPositions[index], leftLimit, maxRight);
            }

            // Left-to-right pass to push overlapping bubbles right
            const double spacing = 6;
            for (var index = 1; index < count; index++)
            {
                var minLeft = bubbleLeftPositions[index - 1] + thumbnailWidth + spacing;
                if (bubbleLeftPositions[index] < minLeft)
                {
                    bubbleLeftPositions[index] = minLeft;
                }
            }

            // Right-to-left pass to push back if they went off-screen
            if (bubbleLeftPositions[count - 1] > maxRight)
            {
                bubbleLeftPositions[count - 1] = maxRight;
                for (var index = count - 2; index >= 0; index--)
                {
                    var maxLeft = bubbleLeftPositions[index + 1] - thumbnailWidth - spacing;
                    if (bubbleLeftPositions[index] > maxLeft)
                    {
                        bubbleLeftPositions[index] = maxLeft;
                    }
                }
            }

            // Draw ticks and bubbles
            for (var index = 0; index < count; index++)
            {
                var timestampSeconds = sceneViewModel.Highlights[index];
                var xPosition = tickXPositions[index];

                // Draw tick mark
                var tick = new Microsoft.UI.Xaml.Shapes.Rectangle
                {
                    Width = 3,
                    Height = 12,
                    Fill = (Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"],
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(tick, xPosition - 1.5);
                Canvas.SetTop(tick, sliderCenterY - 6);
                _highlightsCanvas.Children.Add(tick);

                // Find closest thumbnail
                var closestThumbnail = sceneViewModel.Thumbnails
                    .OrderBy(thumbnail => Math.Abs(thumbnail.Timestamp - timestampSeconds))
                    .FirstOrDefault();

                if (closestThumbnail != null)
                {
                    var thumbnailPath = System.IO.Path.Combine(settingsService.ImagePath, closestThumbnail.Path);
                    if (System.IO.File.Exists(thumbnailPath))
                    {
                        var resolvedLeft = bubbleLeftPositions[index];

                        var imageContainer = new Border
                        {
                            CornerRadius = new CornerRadius(6),
                            Child = new Image
                            {
                                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(thumbnailPath)),
                                Stretch = Stretch.UniformToFill
                            }
                        };

                        var thumbnailButton = new Button
                        {
                            Width = thumbnailWidth,
                            Height = thumbnailHeight,
                            Padding = new Thickness(0),
                            BorderThickness = new Thickness(1.5),
                            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.White),
                            Background = (Brush)Application.Current.Resources["AcrylicBackgroundFillColorDefaultBrush"],
                            CornerRadius = new CornerRadius(8),
                            Content = imageContainer,
                            Translation = new System.Numerics.Vector3(0, 0, 8)
                        };
                        thumbnailButton.Resources["ButtonBorderBrushPointerOver"] = (Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
                        thumbnailButton.Resources["ButtonBorderBrushPressed"] = (Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
                        ToolTipService.SetToolTip(thumbnailButton, TimeSpan.FromSeconds(timestampSeconds).ToString(@"mm\:ss"));

                        var timestamp = timestampSeconds;
                        thumbnailButton.Click += (btnSender, btnArgs) =>
                        {
                            player.Position = TimeSpan.FromSeconds(timestamp);
                        };

                        Canvas.SetLeft(thumbnailButton, resolvedLeft);
                        Canvas.SetTop(thumbnailButton, sliderCenterY - 75);
                        _highlightsCanvas.Children.Add(thumbnailButton);
                    }
                }
            }
        }
        private async void SaveHighlightButton_Click(object sender, RoutedEventArgs eventArgs)
        {
            var mediaPlayerElement = GetParentMediaPlayerElement();
            if (mediaPlayerElement?.MediaPlayer == null)
            {
                return;
            }

            var currentSeconds = (int)Math.Round(mediaPlayerElement.MediaPlayer.Position.TotalSeconds);
            if (DataContext is ViewModels.MediaSceneViewModel sceneViewModel)
            {
                await sceneViewModel.AddHighlightAsync(currentSeconds);
            }
        }

        private void SkipToNextHighlightButton_Click(object sender, RoutedEventArgs eventArgs)
        {
            var mediaPlayerElement = GetParentMediaPlayerElement();
            if (mediaPlayerElement?.MediaPlayer == null)
            {
                return;
            }

            if (DataContext is not ViewModels.MediaSceneViewModel sceneViewModel || !sceneViewModel.Highlights.Any())
            {
                return;
            }

            var currentSeconds = mediaPlayerElement.MediaPlayer.Position.TotalSeconds;

            // Find the first highlight strictly greater than the current playback position
            var nextHighlight = sceneViewModel.Highlights
                .Cast<int?>()
                .FirstOrDefault(timestamp => timestamp > currentSeconds + 0.5);

            // Loop back to the first highlight
            nextHighlight ??= sceneViewModel.Highlights.First();

            mediaPlayerElement.MediaPlayer.Position = TimeSpan.FromSeconds(nextHighlight.Value);
        }

        private MediaPlayerElement? GetParentMediaPlayerElement()
        {
            if (_parentMpe == null)
            {
                _parentMpe = GetMediaPlayerElement();
            }
            return _parentMpe;
        }

        private MediaPlayerElement? GetMediaPlayerElement()
        {
            DependencyObject parent = VisualTreeHelper.GetParent(this);
            while (parent != null)
            {
                if (parent is MediaPlayerElement mediaPlayerElement)
                {
                    return mediaPlayerElement;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private Views.MediaItemPage? GetMediaItemPage()
        {
            DependencyObject parent = VisualTreeHelper.GetParent(this);
            while (parent != null)
            {
                if (parent is Views.MediaItemPage page)
                {
                    return page;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }
}
