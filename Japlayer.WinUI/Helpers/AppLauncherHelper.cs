#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.System;

namespace Japlayer.Helpers
{
    public class AppHandlers(
        IReadOnlyList<Windows.ApplicationModel.AppInfo> uwpHandlers,
        IReadOnlyList<Win32FileHandler> win32Handlers,
        StorageFile sampleFile)
    {
        public IReadOnlyList<Windows.ApplicationModel.AppInfo> UwpHandlers { get; } = uwpHandlers;
        public IReadOnlyList<Win32FileHandler> Win32Handlers { get; } = win32Handlers;
        public StorageFile SampleFile { get; } = sampleFile;

        public static async Task<AppHandlers?> LoadForFileAsync(string filePath)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(filePath);
                var uwp = await Launcher.FindFileHandlersAsync(file.FileType);
                var win32 = FileAssociationResolver.GetHandlers(file.FileType);
                return new AppHandlers(uwp, win32, file);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading app handlers: {ex.Message}");
                return null;
            }
        }
    }

    public static class AppLauncherHelper
    {
        public static void PopulateAppMenu(
            IList<MenuFlyoutItemBase> menuItems,
            string filePath,
            AppHandlers handlers,
            Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue,
            XamlRoot xamlRoot)
        {
            var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add UWP Handlers
            foreach (var handler in handlers.UwpHandlers)
            {
                if (!addedNames.Add(handler.DisplayInfo.DisplayName))
                {
                    continue;
                }

                var item = new MenuFlyoutItem { Text = handler.DisplayInfo.DisplayName };
                LoadUwpIconAsync(item, handler, dispatcherQueue);

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
                        await ShowErrorDialogAsync($"Could not open file: {ex.Message}", xamlRoot);
                    }
                };
                menuItems.Add(item);
            }

            // Add Win32 Handlers
            foreach (var handler in handlers.Win32Handlers)
            {
                if (!addedNames.Add(handler.Name))
                {
                    continue;
                }

                var item = new MenuFlyoutItem { Text = handler.Name };
                if (!string.IsNullOrEmpty(handler.ExePath))
                {
                    LoadWin32IconAsync(item, handler.ExePath, dispatcherQueue);
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
                    await ShowErrorDialogAsync($"Could not open application picker: {ex.Message}", xamlRoot);
                }
            };
            menuItems.Add(moreItem);
        }

        private static void LoadUwpIconAsync(
            MenuFlyoutItem item,
            Windows.ApplicationModel.AppInfo handler,
            Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var logoStream = await handler.DisplayInfo.GetLogo(new Windows.Foundation.Size(256, 256)).OpenReadAsync();
                    dispatcherQueue.TryEnqueue(async () =>
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
                        dispatcherQueue.TryEnqueue(async () =>
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

        private static void LoadWin32IconAsync(
            MenuFlyoutItem item,
            string exePath,
            Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var exeFile = await StorageFile.GetFileFromPathAsync(exePath);
                    var thumbnail = await exeFile.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem, 48);
                    if (thumbnail != null)
                    {
                        dispatcherQueue.TryEnqueue(async () =>
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

        public static async Task ShowErrorDialogAsync(string message, XamlRoot xamlRoot)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = xamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
