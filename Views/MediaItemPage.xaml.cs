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
            GalleryScrollViewer.AddHandler(UIElement.PointerWheelChangedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(GalleryScrollViewer_PointerWheelChanged), true);
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

        private void GalleryScrollViewer_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var pointerPoint = e.GetCurrentPoint(GalleryScrollViewer);
            if (pointerPoint.Properties.IsHorizontalMouseWheel)
            {
                return;
            }

            int delta = pointerPoint.Properties.MouseWheelDelta;

            // Manual bubble up to parent if we've reached the ends
            if (delta > 0 && GalleryScrollViewer.HorizontalOffset <= 0)
            {
                // Scroll Up (to the left) but already at the left edge
                // Manually scroll RootScrollViewer UP
                RootScrollViewer.ChangeView(null, RootScrollViewer.VerticalOffset - (delta * 0.5), null, false);
                return;
            }
            if (delta < 0 && GalleryScrollViewer.HorizontalOffset >= GalleryScrollViewer.ScrollableWidth)
            {
                // Scroll Down (to the right) but already at the right edge
                // Manually scroll RootScrollViewer DOWN
                RootScrollViewer.ChangeView(null, RootScrollViewer.VerticalOffset - (delta * 0.5), null, false);
                return;
            }
            
            double scrollAmount = -delta * 5;
            double newOffset = GalleryScrollViewer.HorizontalOffset + scrollAmount;
            
            GalleryScrollViewer.ChangeView(newOffset, null, null, false);

            // Sync the target index for the manual buttons
            _targetGalleryIndex = GetCurrentImageIndex();

            e.Handled = true;
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
                    mpe.MediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
                }
                
                mpe.SizeChanged += MediaPlayerElement_SizeChanged;
                UpdateMediaPlayerHeight(mpe);
            }
        }

        private void MediaPlayerElement_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is MediaPlayerElement mpe)
            {
                mpe.SizeChanged -= MediaPlayerElement_SizeChanged;
                
                if (mpe.MediaPlayer != null)
                {
                    mpe.MediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
                }
                
                // IMPORTANT: Setting Source to null is enough to release the file handle.
                // Do NOT call mpe.MediaPlayer.Dispose() as it can cause COMException 
                // if the framework tries to access the player during/after unload.
                mpe.Source = null;
            }
        }

        private void MediaPlayer_MediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            // When media opens, we need to update the height.
            // Since we don't have the MediaPlayerElement here, we can't call UpdateMediaPlayerHeight directly.
            // However, we can use the sender's dispatcher if needed, but it's better to find the MPE.
            // A simpler way is to just let SizeChanged handle it if the layout updates,
            // or we use DispatcherQueue but we need a reference.
        }

        private void MediaPlayerElement_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is MediaPlayerElement mpe)
            {
                UpdateMediaPlayerHeight(mpe);
            }
        }

        private void UpdateMediaPlayerHeight(MediaPlayerElement mpe)
        {
            if (mpe == null) return;

            try
            {
                if (mpe.MediaPlayer != null && mpe.MediaPlayer.PlaybackSession != null && mpe.MediaPlayer.PlaybackSession.NaturalVideoWidth > 0)
                {
                    double ratio = (double)mpe.MediaPlayer.PlaybackSession.NaturalVideoHeight / mpe.MediaPlayer.PlaybackSession.NaturalVideoWidth;
                    mpe.Height = mpe.ActualWidth * ratio;
                }
            }
            catch (Exception)
            {
                // Prevent crashes if player is disposed or in an invalid state
            }
        }
    }
}
