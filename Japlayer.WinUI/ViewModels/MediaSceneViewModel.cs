#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Japlayer.Data.Models;
using Microsoft.UI.Xaml;

namespace Japlayer.ViewModels
{
    public partial class MediaSceneViewModel(MediaScene scene, string? posterPath) : ObservableObject
    {
        private readonly MediaScene _scene = scene;
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        [ObservableProperty]
        public partial string? PosterPath { get; set; } = posterPath;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlayOverlayVisibility))]
        public partial bool IsPlayerLoaded { get; set; }

        [ObservableProperty]
        public partial string? PlayerSource { get; set; }

        [ObservableProperty]
        public partial double? AspectRatio { get; set; }

        public void InitializeDimensions()
        {
            if (string.IsNullOrEmpty(File))
            {
                AspectRatio = 9.0 / 16.0; // Default 16:9
                return;
            }

            // Set default 16:9 aspect ratio initially to prevent height jumping
            AspectRatio = 9.0 / 16.0;

            // Start asynchronous resolution in the background
            _ = Task.Run(async () =>
            {
                try
                {
                    if (System.IO.File.Exists(File))
                    {
                        var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(File);
                        var videoProperties = await storageFile.Properties.GetVideoPropertiesAsync();
                        if (videoProperties.Width > 0 && videoProperties.Height > 0)
                        {
                            var ratio = (double)videoProperties.Height / videoProperties.Width;

                            // Marshal back to the UI thread
                            _dispatcherQueue.TryEnqueue(() =>
                            {
                                AspectRatio = ratio;
                            });
                        }
                    }
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load video properties for scene: {exception.Message}");
                }
            });
        }

        public string MediaId => _scene.MediaId;
        public int? SceneNumber => _scene.SceneNumber;

        // TODO: support multiple file locations
        public string File => _scene.FilePaths.FirstOrDefault() ?? string.Empty;

        public Visibility PlayOverlayVisibility => IsPlayerLoaded ? Visibility.Collapsed : Visibility.Visible;
    }
}
