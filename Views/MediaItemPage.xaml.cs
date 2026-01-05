using Japlayer.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace Japlayer.Views
{
    public sealed partial class MediaItemPage : Page
    {
        public MediaItemViewModel ViewModel { get; private set; }
        private int _targetGalleryIndex = 0;

        public MediaItemPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is MediaItemViewModel vm)
            {
                ViewModel = vm;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void ScrollLeft_Click(object sender, RoutedEventArgs e)
        {
            _targetGalleryIndex = Math.Max(_targetGalleryIndex - 1, 0);
            ScrollToImage(_targetGalleryIndex);
        }

        private void ScrollRight_Click(object sender, RoutedEventArgs e)
        {
            _targetGalleryIndex = Math.Min(_targetGalleryIndex + 1, GalleryItemsControl.ItemsPanelRoot.Children.Count - 1);
            ScrollToImage(_targetGalleryIndex);
        }

        private int GetCurrentImageIndex()
        {
            if (GalleryItemsControl.ItemsPanelRoot is not StackPanel panel)
                return 0;

            double scrollOffset = GalleryScrollViewer.HorizontalOffset;

            // Loop through the children and find the first one that begins AFTER the offset
            double cumulative = 0;
            for (int i = 0; i < panel.Children.Count; i++)
            {
                UIElement element = panel.Children[i];

                double elementWidth = ((FrameworkElement)element).ActualWidth;
                double spacing = (panel.Spacing);

                double elementStart = cumulative;
                double elementEnd = cumulative + elementWidth;

                if (scrollOffset < elementEnd)
                {
                    // This is the current item in view
                    return i;
                }

                cumulative += elementWidth + spacing;
            }

            return panel.Children.Count - 1;
        }

        private void ScrollToImage(int index)
        {
            if (GalleryItemsControl.ItemsPanelRoot is not StackPanel panel)
                return;

            index = Math.Clamp(index, 0, panel.Children.Count - 1);

            double targetOffset = 0;

            for (int i = 0; i < index; i++)
            {
                var element = (FrameworkElement)panel.Children[i];
                targetOffset += element.ActualWidth + panel.Spacing;
            }

            GalleryScrollViewer.ChangeView(targetOffset, null, null, false);
        }

        private void Button_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
        }
        private void Button_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ProtectedCursor = null; // Revert to default
        }

        private void MediaPlayerElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is MediaPlayerElement mpe)
            {
                if (mpe.MediaPlayer != null)
                {
                    mpe.MediaPlayer.MediaOpened += (s, args) => 
                    {
                        mpe.DispatcherQueue.TryEnqueue(() => UpdateMediaPlayerHeight(mpe));
                    };
                }
                
                mpe.SizeChanged += (s, args) => UpdateMediaPlayerHeight(mpe);
                UpdateMediaPlayerHeight(mpe);
            }
        }

        private void UpdateMediaPlayerHeight(MediaPlayerElement mpe)
        {
            if (mpe.MediaPlayer != null && mpe.MediaPlayer.PlaybackSession.NaturalVideoWidth > 0)
            {
                double ratio = (double)mpe.MediaPlayer.PlaybackSession.NaturalVideoHeight / mpe.MediaPlayer.PlaybackSession.NaturalVideoWidth;
                mpe.Height = mpe.ActualWidth * ratio;
            }
        }
    }
}
