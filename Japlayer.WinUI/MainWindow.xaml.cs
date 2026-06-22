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
    }
}
