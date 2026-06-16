#nullable enable
using System;
using System.Linq;
using Japlayer.Helpers;
using Japlayer.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Japlayer.Views
{
    public sealed partial class LibraryPage : Page
    {
        public LibraryViewModel ViewModel { get; private set; }

        public LibraryPage()
        {
            ViewModel = App.GetService<LibraryViewModel>();
            this.InitializeComponent();
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
            if (e.ClickedItem is LibraryItemViewModel item)
            {
                Frame.Navigate(typeof(MediaItemPage), item);
            }
        }

        private async void MediaItem_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
            if (sender is FrameworkElement element && element.DataContext is LibraryItemViewModel vm)
            {
                await vm.LoadThumbnailsAsync();
            }
        }

        private void MediaItem_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is LibraryItemViewModel vm)
            {
                var point = e.GetCurrentPoint(element);
                var percent = point.Position.X / element.ActualWidth;
                vm.ScrubPreview(percent);
            }
        }

        private void MediaItem_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ProtectedCursor = null; // Revert to default
            if (sender is FrameworkElement element && element.DataContext is LibraryItemViewModel vm)
            {
                vm.ResetPreview();
            }
        }

        private async void MediaItem_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not LibraryItemViewModel libraryItem)
            {
                return;
            }

            e.Handled = true;

            try
            {
                var scenes = (await ViewModel.GetMediaScenesAsync(libraryItem.Id)).ToList();
                if (scenes == null || !scenes.Any())
                {
                    return;
                }

                // Find the first valid file path to determine the file type (extension)
                string? firstFilePath = null;
                foreach (var scene in scenes)
                {
                    firstFilePath = scene.FilePaths?.FirstOrDefault();
                    if (!string.IsNullOrEmpty(firstFilePath))
                    {
                        break;
                    }
                }

                if (string.IsNullOrEmpty(firstFilePath))
                {
                    return;
                }

                var handlers = await AppHandlers.LoadForFileAsync(firstFilePath);
                if (handlers == null)
                {
                    return;
                }

                var menu = new MenuFlyout();
                var openInSubItem = new MenuFlyoutSubItem { Text = "Open in..." };

                if (scenes.Count == 1)
                {
                    // Single scene: populate apps list directly under "Open in..."
                    var singleSceneFile = scenes[0].FilePaths?.FirstOrDefault();
                    if (!string.IsNullOrEmpty(singleSceneFile))
                    {
                        AppLauncherHelper.PopulateAppMenu(openInSubItem.Items, singleSceneFile, handlers, DispatcherQueue, XamlRoot);
                    }
                }
                else
                {
                    // Multiple scenes: list scenes under "Open in...", each scene has a sub-menu listing the apps
                    for (var i = 0; i < scenes.Count; i++)
                    {
                        var scene = scenes[i];
                        var sceneFile = scene.FilePaths?.FirstOrDefault();
                        if (string.IsNullOrEmpty(sceneFile))
                        {
                            continue;
                        }

                        var sceneName = scene.SceneNumber.HasValue ? $"Scene {scene.SceneNumber.Value}" : $"Scene {i + 1}";
                        var sceneSubItem = new MenuFlyoutSubItem { Text = sceneName };

                        AppLauncherHelper.PopulateAppMenu(sceneSubItem.Items, sceneFile, handlers, DispatcherQueue, XamlRoot);
                        openInSubItem.Items.Add(sceneSubItem);
                    }
                }

                if (openInSubItem.Items.Count > 0)
                {
                    menu.Items.Add(openInSubItem);
                    var position = e.GetPosition(element);
                    menu.ShowAt(element, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions { Position = position });
                }
            }
            catch (Exception ex)
            {
                await AppLauncherHelper.ShowErrorDialogAsync($"Could not open file handlers: {ex.Message}", XamlRoot);
            }
        }
    }
}
