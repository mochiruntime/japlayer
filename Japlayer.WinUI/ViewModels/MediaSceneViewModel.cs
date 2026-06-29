#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Japlayer.Data.Models;
using Microsoft.UI.Xaml;

namespace Japlayer.ViewModels
{
    public partial class MediaSceneViewModel(
        MediaScene scene,
        string? posterPath,
        System.Collections.Generic.IEnumerable<int> highlights,
        System.Collections.Generic.IEnumerable<Data.Entities.MediaThumbnail> thumbnails,
        Data.Contracts.IMediaHighlightProvider highlightProvider) : ObservableObject
    {
        private readonly MediaScene _scene = scene;
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        public System.Collections.ObjectModel.ObservableCollection<int> Highlights { get; } = new(highlights.OrderBy(h => h));
        public System.Collections.Generic.List<Data.Entities.MediaThumbnail> Thumbnails { get; } = [.. thumbnails];
        public Data.Contracts.IMediaHighlightProvider HighlightProvider { get; } = highlightProvider;

        public bool HasHighlights => Highlights.Count > 0;

        public async Task AddHighlightAsync(int timestampSeconds)
        {
            if (Highlights.Contains(timestampSeconds))
            {
                return;
            }

            try
            {
                await HighlightProvider.AddHighlightAsync(MediaId, SceneNumber ?? 1, timestampSeconds);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save highlight: {exception.Message}");
                return;
            }

            var insertionIndex = 0;
            while (insertionIndex < Highlights.Count && Highlights[insertionIndex] < timestampSeconds)
            {
                insertionIndex++;
            }
            Highlights.Insert(insertionIndex, timestampSeconds);

            OnPropertyChanged(nameof(HasHighlights));
        }

        public async Task RemoveHighlightAsync(int timestampSeconds)
        {
            if (!Highlights.Contains(timestampSeconds))
            {
                return;
            }

            try
            {
                await HighlightProvider.RemoveHighlightAsync(MediaId, SceneNumber ?? 1, timestampSeconds);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to remove highlight: {exception.Message}");
                return;
            }

            Highlights.Remove(timestampSeconds);

            OnPropertyChanged(nameof(HasHighlights));
        }

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
