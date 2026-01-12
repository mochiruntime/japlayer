using Japlayer.Services;
using Japlayer.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace Japlayer.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; private set; }

        public MainPage()
        {
            this.InitializeComponent();
            ViewModel = App.GetService<MainViewModel>();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Only load data if this is a new navigation or data hasn't been loaded yet.
            // When navigating back, NavigationCacheMode="Required" preserves the state.
            if (e.NavigationMode == NavigationMode.New || !ViewModel.IsDataLoaded)
            {
                await ViewModel.LoadDataAsync();
            }
        }

        private void MediaGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MediaItemViewModel item)
            {
                Frame.Navigate(typeof(MediaItemPage), item);
            }
        }

        private void MediaItem_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
        }

        private void MediaItem_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ProtectedCursor = null; // Revert to default
        }
    }
}
