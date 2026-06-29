#nullable enable
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Japlayer
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            Title = "Japlayer";
            SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            // Set window size
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32(1960, 1080));

            ContentFrame.Navigate(typeof(Views.LibraryPage));
        }

        public Grid FullScreenOverlay => FullScreenOverlayGrid;
        public Frame NavigationFrame => ContentFrame;

        public void SetTitleBarAndFrameFullscreen(bool isFullscreen)
        {
            if (isFullscreen)
            {
                AppTitleBar.Visibility = Visibility.Collapsed;
                Grid.SetRow(ContentFrame, 0);
                Grid.SetRowSpan(ContentFrame, 2);
            }
            else
            {
                AppTitleBar.Visibility = Visibility.Visible;
                Grid.SetRow(ContentFrame, 1);
                Grid.SetRowSpan(ContentFrame, 1);
            }
        }

        public void EnterFullScreen(FrameworkElement content, Panel originalParent)
        {
            FullScreenOverlayGrid.Children.Add(content);
            FullScreenOverlayGrid.Visibility = Visibility.Visible;
            content.Focus(FocusState.Programmatic);

            SetTitleBarAndFrameFullscreen(true);

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
        }

        public void ExitFullScreen(FrameworkElement content, Panel originalParent, double originalHeight)
        {
            FullScreenOverlayGrid.Children.Remove(content);
            FullScreenOverlayGrid.Visibility = Visibility.Collapsed;

            content.ClearValue(FrameworkElement.DataContextProperty);
            originalParent.Children.Add(content);
            originalParent.Height = originalHeight;
            content.Focus(FocusState.Programmatic);

            SetTitleBarAndFrameFullscreen(false);

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
        }

        private void RootGrid_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            if (e.Key == Windows.System.VirtualKey.Left || e.Key == Windows.System.VirtualKey.Right)
            {
                // Check if user is typing in a text input control
                var focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(this.Content.XamlRoot);
                if (focused is TextBox || focused is AutoSuggestBox || focused is PasswordBox || focused is RichEditBox)
                {
                    return;
                }

                MediaPlayerElement? activePlayer = null;

                // Check if current page is MediaItemPage and has an active player
                if (ContentFrame.Content is Views.MediaItemPage mediaItemPage)
                {
                    activePlayer = mediaItemPage.ActivePlayer;
                }

                if (activePlayer?.MediaPlayer != null)
                {
                    // Retrieve seek preferences from custom controls if available, otherwise use defaults
                    var normalSeek = 10.0;
                    var modifierSeek = 60.0;

                    var controls = activePlayer.TransportControls as Controls.CustomMediaTransportControls;
                    if (controls != null)
                    {
                        normalSeek = controls.NormalSeekResolution;
                        modifierSeek = controls.ModifierSeekResolution;
                    }

                    // Check modifier key
                    var isCtrlPressed = false;
                    try
                    {
                        isCtrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                    }
                    catch { }

                    Helpers.MediaPlaybackHelper.HandleArrowSeek(activePlayer.MediaPlayer, e.Key, isCtrlPressed, normalSeek, modifierSeek);
                    e.Handled = true;
                }
            }
        }
    }
}
