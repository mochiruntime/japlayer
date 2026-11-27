#nullable enable
using System;
using Japlayer.Controls;
using Japlayer.Helpers;
using Japlayer.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

namespace Japlayer.Views
{
    public sealed partial class MediaItemPage : Page
    {
        public MediaItemViewModel ViewModel { get; private set; } = null!;
        private MediaPlayerElement? _fullScreenPlayer;
        private Grid? _originalPlayerParent;
        private double _originalParentHeight;
        private MediaPlayerElement? _activePlayer;
        private int _lightboxIndex;
        private bool _isDockedToScenes;
        private double _lastGenresAvailableWidth;
        private bool _isSuppressingPostSubmitReopen;
        private int _tagDropdownCloseGeneration;
        private readonly PointerEventHandler _highlightsWheelHandler;
        private readonly PointerEventHandler _rootGridPointerPressedHandler;
        private readonly PointerEventHandler _tagBoxPointerPressedHandler;

        public MediaItemPage()
        {
            this.InitializeComponent();
            _highlightsWheelHandler = new PointerEventHandler(HighlightsScrollViewer_PointerWheelChanged);
            _rootGridPointerPressedHandler = new PointerEventHandler(RootGrid_PointerPressed);
            _tagBoxPointerPressedHandler = new PointerEventHandler(TagAutoSuggestBox_PointerPressed);
            GalleryScrollViewer.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(GalleryScrollViewer_PointerWheelChanged), true);
            RootGrid.AddHandler(UIElement.PointerPressedEvent, _rootGridPointerPressedHandler, true);
            TagAutoSuggestBox.AddHandler(UIElement.PointerPressedEvent, _tagBoxPointerPressedHandler, true);
            this.Unloaded += MediaItemPage_Unloaded;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs eventArgs)
        {
            base.OnNavigatedTo(eventArgs);

            if (eventArgs.Parameter is LibraryItemViewModel libraryItem)
            {
                ViewModel = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<MediaItemViewModel>(App.Current.Services, libraryItem.LibraryItem);
                ViewModel.LibraryItemViewModel = libraryItem;
                await ViewModel.LoadDetailsAsync();

                Bindings.Update();
                HeaderBreadcrumbBar.ItemsSource = new[] { "Library", ViewModel.Id };
                UpdateGenreCollapseLimit();
                UpdateSceneRailColumnWidth();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void HeaderBreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs eventArgs)
        {
            if (eventArgs.Index == 0 && Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void ScrollToggleButton_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (_isDockedToScenes)
            {
                RootScrollViewer.ChangeView(null, 0, null, false);
            }
            else
            {
                RootScrollViewer.ChangeView(null, OverviewSection.ActualHeight, null, false);
            }
        }

        private void RootScrollViewer_SizeChanged(object sender, SizeChangedEventArgs eventArgs) => UpdateLightboxImageHeight();

        private void RootScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs eventArgs)
        {
            var dockThreshold = Math.Max(0, OverviewSection.ActualHeight - 40);
            _isDockedToScenes = RootScrollViewer.VerticalOffset >= dockThreshold && dockThreshold > 0;
            ScrollToggleButton.Content = _isDockedToScenes
                ? "↑ Back to Details & Gallery"
                : "Scroll to Scenes ↓";
        }

        private void UpdateSceneRailColumnWidth()
        {
            if (ViewModel == null)
            {
                return;
            }

            SceneRailColumn.Width = ViewModel.HasMultipleScenes ? new GridLength(320) : new GridLength(0);
        }

        private void ToggleGenres_Click(object sender, RoutedEventArgs eventArgs) => ViewModel.ToggleGenresExpanded();

        private void GenresContainerGrid_SizeChanged(object sender, SizeChangedEventArgs eventArgs)
        {
            if (LayoutHelper.HasSignificantChange(ref _lastGenresAvailableWidth, eventArgs.NewSize.Width))
            {
                UpdateGenreCollapseLimit();
            }
        }

        private void UpdateGenreCollapseLimit()
        {
            if (ViewModel == null || ViewModel.Genres.Count == 0)
            {
                return;
            }

            var availableWidth = GenresContainerGrid.ActualWidth;
            if (availableWidth <= 0)
            {
                return;
            }

            // Reserve space for the "+XX more" button + column spacing (8px) when items overflow
            const double overflowButtonReserveWidth = 84d;
            const double chipHorizontalPaddingAndBorder = 22d;
            const double chipSpacing = 6d;

            var totalAllChipsWidth = 0d;
            var chipWidths = new double[ViewModel.Genres.Count];
            var measureBlock = new TextBlock { FontSize = 12 };

            for (var index = 0; index < ViewModel.Genres.Count; index++)
            {
                measureBlock.Text = ViewModel.Genres[index];
                measureBlock.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                var chipWidth = measureBlock.DesiredSize.Width + chipHorizontalPaddingAndBorder;
                chipWidths[index] = chipWidth;
                totalAllChipsWidth += chipWidth + (index > 0 ? chipSpacing : 0d);
            }

            if (totalAllChipsWidth <= availableWidth)
            {
                ViewModel.SetGenreCollapseLimit(ViewModel.Genres.Count);
                return;
            }

            var maxChipsWidth = Math.Max(100d, availableWidth - overflowButtonReserveWidth);
            var cumulativeWidth = 0d;
            var fitCount = 0;

            for (var index = 0; index < chipWidths.Length; index++)
            {
                var nextWidth = index == 0 ? chipWidths[index] : cumulativeWidth + chipSpacing + chipWidths[index];
                if (nextWidth > maxChipsWidth && fitCount > 0)
                {
                    break;
                }

                cumulativeWidth = nextWidth;
                fitCount++;
            }

            ViewModel.SetGenreCollapseLimit(Math.Max(1, fitCount));
        }

        private async void RemoveTag_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (sender is FrameworkElement element && element.Tag is string tagName)
            {
                var wasDropdownOpen = TagAutoSuggestBox.IsSuggestionListOpen;
                if (!wasDropdownOpen)
                {
                    FocusNeutralSink();
                }

                await ViewModel.RemoveUserTagAsync(tagName);
                if (IsLoaded && !wasDropdownOpen)
                {
                    CloseTagDropdownDeferred(suppressGotFocus: false);
                }
            }
        }

        private void CloseTagDropdownDeferred(bool suppressGotFocus)
        {
            _isSuppressingPostSubmitReopen = suppressGotFocus;
            var closeGeneration = ++_tagDropdownCloseGeneration;
            TagAutoSuggestBox.IsSuggestionListOpen = false;
            DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                _isSuppressingPostSubmitReopen = false;
                if (IsLoaded && _tagDropdownCloseGeneration == closeGeneration)
                {
                    TagAutoSuggestBox.IsSuggestionListOpen = false;
                }
            });
        }

        private void TagAutoSuggestBox_GotFocus(object sender, RoutedEventArgs eventArgs)
        {
            if (_isSuppressingPostSubmitReopen)
            {
                return;
            }

            OpenTagSuggestionsIfAvailable();
        }

        private void TagAutoSuggestBox_PointerPressed(object sender, PointerRoutedEventArgs eventArgs)
        {
            _isSuppressingPostSubmitReopen = false;
            OpenTagSuggestionsIfAvailable();
        }

        private void OpenTagSuggestionsIfAvailable()
        {
            _tagDropdownCloseGeneration++;
            if (ViewModel != null && !TagAutoSuggestBox.IsSuggestionListOpen)
            {
                TagAutoSuggestBox.IsSuggestionListOpen = ViewModel.TagSuggestions.Count > 0;
            }
        }

        private void TagAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs eventArgs)
        {
            if (eventArgs.SelectedItem is TagSuggestionItem && ViewModel != null)
            {
                _isSuppressingPostSubmitReopen = true;
            }
        }

        private async void TagAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs eventArgs)
        {
            if (ViewModel == null)
            {
                return;
            }

            var tagName = eventArgs.ChosenSuggestion is TagSuggestionItem suggestion
                ? suggestion.Name
                : eventArgs.QueryText;

            ViewModel.TagSearchText = string.Empty;
            CloseTagDropdownDeferred(suppressGotFocus: true);
            var closeGeneration = _tagDropdownCloseGeneration;

            if (!string.IsNullOrWhiteSpace(tagName))
            {
                await ViewModel.AddUserTagAsync(tagName);
                if (IsLoaded && _tagDropdownCloseGeneration == closeGeneration)
                {
                    CloseTagDropdownDeferred(suppressGotFocus: true);
                }
            }
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs eventArgs)
        {
            if (eventArgs.OriginalSource is DependencyObject source && IsDescendantOf(source, TagAutoSuggestBox))
            {
                return;
            }

            if (TagAutoSuggestBox.IsSuggestionListOpen)
            {
                TagAutoSuggestBox.IsSuggestionListOpen = false;
            }

            if (IsTagAutoSuggestBoxFocused())
            {
                DispatcherQueue?.TryEnqueue(BlurTagAutoSuggestBox);
            }
        }

        private bool IsTagAutoSuggestBoxFocused() =>
            XamlRoot != null &&
            FocusManager.GetFocusedElement(XamlRoot) is DependencyObject focusedElement &&
            IsDescendantOf(focusedElement, TagAutoSuggestBox);

        private void BlurTagAutoSuggestBox()
        {
            if (!IsLoaded || !IsTagAutoSuggestBoxFocused())
            {
                return;
            }

            FocusNeutralSink();
        }

        private void FocusNeutralSink()
        {
            if (!IsLoaded)
            {
                return;
            }

            NeutralFocusSink.IsTabStop = true;
            NeutralFocusSink.Focus(FocusState.Programmatic);
            DispatcherQueue?.TryEnqueue(() => NeutralFocusSink.IsTabStop = false);
        }

        private static bool IsDescendantOf(DependencyObject? element, DependencyObject ancestor)
        {
            var current = element;
            while (current is UIElement)
            {
                if (ReferenceEquals(current, ancestor))
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void GalleryScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs eventArgs) => HandleHorizontalWheelScroll(GalleryScrollViewer, eventArgs);

        private void HighlightsScrollViewer_Loaded(object sender, RoutedEventArgs eventArgs)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                scrollViewer.RemoveHandler(UIElement.PointerWheelChangedEvent, _highlightsWheelHandler);
                scrollViewer.AddHandler(UIElement.PointerWheelChangedEvent, _highlightsWheelHandler, true);
            }
        }

        private void HighlightsScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs eventArgs)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                HandleHorizontalWheelScroll(scrollViewer, eventArgs);
            }
        }

        private static void HandleHorizontalWheelScroll(ScrollViewer scrollViewer, PointerRoutedEventArgs eventArgs)
        {
            var pointerPoint = eventArgs.GetCurrentPoint(scrollViewer);
            if (pointerPoint.Properties.IsHorizontalMouseWheel || scrollViewer.ScrollableWidth <= 0)
            {
                return;
            }

            var delta = pointerPoint.Properties.MouseWheelDelta;
            var newOffset = Math.Clamp(scrollViewer.HorizontalOffset - (delta * 2.5), 0, scrollViewer.ScrollableWidth);
            scrollViewer.ChangeView(newOffset, null, null, false);
            eventArgs.Handled = true;
        }

        private void GalleryImage_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (sender is FrameworkElement element && element.Tag is string imagePath && ViewModel.GalleryImages != null)
            {
                var index = ViewModel.GalleryImages.IndexOf(imagePath);
                OpenLightbox(Math.Max(0, index));
            }
        }

        private void OpenLightbox(int index)
        {
            if (ViewModel.GalleryImages == null || ViewModel.GalleryImages.Count == 0)
            {
                return;
            }

            _lightboxIndex = Math.Clamp(index, 0, ViewModel.GalleryImages.Count - 1);
            var imagePath = ViewModel.GalleryImages[_lightboxIndex];
            LightboxImage.Source = new BitmapImage(new Uri(imagePath));
            LightboxCounterText.Text = $"{_lightboxIndex + 1} / {ViewModel.GalleryImages.Count}";
            UpdateLightboxImageHeight();
            LightboxOverlay.Visibility = Visibility.Visible;
        }

        private void UpdateLightboxImageHeight()
        {
            if (ActualHeight <= 0)
            {
                return;
            }

            var targetHeight = Math.Clamp(ActualHeight * 0.72, 320, 660);
            LightboxImage.Height = targetHeight;
            if (LightboxImage.Parent is FrameworkElement parentBorder)
            {
                parentBorder.Height = targetHeight;
            }
        }

        private void LightboxPrevious_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (ViewModel.GalleryImages == null || ViewModel.GalleryImages.Count == 0)
            {
                return;
            }

            var previousIndex = (_lightboxIndex - 1 + ViewModel.GalleryImages.Count) % ViewModel.GalleryImages.Count;
            OpenLightbox(previousIndex);
        }

        private void LightboxNext_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (ViewModel.GalleryImages == null || ViewModel.GalleryImages.Count == 0)
            {
                return;
            }

            var nextIndex = (_lightboxIndex + 1) % ViewModel.GalleryImages.Count;
            OpenLightbox(nextIndex);
        }

        private void LightboxClose_Click(object sender, RoutedEventArgs eventArgs) => LightboxOverlay.Visibility = Visibility.Collapsed;

        private void LightboxOverlay_PointerPressed(object sender, PointerRoutedEventArgs eventArgs)
        {
            if (ReferenceEquals(eventArgs.OriginalSource, LightboxOverlay))
            {
                LightboxOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void Page_PreviewKeyDown(object sender, KeyRoutedEventArgs eventArgs)
        {
            if (LightboxOverlay.Visibility != Visibility.Visible)
            {
                return;
            }

            if (eventArgs.Key == Windows.System.VirtualKey.Escape)
            {
                LightboxOverlay.Visibility = Visibility.Collapsed;
                eventArgs.Handled = true;
            }
            else if (eventArgs.Key == Windows.System.VirtualKey.Left)
            {
                LightboxPrevious_Click(this, new RoutedEventArgs());
                eventArgs.Handled = true;
            }
            else if (eventArgs.Key == Windows.System.VirtualKey.Right)
            {
                LightboxNext_Click(this, new RoutedEventArgs());
                eventArgs.Handled = true;
            }
        }

        private void FocusViewButton_Click(object sender, RoutedEventArgs eventArgs)
        {
            PausePlayersIn(MultiGridContainer);
            ViewModel.IsGridMode = false;
            FocusViewButton.Style = null;
            if (Application.Current.Resources.TryGetValue("SubtleButtonStyle", out var subtleStyle) && subtleStyle is Style style)
            {
                GridViewButton.Style = style;
            }
        }

        private void GridViewButton_Click(object sender, RoutedEventArgs eventArgs)
        {
            PausePlayersIn(FocusTheaterContainer);
            ViewModel.IsGridMode = true;
            GridViewButton.Style = null;
            if (Application.Current.Resources.TryGetValue("SubtleButtonStyle", out var subtleStyle) && subtleStyle is Style style)
            {
                FocusViewButton.Style = style;
            }
        }

        private void SceneRailItem_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (sender is FrameworkElement element && element.DataContext is MediaSceneViewModel sceneViewModel)
            {
                if (ViewModel.SelectedScene != sceneViewModel)
                {
                    PausePlayersIn(FocusTheaterContainer);
                    ViewModel.SelectedScene = sceneViewModel;
                }
            }
        }

        private static void PausePlayersIn(DependencyObject parent)
        {
            var childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var index = 0; index < childrenCount; index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child is MediaPlayerElement mediaPlayerElement)
                {
                    try
                    {
                        mediaPlayerElement.MediaPlayer?.Pause();
                    }
                    catch
                    {
                    }
                }
                else
                {
                    PausePlayersIn(child);
                }
            }
        }

        private void FocusSourceDropDownButton_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (sender is not FrameworkElement element || ViewModel.SelectedScene is not MediaSceneViewModel sceneViewModel)
            {
                return;
            }

            var flyout = new MenuFlyout();
            foreach (var source in sceneViewModel.Sources)
            {
                var item = new ToggleMenuFlyoutItem
                {
                    Text = source.DisplayLabel,
                    IsChecked = source == sceneViewModel.SelectedSource
                };
                item.Click += (_, _) => sceneViewModel.SelectedSource = source;
                flyout.Items.Add(item);
            }

            flyout.ShowAt(element);
        }

        private void Button_PointerEntered(object sender, PointerRoutedEventArgs eventArgs)
            => ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);

        private void Button_PointerExited(object sender, PointerRoutedEventArgs eventArgs)
            => ProtectedCursor = null;

        private void PlayOverlay_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (sender is FrameworkElement element && element.DataContext is MediaSceneViewModel sceneViewModel)
            {
                sceneViewModel.PendingSeekPosition = null;
                sceneViewModel.PlayerSource = sceneViewModel.File;
                sceneViewModel.IsPlayerLoaded = true;
            }
        }

        private void HighlightThumbnail_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (sender is not FrameworkElement element || element.DataContext is not HighlightThumbnailItem highlightItem)
            {
                return;
            }

            var sceneViewModel = FindAncestorDataContext<MediaSceneViewModel>(element) ?? ViewModel.SelectedScene;
            if (sceneViewModel == null)
            {
                return;
            }

            sceneViewModel.PendingSeekPosition = TimeSpan.FromSeconds(highlightItem.TimestampSeconds);
            sceneViewModel.PlayerSource = sceneViewModel.File;
            sceneViewModel.IsPlayerLoaded = true;
        }

        private static T? FindAncestorDataContext<T>(DependencyObject element) where T : class
        {
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is FrameworkElement frameworkElement && frameworkElement.DataContext is T match)
                {
                    return match;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private void MediaPlayerElement_Loaded(object sender, RoutedEventArgs eventArgs)
        {
            if (sender is MediaPlayerElement mediaPlayerElement)
            {
                _activePlayer = mediaPlayerElement;

                try
                {
                    var player = mediaPlayerElement.MediaPlayer;
                    if (player != null)
                    {
                        player.MediaOpened += MediaPlayer_MediaOpened;
                    }
                }
                catch
                {
                }

                mediaPlayerElement.Focus(FocusState.Programmatic);
                mediaPlayerElement.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(MediaPlayerElement_PointerPressed), true);
            }
        }

        private void MediaPlayerElement_Unloaded(object sender, RoutedEventArgs eventArgs)
        {
            if (sender is MediaPlayerElement mediaPlayerElement)
            {
                try
                {
                    var player = mediaPlayerElement.MediaPlayer;
                    if (player != null)
                    {
                        player.MediaOpened -= MediaPlayer_MediaOpened;
                    }
                }
                catch
                {
                }

                mediaPlayerElement.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(MediaPlayerElement_PointerPressed));

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

        private void MediaPlayer_MediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var mediaPlayerElement = FindMediaPlayerElement(ScenesTheaterSection, sender);
                if (mediaPlayerElement?.DataContext is MediaSceneViewModel sceneViewModel && sceneViewModel.PendingSeekPosition.HasValue)
                {
                    try
                    {
                        sender.PlaybackSession.Position = sceneViewModel.PendingSeekPosition.Value;
                    }
                    catch
                    {
                    }
                    sceneViewModel.PendingSeekPosition = null;
                }
            });
        }

        private MediaPlayerElement? FindMediaPlayerElement(DependencyObject parent, Windows.Media.Playback.MediaPlayer player)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent is MediaPlayerElement mediaPlayerElement)
            {
                try
                {
                    if (mediaPlayerElement.MediaPlayer == player)
                    {
                        return mediaPlayerElement;
                    }
                }
                catch
                {
                }
            }

            var childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var index = 0; index < childrenCount; index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                var result = FindMediaPlayerElement(child, player);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private async void OpenIn_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (sender is not FrameworkElement element || element.DataContext is not MediaSceneViewModel sceneViewModel)
            {
                return;
            }

            if (string.IsNullOrEmpty(sceneViewModel.File))
            {
                return;
            }

            try
            {
                var handlers = await AppHandlers.LoadForFileAsync(sceneViewModel.File);
                if (handlers == null)
                {
                    return;
                }

                var menu = new MenuFlyout();
                AppLauncherHelper.PopulateAppMenu(menu.Items, sceneViewModel.File, handlers, DispatcherQueue, XamlRoot);

                if (menu.Items.Count > 0)
                {
                    menu.ShowAt(element);
                }
            }
            catch (Exception exception)
            {
                await AppLauncherHelper.ShowErrorDialogAsync($"Could not open file handlers: {exception.Message}", XamlRoot);
            }
        }

        private void MediaItemPage_Unloaded(object sender, RoutedEventArgs eventArgs)
        {
            RootGrid.RemoveHandler(UIElement.PointerPressedEvent, _rootGridPointerPressedHandler);
            TagAutoSuggestBox.RemoveHandler(UIElement.PointerPressedEvent, _tagBoxPointerPressedHandler);

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
                var wasPlaying = false;
                try
                {
                    if (_fullScreenPlayer.MediaPlayer != null)
                    {
                        wasPlaying = _fullScreenPlayer.MediaPlayer.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing;
                    }
                }
                catch
                {
                }

                _fullScreenPlayer.Unloaded -= MediaPlayerElement_Unloaded;

                if (_originalPlayerParent != null)
                {
                    mainWindow.ExitFullScreen(_fullScreenPlayer, _originalPlayerParent, _originalParentHeight);

                    var targetParent = _originalPlayerParent;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        targetParent.StartBringIntoView();
                    });
                }

                _fullScreenPlayer.Unloaded += MediaPlayerElement_Unloaded;

                if (wasPlaying)
                {
                    try
                    {
                        _fullScreenPlayer.MediaPlayer?.Play();
                    }
                    catch
                    {
                    }
                }

                if (_fullScreenPlayer.TransportControls is CustomMediaTransportControls controls)
                {
                    VisualStateManager.GoToState(controls, "NonFullWindowState", true);
                }

                _fullScreenPlayer = null;
                _originalPlayerParent = null;
            }
            else
            {
                var sceneDataContext = player.DataContext;
                _originalPlayerParent = VisualTreeHelper.GetParent(player) as Grid;

                var wasPlaying = false;
                try
                {
                    if (player.MediaPlayer != null)
                    {
                        wasPlaying = player.MediaPlayer.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing;
                    }
                }
                catch
                {
                }

                if (_originalPlayerParent != null)
                {
                    _originalParentHeight = _originalPlayerParent.Height;

                    player.Unloaded -= MediaPlayerElement_Unloaded;

                    _originalPlayerParent.Children.Remove(player);
                    _originalPlayerParent.Height = 0;
                }

                _fullScreenPlayer = player;
                mainWindow.EnterFullScreen(_fullScreenPlayer, _originalPlayerParent!);
                _fullScreenPlayer.DataContext = sceneDataContext;

                _fullScreenPlayer.Unloaded += MediaPlayerElement_Unloaded;

                if (wasPlaying)
                {
                    try
                    {
                        _fullScreenPlayer.MediaPlayer?.Play();
                    }
                    catch
                    {
                    }
                }

                if (_fullScreenPlayer.TransportControls is CustomMediaTransportControls controls)
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

        private void MediaPlayerElement_PointerPressed(object sender, PointerRoutedEventArgs eventArgs)
        {
            if (sender is MediaPlayerElement mediaPlayerElement)
            {
                ActivePlayer = mediaPlayerElement;
            }
        }
    }
}
