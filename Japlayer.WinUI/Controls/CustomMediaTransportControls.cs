#nullable enable
using System;
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


            // Find elements
            _slowSeekBackwardButton = GetTemplateChild("SlowSeekBackwardButton") as Button;
            _slowSeekForwardButton = GetTemplateChild("SlowSeekForwardButton") as Button;
            _fastSeekBackwardButton = GetTemplateChild("FastSeekBackwardButton") as Button;
            _fastSeekForwardButton = GetTemplateChild("FastSeekForwardButton") as Button;
            _progressSlider = GetTemplateChild("ProgressSlider") as Slider;
            _fullWindowButton = GetTemplateChild("FullWindowButton") as Button;

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

        }


        private void CustomMediaTransportControls_Loaded(object sender, RoutedEventArgs e)
        {
            _parentMpe = GetMediaPlayerElement();
            UpdateActivePlayer();
        }

        private void CustomMediaTransportControls_Unloaded(object sender, RoutedEventArgs e) => _parentMpe = null;

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
