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
    }
}
