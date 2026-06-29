#nullable enable
using System;
using Japlayer.Controls;
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
        private MediaPlayerElement? _fullScreenPlayer;
        private Grid? _originalPlayerParent;
        private double _originalParentHeight;
        private MediaPlayerElement? _activePlayer;

        public MediaItemPage()
        {
            this.InitializeComponent();
            GalleryScrollViewer.AddHandler(UIElement.PointerWheelChangedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(GalleryScrollViewer_PointerWheelChanged), true);
            this.Unloaded += MediaItemPage_Unloaded;
            this.PreviewKeyDown += MediaItemPage_PreviewKeyDown;
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

        private void PlayOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is MediaSceneViewModel sceneVm)
            {
                sceneVm.PlayerSource = sceneVm.File;
                sceneVm.IsPlayerLoaded = true;
            }
        }

        private void MediaPlayerElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is MediaPlayerElement mediaPlayerElement)
            {
                _activePlayer = mediaPlayerElement;

                if (mediaPlayerElement.DataContext is MediaSceneViewModel sceneViewModel)
                {
                    sceneViewModel.PropertyChanged += SceneVm_PropertyChanged;
                }

                try
                {
                    var player = mediaPlayerElement.MediaPlayer;
                    if (player != null)
                    {
                        player.MediaOpened += MediaPlayer_MediaOpened;
                    }
                }
                catch (Exception)
                {
                }

                var container = FindParentGrid(mediaPlayerElement);
                if (container != null)
                {
                    UpdatePlayerContainerHeight(container);
                }

                mediaPlayerElement.Focus(FocusState.Programmatic);
                mediaPlayerElement.AddHandler(UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(MediaPlayerElement_PointerPressed), true);
            }
        }

        private void MediaPlayerElement_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is MediaPlayerElement mediaPlayerElement)
            {
                if (mediaPlayerElement.DataContext is MediaSceneViewModel sceneViewModel)
                {
                    sceneViewModel.PropertyChanged -= SceneVm_PropertyChanged;
                }

                try
                {
                    var player = mediaPlayerElement.MediaPlayer;
                    if (player != null)
                    {
                        player.MediaOpened -= MediaPlayer_MediaOpened;
                    }
                }
                catch (Exception)
                {
                }

                mediaPlayerElement.RemoveHandler(UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(MediaPlayerElement_PointerPressed));

                if (_activePlayer == mediaPlayerElement)
                {
                    _activePlayer = null;
                }

                // IMPORTANT: Setting Source to null is enough to release the file handle.
                // Do NOT call mediaPlayerElement.MediaPlayer.Dispose() as it can cause COMException 
                // if the framework tries to access the player during/after unload.
                mediaPlayerElement.Source = null;
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

        private void MediaItemPage_Unloaded(object sender, RoutedEventArgs e)
        {
            this.PreviewKeyDown -= MediaItemPage_PreviewKeyDown;
            if (_fullScreenPlayer != null)
            {
                ToggleFullScreen(_fullScreenPlayer);
            }
        }

        public void ToggleFullScreen(MediaPlayerElement player)
        {
            var mainWindow = App.GetService<MainWindow>();
            if (mainWindow == null)
            {
                return;
            }

            if (_fullScreenPlayer != null)
            {
                // Save playing state
                var wasPlaying = false;
                try
                {
                    if (_fullScreenPlayer.MediaPlayer != null)
                    {
                        wasPlaying = _fullScreenPlayer.MediaPlayer.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing;
                    }
                }
                catch { }

                // Temporarily unsubscribe Unloaded to prevent clearing Source
                _fullScreenPlayer.Unloaded -= MediaPlayerElement_Unloaded;

                if (_originalPlayerParent != null)
                {
                    mainWindow.ExitFullScreen(_fullScreenPlayer, _originalPlayerParent, _originalParentHeight);

                    // Scroll back to the player on the next layout pass
                    var targetParent = _originalPlayerParent;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        targetParent.StartBringIntoView();
                    });
                }

                // Resubscribe Unloaded
                _fullScreenPlayer.Unloaded += MediaPlayerElement_Unloaded;

                // Resume playback if it was playing
                if (wasPlaying)
                {
                    try
                    {
                        _fullScreenPlayer.MediaPlayer?.Play();
                    }
                    catch { }
                }

                // Notify controls
                var controls = _fullScreenPlayer.TransportControls as CustomMediaTransportControls;
                if (controls != null)
                {
                    VisualStateManager.GoToState(controls, "NonFullWindowState", true);
                }

                _fullScreenPlayer = null;
                _originalPlayerParent = null;
            }
            else
            {
                // Enter Fullscreen
                var sceneDataContext = player.DataContext;
                _originalPlayerParent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(player) as Grid;

                // Save playing state
                var wasPlaying = false;
                try
                {
                    if (player.MediaPlayer != null)
                    {
                        wasPlaying = player.MediaPlayer.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing;
                    }
                }
                catch { }

                if (_originalPlayerParent != null)
                {
                    _originalParentHeight = _originalPlayerParent.Height;

                    // Temporarily unsubscribe Unloaded to prevent clearing Source
                    player.Unloaded -= MediaPlayerElement_Unloaded;

                    _originalPlayerParent.Children.Remove(player);
                    _originalPlayerParent.Height = 0; // Collapse container
                }

                _fullScreenPlayer = player;
                mainWindow.EnterFullScreen(_fullScreenPlayer, _originalPlayerParent!);
                _fullScreenPlayer.DataContext = sceneDataContext;

                // Resubscribe Unloaded
                _fullScreenPlayer.Unloaded += MediaPlayerElement_Unloaded;

                // Resume playback if it was playing
                if (wasPlaying)
                {
                    try
                    {
                        _fullScreenPlayer.MediaPlayer?.Play();
                    }
                    catch { }
                }

                // Notify controls
                var controls = _fullScreenPlayer.TransportControls as CustomMediaTransportControls;
                if (controls != null)
                {
                    VisualStateManager.GoToState(controls, "FullWindowState", true);
                }
            }
        }

        public MediaPlayerElement? ActivePlayer
        {
            get => _activePlayer;
            set => _activePlayer = value;
        }

        private void MediaPlayerElement_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is MediaPlayerElement mediaPlayerElement)
            {
                ActivePlayer = mediaPlayerElement;
            }
        }

        private void MediaItemPage_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (_activePlayer?.MediaPlayer == null)
            {
                return;
            }

            if (e.Key == Windows.System.VirtualKey.Left || e.Key == Windows.System.VirtualKey.Right)
            {
                // Retrieve seek interval preferences from custom controls if available, otherwise use defaults
                var normalSeek = 10.0;
                var modifierSeek = 60.0;

                var controls = _activePlayer.TransportControls as CustomMediaTransportControls;
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

                Japlayer.Helpers.MediaPlaybackHelper.HandleArrowSeek(_activePlayer.MediaPlayer, e.Key, isCtrlPressed, normalSeek, modifierSeek);
                e.Handled = true;
            }
        }
    }
}
