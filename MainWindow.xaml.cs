using Microsoft.UI.Xaml;

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
            this.InitializeComponent(); // Component must be initialized to find AppTitleBar
            SetTitleBar(AppTitleBar);
            
            Title = "Japlayer";
            ContentFrame.Navigate(typeof(Views.MainPage));
        }
    }
}
