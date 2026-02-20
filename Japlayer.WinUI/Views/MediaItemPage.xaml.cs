using System;
using System.Collections.Generic;
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
    public sealed partial class MediaItemPage : Page
    {
        public MediaItemViewModel ViewModel { get; private set; }
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

                var elementStart = cumulative;
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

        private void MediaPlayerElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is MediaPlayerElement mpe)
            {
                mpe.SizeChanged += MediaPlayerElement_SizeChanged;
                UpdateMediaPlayerHeight(mpe);
            }
        }

        private void MediaPlayerElement_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is MediaPlayerElement mpe)
            {
                mpe.SizeChanged -= MediaPlayerElement_SizeChanged;

                // IMPORTANT: Setting Source to null is enough to release the file handle.
                // Do NOT call mpe.MediaPlayer.Dispose() as it can cause COMException 
                // if the framework tries to access the player during/after unload.
                mpe.Source = null;
            }
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
            if (mpe == null)
            {
                return;
            }

            try
            {
                if (mpe.MediaPlayer != null && mpe.MediaPlayer.PlaybackSession != null && mpe.MediaPlayer.PlaybackSession.NaturalVideoWidth > 0)
                {
                    var ratio = (double)mpe.MediaPlayer.PlaybackSession.NaturalVideoHeight / mpe.MediaPlayer.PlaybackSession.NaturalVideoWidth;
                    mpe.Height = mpe.ActualWidth * ratio;
                }
            }
            catch (Exception)
            {
                // Prevent crashes if player is disposed or in an invalid state
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
                var file = await StorageFile.GetFileFromPathAsync(sceneVm.File);
                var uwpHandlers = await Launcher.FindFileHandlersAsync(file.FileType);
                var win32Handlers = FileAssociationResolver.GetHandlers(file.FileType);

                var menu = new MenuFlyout();
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
                        var options = new LauncherOptions { TargetApplicationPackageFamilyName = handler.PackageFamilyName };
                        await Launcher.LaunchFileAsync(file, options);
                    };
                    menu.Items.Add(item);
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
                            handler.Invoke(sceneVm.File);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Win32 Launch error: {ex.Message}");
                        }
                    };
                    menu.Items.Add(item);
                }

                if (menu.Items.Count > 0)
                {
                    menu.Items.Add(new MenuFlyoutSeparator());
                }

                var moreItem = new MenuFlyoutItem
                {
                    Text = "Choose another app...",
                    Icon = new SymbolIcon { Symbol = Symbol.OpenWith }
                };
                moreItem.Click += async (s, args) =>
                {
                    var options = new LauncherOptions { DisplayApplicationPicker = true };
                    await Launcher.LaunchFileAsync(file, options);
                };
                menu.Items.Add(moreItem);

                menu.ShowAt(element);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync($"Could not open file handlers: {ex.Message}");
            }
        }

        private void LoadUwpIconAsync(MenuFlyoutItem item, Windows.ApplicationModel.AppInfo handler)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // Request a high-res source for maximum sharp detail
                    var logoStream = await handler.DisplayInfo.GetLogo(new Windows.Foundation.Size(256, 256)).OpenReadAsync();
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        var bitmap = new BitmapImage
                        {
                            // High-res decode to avoid soft edges and aliasing
                            DecodePixelHeight = 72
                        };
                        await bitmap.SetSourceAsync(logoStream);

                        // Use 48x48 with -14 margin to "zoom in" natively by effectively 2.4x.
                        // This crops out the huge transparent borders common in MSIX app logos.
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
                    // Fallback to smaller icon if high-res fails
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
                    // Request 48x48 to avoid extreme downscaling aliasing
                    var thumbnail = await exeFile.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem, 48);
                    if (thumbnail != null)
                    {
                        DispatcherQueue.TryEnqueue(async () =>
                        {
                            var bitmap = new BitmapImage
                            {
                                // Decode specifically to 20px for maximum sharpness
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
