using Japlayer.Services;
using Japlayer.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Japlayer.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; private set; }

        public MainPage()
        {
            this.InitializeComponent();
            
            // Manual DI wire-up (ideally this should be done in App.xaml.cs or a factory)
            var settings = new SettingsService();
            var mediaProvider = new FileSystemMediaProvider(settings);
            var imageProvider = new FileSystemImageProvider(settings);
            
            ViewModel = new MainViewModel(mediaProvider, imageProvider);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.LoadDataAsync();
        }
    }
}
