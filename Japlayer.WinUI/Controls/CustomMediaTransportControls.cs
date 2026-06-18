#nullable enable
using System;
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
        private MediaPlayerElement? _parentMpe;
        private Slider? _progressSlider;
        private Button? _slowSeekBackwardButton;
        private Button? _slowSeekForwardButton;
        private Button? _fastSeekBackwardButton;
        private Button? _fastSeekForwardButton;

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
            DefaultStyleKey = typeof(CustomMediaTransportControls);
            Loaded += CustomMediaTransportControls_Loaded;
            Unloaded += CustomMediaTransportControls_Unloaded;

            IsVolumeButtonVisible = true;
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            try
            {
                System.IO.File.AppendAllText(@"c:\Users\alex\Documents\Code\japlayer\debug_log.txt", $"[{DateTime.Now}] OnApplyTemplate started\n");
            }
            catch {}

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


            // Find elements
            _slowSeekBackwardButton = GetTemplateChild("SlowSeekBackwardButton") as Button;
            _slowSeekForwardButton = GetTemplateChild("SlowSeekForwardButton") as Button;
            _fastSeekBackwardButton = GetTemplateChild("FastSeekBackwardButton") as Button;
            _fastSeekForwardButton = GetTemplateChild("FastSeekForwardButton") as Button;
            _progressSlider = GetTemplateChild("ProgressSlider") as Slider;

            try
            {
                System.IO.File.AppendAllText(@"c:\Users\alex\Documents\Code\japlayer\debug_log.txt", 
                    $"[{DateTime.Now}] Buttons found: " +
                    $"slowBack={(_slowSeekBackwardButton != null)}, " +
                    $"slowForward={(_slowSeekForwardButton != null)}, " +
                    $"fastBack={(_fastSeekBackwardButton != null)}, " +
                    $"fastForward={(_fastSeekForwardButton != null)}, " +
                    $"slider={(_progressSlider != null)}\n");
            }
            catch {}

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

        }


        private void CustomMediaTransportControls_Loaded(object sender, RoutedEventArgs e)
        {
            // Bind keyboard handlers at the MediaPlayerElement level when control is loaded in visual tree
            _parentMpe = GetMediaPlayerElement();
            _parentMpe?.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnPlayerPreviewKeyDown), true);
        }

        private void CustomMediaTransportControls_Unloaded(object sender, RoutedEventArgs e)
        {
            _parentMpe?.RemoveHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnPlayerPreviewKeyDown));
            _parentMpe = null;
        }

        private void SlowSeekBackward_Click(object sender, RoutedEventArgs e) => Seek(-SlowSeekInterval);
        private void SlowSeekForward_Click(object sender, RoutedEventArgs e) => Seek(SlowSeekInterval);
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
            if (_parentMpe?.MediaPlayer == null)
            {
                return;
            }

            if (e.Key == VirtualKey.Left || e.Key == VirtualKey.Right)
            {
                var seekAmount = IsCtrlPressed() ? ModifierSeekResolution : NormalSeekResolution;

                var player = _parentMpe.MediaPlayer;
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
            try
            {
                System.IO.File.AppendAllText(@"c:\Users\alex\Documents\Code\japlayer\debug_log.txt", 
                    $"[{DateTime.Now}] Seek called: seconds={seconds}, _parentMpe={(_parentMpe != null)}, player={(_parentMpe?.MediaPlayer != null)}\n");
            }
            catch {}

            if (_parentMpe?.MediaPlayer == null)
            {
                return;
            }

            var player = _parentMpe.MediaPlayer;
            var position = player.Position;
            var duration = player.PlaybackSession.NaturalDuration;

            try
            {
                System.IO.File.AppendAllText(@"c:\Users\alex\Documents\Code\japlayer\debug_log.txt", 
                    $"[{DateTime.Now}] Seek values: pos={position.TotalSeconds}, dur={duration.TotalSeconds}\n");
            }
            catch {}

            player.Position = TimeSpan.FromSeconds(Math.Clamp(position.TotalSeconds + seconds, 0, duration.TotalSeconds));
        }

        private MediaPlayerElement? GetMediaPlayerElement()
        {
            DependencyObject parent = VisualTreeHelper.GetParent(this);
            while (parent != null)
            {
                if (parent is MediaPlayerElement mpe)
                {
                    return mpe;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }
}
