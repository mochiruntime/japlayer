#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Japlayer.Data.Models;
using Japlayer.Services;
using Microsoft.UI.Xaml;

namespace Japlayer.ViewModels
{
    public record MediaSceneSourceItem(string FilePath, string FileName, string FormatLabel)
    {
        public string DisplayLabel => string.IsNullOrEmpty(FormatLabel)
            ? FileName
            : $"{FileName} ({FormatLabel})";
    }

    public record HighlightThumbnailItem(int TimestampSeconds, string FormattedTimestamp, string? ThumbnailPath);

    public partial class MediaSceneViewModel : ObservableObject
    {
        private readonly MediaScene _scene;
        private readonly string _imageRootPath;
        private readonly IDatabaseChangeMonitor? _databaseChangeMonitor;
        private readonly Microsoft.UI.Dispatching.DispatcherQueue? _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        public MediaSceneViewModel(
            MediaScene scene,
            string? posterPath,
            IEnumerable<int> highlights,
            IEnumerable<Data.Entities.MediaThumbnail> thumbnails,
            Data.Contracts.IMediaHighlightProvider highlightProvider,
            string imageRootPath = "",
            IDatabaseChangeMonitor? databaseChangeMonitor = null)
        {
            _scene = scene;
            _imageRootPath = imageRootPath;
            _databaseChangeMonitor = databaseChangeMonitor;
            PosterPath = posterPath;
            HighlightProvider = highlightProvider;
            Highlights = new ObservableCollection<int>(highlights.OrderBy(timestamp => timestamp));
            Thumbnails = [.. thumbnails];

            var sourceItems = scene.FilePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path =>
                {
                    var fileName = Path.GetFileName(path);
                    var extension = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
                    return new MediaSceneSourceItem(path, fileName, extension);
                })
                .ToList();

            Sources = new ObservableCollection<MediaSceneSourceItem>(sourceItems);
            SelectedSource = sourceItems.FirstOrDefault();

            RebuildHighlightThumbnails();
        }

        public ObservableCollection<int> Highlights { get; }
        public ObservableCollection<HighlightThumbnailItem> HighlightThumbnails { get; } = [];
        public ObservableCollection<MediaSceneSourceItem> Sources { get; }
        public List<Data.Entities.MediaThumbnail> Thumbnails { get; }
        public Data.Contracts.IMediaHighlightProvider HighlightProvider { get; }

        public bool HasHighlights => Highlights.Count > 0;
        public bool HasHighlightThumbnails => HighlightThumbnails.Count > 0;
        public bool HasMultipleSources => Sources.Count > 1;
        public bool IsSingleSource => !HasMultipleSources;
        public string HighlightSummaryText => Highlights.Count switch
        {
            0 => "No highlights",
            1 => "1 highlight",
            _ => $"{Highlights.Count} highlights"
        };

        public string SourceCountLabel => $"{Sources.Count} sources";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(File))]
        [NotifyPropertyChangedFor(nameof(SelectedSourceDisplay))]
        public partial MediaSceneSourceItem? SelectedSource { get; set; }

        partial void OnSelectedSourceChanged(MediaSceneSourceItem? value)
        {
            if (value == null)
            {
                return;
            }

            if (IsPlayerLoaded)
            {
                PlayerSource = value.FilePath;
            }

            InitializeDimensions();
        }

        public string SelectedSourceDisplay => SelectedSource?.DisplayLabel ?? string.Empty;

        public TimeSpan? PendingSeekPosition { get; set; }

        public async Task AddHighlightAsync(int timestampSeconds)
        {
            if (Highlights.Contains(timestampSeconds))
            {
                return;
            }

            try
            {
                using (_databaseChangeMonitor?.BeginInternalWrite())
                {
                    await HighlightProvider.AddHighlightAsync(MediaId, SceneNumber ?? 1, timestampSeconds);
                }
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
            RebuildHighlightThumbnails();

            OnPropertyChanged(nameof(HasHighlights));
            OnPropertyChanged(nameof(HasHighlightThumbnails));
            OnPropertyChanged(nameof(HighlightSummaryText));
        }

        public async Task RemoveHighlightAsync(int timestampSeconds)
        {
            if (!Highlights.Contains(timestampSeconds))
            {
                return;
            }

            try
            {
                using (_databaseChangeMonitor?.BeginInternalWrite())
                {
                    await HighlightProvider.RemoveHighlightAsync(MediaId, SceneNumber ?? 1, timestampSeconds);
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to remove highlight: {exception.Message}");
                return;
            }

            Highlights.Remove(timestampSeconds);
            RebuildHighlightThumbnails();

            OnPropertyChanged(nameof(HasHighlights));
            OnPropertyChanged(nameof(HasHighlightThumbnails));
            OnPropertyChanged(nameof(HighlightSummaryText));
        }

        private void RebuildHighlightThumbnails()
        {
            HighlightThumbnails.Clear();

            foreach (var timestampSeconds in Highlights)
            {
                var thumbnailPath = PosterPath;
                if (Thumbnails.Count > 0 && !string.IsNullOrEmpty(_imageRootPath))
                {
                    var nearestThumbnail = Thumbnails
                        .OrderBy(thumbnail => Math.Abs(thumbnail.Timestamp - timestampSeconds))
                        .FirstOrDefault();

                    if (nearestThumbnail != null && !string.IsNullOrEmpty(nearestThumbnail.Path))
                    {
                        thumbnailPath = Path.Combine(_imageRootPath, nearestThumbnail.Path);
                    }
                }

                HighlightThumbnails.Add(new HighlightThumbnailItem(
                    timestampSeconds,
                    FormatTimestamp(timestampSeconds),
                    thumbnailPath));
            }
        }

        private static string FormatTimestamp(int timestampSeconds)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0, timestampSeconds));
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}"
                : $"{span.Minutes:D2}:{span.Seconds:D2}";
        }

        [ObservableProperty]
        public partial string? PosterPath { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlayOverlayVisibility))]
        [NotifyPropertyChangedFor(nameof(StatusLabel))]
        public partial bool IsPlayerLoaded { get; set; }

        [ObservableProperty]
        public partial string? PlayerSource { get; set; }

        [ObservableProperty]
        public partial double? AspectRatio { get; set; }

        public string StatusLabel => IsPlayerLoaded ? "Loaded" : "Click to load";

        public void InitializeDimensions()
        {
            var currentFile = File;
            if (string.IsNullOrEmpty(currentFile))
            {
                AspectRatio = 9.0 / 16.0;
                return;
            }

            AspectRatio ??= 9.0 / 16.0;

            _ = Task.Run(async () =>
            {
                try
                {
                    if (System.IO.File.Exists(currentFile))
                    {
                        var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(currentFile);
                        var videoProperties = await storageFile.Properties.GetVideoPropertiesAsync();
                        if (videoProperties.Width > 0 && videoProperties.Height > 0)
                        {
                            var ratio = (double)videoProperties.Height / videoProperties.Width;

                            if (_dispatcherQueue != null)
                            {
                                _dispatcherQueue.TryEnqueue(() =>
                                {
                                    AspectRatio = ratio;
                                });
                            }
                            else
                            {
                                AspectRatio = ratio;
                            }
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
        public string SceneDisplayTitle => $"Scene {SceneNumber ?? 1}";
        public string File => SelectedSource?.FilePath ?? _scene.FilePaths.FirstOrDefault() ?? string.Empty;

        public Visibility PlayOverlayVisibility => IsPlayerLoaded ? Visibility.Collapsed : Visibility.Visible;
    }
}
