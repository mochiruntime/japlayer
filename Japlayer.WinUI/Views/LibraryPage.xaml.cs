#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Japlayer.Helpers;
using Japlayer.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.System;

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

        private void MediaItem_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);

        private void MediaItem_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => ProtectedCursor = null; // Revert to default

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

                var file = await StorageFile.GetFileFromPathAsync(firstFilePath);
                var fileType = file.FileType;
                var uwpHandlers = await Launcher.FindFileHandlersAsync(fileType);
                var win32Handlers = FileAssociationResolver.GetHandlers(fileType);

                var menu = new MenuFlyout();
                var openInSubItem = new MenuFlyoutSubItem { Text = "Open in..." };

                if (scenes.Count == 1)
                {
                    // Single scene: populate apps list directly under "Open in..."
                    var singleSceneFile = scenes[0].FilePaths?.FirstOrDefault();
                    if (!string.IsNullOrEmpty(singleSceneFile))
                    {
                        PopulateAppMenu(openInSubItem.Items, singleSceneFile, uwpHandlers, win32Handlers);
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

                        PopulateAppMenu(sceneSubItem.Items, sceneFile, uwpHandlers, win32Handlers);
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
                await ShowErrorDialogAsync($"Could not open file handlers: {ex.Message}");
            }
        }

        private void PopulateAppMenu(IList<MenuFlyoutItemBase> menuItems, string filePath, IReadOnlyList<Windows.ApplicationModel.AppInfo> uwpHandlers, IReadOnlyList<Win32FileHandler> win32Handlers)
        {
            var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add UWP Handlers
            foreach (var handler in uwpHandlers)
            {
                if (!addedNames.Add(handler.DisplayInfo.DisplayName))
                {
                    continue;
                }

                var item = new MenuFlyoutItem { Text = handler.DisplayInfo.DisplayName };
                LoadUwpIconAsync(item, handler);

                item.Click += async (s, args) =>
                {
                    try
                    {
                        var targetFile = await StorageFile.GetFileFromPathAsync(filePath);
                        var options = new LauncherOptions { TargetApplicationPackageFamilyName = handler.PackageFamilyName };
                        await Launcher.LaunchFileAsync(targetFile, options);
                    }
                    catch (Exception ex)
                    {
                        await ShowErrorDialogAsync($"Could not open file: {ex.Message}");
                    }
                };
                menuItems.Add(item);
            }

            // Add Win32 Handlers
            foreach (var handler in win32Handlers)
            {
                if (!addedNames.Add(handler.Name))
                {
                    continue;
                }

                var item = new MenuFlyoutItem { Text = handler.Name };
                if (!string.IsNullOrEmpty(handler.ExePath))
                {
                    LoadWin32IconAsync(item, handler.ExePath);
                }

                item.Click += (s, args) =>
                {
                    try
                    {
                        handler.Invoke(filePath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Win32 Launch error: {ex.Message}");
                    }
                };
                menuItems.Add(item);
            }

            if (menuItems.Count > 0)
            {
                menuItems.Add(new MenuFlyoutSeparator());
            }

            var moreItem = new MenuFlyoutItem
            {
                Text = "Choose another app...",
                Icon = new SymbolIcon { Symbol = Symbol.OpenWith }
            };
            moreItem.Click += async (s, args) =>
            {
                try
                {
                    var targetFile = await StorageFile.GetFileFromPathAsync(filePath);
                    var options = new LauncherOptions { DisplayApplicationPicker = true };
                    await Launcher.LaunchFileAsync(targetFile, options);
                }
                catch (Exception ex)
                {
                    await ShowErrorDialogAsync($"Could not open application picker: {ex.Message}");
                }
            };
            menuItems.Add(moreItem);
        }

        private void LoadUwpIconAsync(MenuFlyoutItem item, Windows.ApplicationModel.AppInfo handler)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var logoStream = await handler.DisplayInfo.GetLogo(new Windows.Foundation.Size(256, 256)).OpenReadAsync();
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        var bitmap = new BitmapImage
                        {
                            DecodePixelHeight = 72
                        };
                        await bitmap.SetSourceAsync(logoStream);

                        item.Icon = new ImageIcon
                        {
                            Source = bitmap,
                            Width = 48,
                            Height = 48,
                            Margin = new Thickness(-14)
                        };
                    });
                }
                catch
                {
                    try
                    {
                        var logoStream = await handler.DisplayInfo.GetLogo(new Windows.Foundation.Size(64, 64)).OpenReadAsync();
                        DispatcherQueue.TryEnqueue(async () =>
                        {
                            var bitmap = new BitmapImage
                            {
                                DecodePixelHeight = 20
                            };
                            await bitmap.SetSourceAsync(logoStream);
                            item.Icon = new ImageIcon { Source = bitmap, Width = 20, Height = 20 };
                        });
                    }
                    catch { }
                }
            });
        }

        private void LoadWin32IconAsync(MenuFlyoutItem item, string exePath)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var exeFile = await StorageFile.GetFileFromPathAsync(exePath);
                    var thumbnail = await exeFile.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem, 48);
                    if (thumbnail != null)
                    {
                        DispatcherQueue.TryEnqueue(async () =>
                        {
                            var bitmap = new BitmapImage
                            {
                                DecodePixelHeight = 20
                            };
                            await bitmap.SetSourceAsync(thumbnail);
                            item.Icon = new ImageIcon
                            {
                                Source = bitmap,
                                Width = 20,
                                Height = 20
                            };
                        });
                    }
                }
                catch { }
            });
        }

        private async Task ShowErrorDialogAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
