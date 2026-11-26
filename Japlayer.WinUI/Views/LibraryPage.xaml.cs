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

            FlyoutPassThroughHelper.Attach(RatingFilterButton, () => TopControlsContainer);
            FlyoutPassThroughHelper.Attach(RuntimeFilterButton, () => TopControlsContainer);
            FlyoutPassThroughHelper.Attach(SortDropDownButton, () => TopControlsContainer);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ViewModel.SortOrderApplied += ViewModel_SortOrderApplied;

            if (e.NavigationMode == NavigationMode.New || !ViewModel.IsDataLoaded)
            {
                await ViewModel.LoadDataAsync();
            }

            await ViewModel.SetPageActiveAsync(true);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ViewModel.SortOrderApplied -= ViewModel_SortOrderApplied;
            _ = ViewModel.SetPageActiveAsync(false);
        }

        private void ViewModel_SortOrderApplied(object? sender, EventArgs e)
        {
            if (MediaGrid.Items.Count > 0)
            {
                MediaGrid.ScrollIntoView(MediaGrid.Items[0]);
            }
        }

        private async void RefreshLibraryAccelerator_Invoked(
            Microsoft.UI.Xaml.Input.KeyboardAccelerator sender,
            Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            await ViewModel.ReloadLibraryCommand.ExecuteAsync(null);
        }

        private void PresetPill_Click(object sender, RoutedEventArgs e)
        {
            if (TryGetPresetFromSender(sender, out var preset))
            {
                ViewModel.ApplyPreset(preset);
            }
        }

        private void PresetApply_Click(object sender, RoutedEventArgs e)
        {
            if (TryGetPresetFromSender(sender, out var preset))
            {
                ViewModel.ApplyPreset(preset);
            }
        }

        private async void PresetRename_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetPresetFromSender(sender, out var preset))
            {
                return;
            }

            var nameBox = new TextBox
            {
                Text = preset.DisplayName,
                PlaceholderText = "Preset name"
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(nameBox, "RenamePresetTextBox");

            var validationText = new TextBlock
            {
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
                Visibility = Visibility.Collapsed,
                TextWrapping = TextWrapping.Wrap
            };

            var dialog = new ContentDialog
            {
                Title = "Rename preset",
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
                Content = new StackPanel
                {
                    Spacing = 8,
                    MinWidth = 300,
                    Children = { nameBox, validationText }
                }
            };

            nameBox.TextChanged += (_, _) =>
            {
                var isValid = ViewModel.TryValidatePresetName(nameBox.Text, preset.Id, out var errorMessage);
                dialog.IsPrimaryButtonEnabled = isValid;
                validationText.Text = errorMessage ?? string.Empty;
                validationText.Visibility = isValid ? Visibility.Collapsed : Visibility.Visible;
            };

            nameBox.Loaded += (_, _) =>
            {
                nameBox.SelectAll();
                nameBox.Focus(FocusState.Programmatic);
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await ViewModel.RenamePresetAsync(preset, nameBox.Text);
            }
        }

        private async void PresetUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (TryGetPresetFromSender(sender, out var preset))
            {
                await ViewModel.UpdatePresetFromCurrentAsync(preset);
            }
        }

        private async void PresetDelete_Click(object sender, RoutedEventArgs e)
        {
            if (TryGetPresetFromSender(sender, out var preset))
            {
                await ViewModel.DeletePresetAsync(preset);
            }
        }

        private static bool TryGetPresetFromSender(object sender, out LibraryPresetViewModel preset)
        {
            if (sender is FrameworkElement element)
            {
                if (element.Tag is LibraryPresetViewModel taggedPreset)
                {
                    preset = taggedPreset;
                    return true;
                }

                if (element.DataContext is LibraryPresetViewModel contextPreset)
                {
                    preset = contextPreset;
                    return true;
                }
            }

            preset = null!;
            return false;
        }

        private void MediaGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is LibraryItemViewModel item)
            {
                Frame.Navigate(typeof(MediaItemPage), item);
            }
        }

        private System.Threading.CancellationTokenSource? _hoverPrewarmCancellationTokenSource;

        private void CancelHoverPrewarm()
        {
            _hoverPrewarmCancellationTokenSource?.Cancel();
            _hoverPrewarmCancellationTokenSource?.Dispose();
            _hoverPrewarmCancellationTokenSource = null;
        }

        private async void MediaItem_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
            if (sender is FrameworkElement element && element.DataContext is LibraryItemViewModel viewModel)
            {
                CancelHoverPrewarm();
                var cancellationTokenSource = new System.Threading.CancellationTokenSource();
                _hoverPrewarmCancellationTokenSource = cancellationTokenSource;

                await viewModel.LoadThumbnailsAsync();
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    await Controls.MediaCoverView.PrewarmThumbnailsAsync(
                        viewModel.ThumbnailPaths,
                        element.ActualWidth,
                        cancellationTokenSource.Token);
                }
            }
        }

        private void MediaItem_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is LibraryItemViewModel viewModel)
            {
                var point = e.GetCurrentPoint(element);
                if (Helpers.ThumbnailDecodeCoordinator.TryCalculateScrubPercent(point.Position.X, element.ActualWidth, out var percent))
                {
                    viewModel.ScrubPreview(percent);
                }
            }
        }

        private void MediaItem_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ProtectedCursor = null; // Revert to default
            CancelHoverPrewarm();
            if (sender is FrameworkElement element && element.DataContext is LibraryItemViewModel viewModel)
            {
                viewModel.ResetPreview();
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
