#nullable enable
using System;
using Japlayer.Helpers;
using Japlayer.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Japlayer.Views
{
    public sealed partial class MediaItemPage : Page
    {
        public MediaItemViewModel ViewModel { get; private set; } = null!;
        private int _targetGalleryIndex = 0;

        public MediaItemPage()
        {
            this.InitializeComponent();
            GalleryScrollViewer.AddHandler(UIElement.PointerWheelChangedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(GalleryScrollViewer_PointerWheelChanged), true);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is LibraryItemViewModel libraryItem)
            {
                ViewModel = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<MediaItemViewModel>(App.Current.Services, libraryItem.LibraryItem);
                await ViewModel.LoadDetailsAsync();
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
            {
                return 0;
            }

            var scrollOffset = GalleryScrollViewer.HorizontalOffset;

            // Loop through the children and find the first one that begins AFTER the offset
            double cumulative = 0;
            for (var i = 0; i < panel.Children.Count; i++)
            {
                UIElement element = panel.Children[i];

                var elementWidth = ((FrameworkElement)element).ActualWidth;
                var spacing = (panel.Spacing);
                var elementEnd = cumulative + elementWidth;

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
            {
                return;
            }

            index = Math.Clamp(index, 0, panel.Children.Count - 1);

            double targetOffset = 0;

            for (var i = 0; i < index; i++)
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

            var delta = pointerPoint.Properties.MouseWheelDelta;

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
            var newOffset = GalleryScrollViewer.HorizontalOffset + scrollAmount;

            GalleryScrollViewer.ChangeView(newOffset, null, null, false);

            // Sync the target index for the manual buttons
            _targetGalleryIndex = GetCurrentImageIndex();

            e.Handled = true;
        }

        private void Button_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
        private void Button_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => ProtectedCursor = null; // Revert to default cursor

        private void PlayOverlay_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is MediaSceneViewModel sceneVm)
            {
                sceneVm.PlayerSource = sceneVm.File;
                sceneVm.IsPlayerLoaded = true;
            }
        }

        private void MediaPlayerElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is MediaPlayerElement mpe)
            {
                if (mpe.DataContext is MediaSceneViewModel sceneVm)
                {
                    sceneVm.PropertyChanged += SceneVm_PropertyChanged;
                }

                try
                {
                    var player = mpe.MediaPlayer;
                    if (player != null)
                    {
                        player.MediaOpened += MediaPlayer_MediaOpened;
                    }
                }
                catch (Exception)
                {
                }

                var container = FindParentGrid(mpe);
                if (container != null)
                {
                    UpdatePlayerContainerHeight(container);
                }
            }
        }

        private void MediaPlayerElement_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is MediaPlayerElement mpe)
            {
                if (mpe.DataContext is MediaSceneViewModel sceneVm)
                {
                    sceneVm.PropertyChanged -= SceneVm_PropertyChanged;
                }

                try
                {
                    var player = mpe.MediaPlayer;
                    if (player != null)
                    {
                        player.MediaOpened -= MediaPlayer_MediaOpened;
                    }
                }
                catch (Exception)
                {
                }

                // IMPORTANT: Setting Source to null is enough to release the file handle.
                // Do NOT call mpe.MediaPlayer.Dispose() as it can cause COMException 
                // if the framework tries to access the player during/after unload.
                mpe.Source = null;
            }
        }

        private void PlayerContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Grid container)
            {
                UpdatePlayerContainerHeight(container);
            }
        }

        private void MediaPlayer_MediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var container = FindPlayerContainer(RootScrollViewer, sender);
                if (container != null)
                {
                    UpdatePlayerContainerHeight(container);
                }
            });
        }

        private void SceneVm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MediaSceneViewModel.AspectRatio))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (sender is MediaSceneViewModel sceneVm)
                    {
                        var container = FindPlayerContainerByDataContext(RootScrollViewer, sceneVm);
                        if (container != null)
                        {
                            UpdatePlayerContainerHeight(container);
                        }
                    }
                });
            }
        }

        private Grid? FindParentGrid(DependencyObject child)
        {
            var parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is Grid grid && grid.Name == "PlayerContainer")
                {
                    return grid;
                }
                parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private MediaPlayerElement? FindMediaPlayerElement(DependencyObject parent, Windows.Media.Playback.MediaPlayer player)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent is MediaPlayerElement mpe)
            {
                try
                {
                    if (mpe.MediaPlayer == player)
                    {
                        return mpe;
                    }
                }
                catch { }
            }

            var childrenCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childrenCount; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                var result = FindMediaPlayerElement(child, player);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private Grid? FindPlayerContainer(DependencyObject parent, Windows.Media.Playback.MediaPlayer player)
        {
            var mpe = FindMediaPlayerElement(parent, player);
            if (mpe != null)
            {
                return FindParentGrid(mpe);
            }
            return null;
        }

        private Grid? FindPlayerContainerByDataContext(DependencyObject parent, MediaSceneViewModel dataContext)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent is Grid grid && grid.Name == "PlayerContainer" && grid.DataContext == dataContext)
            {
                return grid;
            }

            var childrenCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childrenCount; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                var result = FindPlayerContainerByDataContext(child, dataContext);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            if (parent is T typedChild)
            {
                return typedChild;
            }

            var childrenCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childrenCount; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                var result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void UpdatePlayerContainerHeight(Grid container)
        {
            if (container == null || container.DataContext is not MediaSceneViewModel sceneVm)
            {
                return;
            }

            if (container.ActualWidth <= 0)
            {
                return;
            }

            // Use the cached aspect ratio if available to avoid calling COM properties on the UI thread
            if (sceneVm.AspectRatio.HasValue)
            {
                container.Height = container.ActualWidth * sceneVm.AspectRatio.Value;
                return;
            }

            // Only retrieve dimensions if player is active and we are in or after the MediaOpened event
            if (sceneVm.IsPlayerLoaded)
            {
                try
                {
                    var mpe = FindVisualChild<MediaPlayerElement>(container);
                    var player = mpe?.MediaPlayer;
                    if (player != null && player.PlaybackSession != null && player.PlaybackSession.NaturalVideoWidth > 0)
                    {
                        var ratio = (double)player.PlaybackSession.NaturalVideoHeight / player.PlaybackSession.NaturalVideoWidth;
                        sceneVm.AspectRatio = ratio;
                        container.Height = container.ActualWidth * ratio;
                    }
                }
                catch (Exception)
                {
                    // Prevent crashes if player is disposed or in an invalid state
                }
            }
        }
        private async void OpenIn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not MediaSceneViewModel sceneVm)
            {
                return;
            }

            if (string.IsNullOrEmpty(sceneVm.File))
            {
                return;
            }

            try
            {
                var handlers = await AppHandlers.LoadForFileAsync(sceneVm.File);
                if (handlers == null)
                {
                    return;
                }

                var menu = new MenuFlyout();
                AppLauncherHelper.PopulateAppMenu(menu.Items, sceneVm.File, handlers, DispatcherQueue, XamlRoot);

                if (menu.Items.Count > 0)
                {
                    menu.ShowAt(element);
                }
            }
            catch (Exception ex)
            {
                await AppLauncherHelper.ShowErrorDialogAsync($"Could not open file handlers: {ex.Message}", XamlRoot);
            }
        }
    }
}
