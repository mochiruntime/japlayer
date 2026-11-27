#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Japlayer.Collections;
using Japlayer.Contracts;
using Japlayer.Data.Contracts;
using Japlayer.Data.Models;
using Japlayer.Messages;
using Japlayer.Services;

namespace Japlayer.ViewModels
{
    public record TagSuggestionItem(string Name, bool IsNew)
    {
        public string DisplayLabel => IsNew ? $"+ Create \"{Name}\"" : Name;
        public override string ToString() => Name;
    }

    public partial class MediaItemViewModel(
        LibraryItem libraryItem,
        IImageProvider imageProvider,
        IMediaSceneProvider sceneProvider,
        IMediaProvider mediaProvider,
        ISettingsService settingsService,
        IMediaThumbnailProvider thumbnailProvider,
        IMediaHighlightProvider highlightProvider,
        IDatabaseChangeMonitor? databaseChangeMonitor = null,
        IMessenger? messenger = null) : ObservableObject
    {
        private int _genreCollapseLimit = 6;

        private readonly LibraryItem _libraryItem = libraryItem;
        private readonly IImageProvider _imageProvider = imageProvider;
        private readonly IMediaSceneProvider _sceneProvider = sceneProvider;
        private readonly IMediaProvider _mediaProvider = mediaProvider;
        private readonly ISettingsService _settingsService = settingsService;
        private readonly IMediaThumbnailProvider _thumbnailProvider = thumbnailProvider;
        private readonly IMediaHighlightProvider _highlightProvider = highlightProvider;
        private readonly IDatabaseChangeMonitor? _databaseChangeMonitor = databaseChangeMonitor;
        private readonly IMessenger _messenger = messenger ?? WeakReferenceMessenger.Default;
        private bool _isInitializingRating;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Title))]
        [NotifyPropertyChangedFor(nameof(ContentId))]
        [NotifyPropertyChangedFor(nameof(ReleaseDate))]
        [NotifyPropertyChangedFor(nameof(Runtime))]
        [NotifyPropertyChangedFor(nameof(FactsSummary))]
        [NotifyPropertyChangedFor(nameof(Genres))]
        [NotifyPropertyChangedFor(nameof(VisibleGenres))]
        [NotifyPropertyChangedFor(nameof(HasHiddenGenres))]
        [NotifyPropertyChangedFor(nameof(ToggleGenresButtonText))]
        [NotifyPropertyChangedFor(nameof(Series))]
        [NotifyPropertyChangedFor(nameof(SeriesSummary))]
        [NotifyPropertyChangedFor(nameof(Studios))]
        [NotifyPropertyChangedFor(nameof(PrimaryStudio))]
        [NotifyPropertyChangedFor(nameof(Staff))]
        [NotifyPropertyChangedFor(nameof(HasStaff))]
        [NotifyPropertyChangedFor(nameof(NoStaff))]
        [NotifyPropertyChangedFor(nameof(Cast))]
        [NotifyPropertyChangedFor(nameof(HasCast))]
        [NotifyPropertyChangedFor(nameof(NoCast))]
        [NotifyPropertyChangedFor(nameof(IsDetailsLoaded))]
        public partial MediaItem? MediaItemData { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMultipleScenes))]
        [NotifyPropertyChangedFor(nameof(IsSingleScene))]
        [NotifyPropertyChangedFor(nameof(ScenesSummaryText))]
        public partial ObservableCollection<MediaSceneViewModel>? Scenes { get; set; }

        [ObservableProperty]
        public partial MediaSceneViewModel? SelectedScene { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFocusMode))]
        public partial bool IsGridMode { get; set; }

        public bool IsFocusMode => !IsGridMode;

        [ObservableProperty]
        public partial bool IsGenresExpanded { get; set; }

        partial void OnIsGenresExpandedChanged(bool value)
        {
            OnPropertyChanged(nameof(VisibleGenres));
            OnPropertyChanged(nameof(HasHiddenGenres));
            OnPropertyChanged(nameof(ToggleGenresButtonText));
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RatingDisplayText))]
        public partial double Rating { get; set; } = -1;

        partial void OnRatingChanged(double value)
        {
            if (_isInitializingRating)
            {
                return;
            }

            var integerRating = value <= 0 ? 0 : (int)Math.Round(Math.Clamp(value, 0, 5));
            _ = SaveRatingAsync(integerRating);
        }

        private async Task SaveRatingAsync(int rating)
        {
            try
            {
                using (_databaseChangeMonitor?.BeginInternalWrite())
                {
                    await _mediaProvider.SetRatingAsync(Id, rating);
                }

                _messenger.Send(new MediaRatingChangedMessage(Id, rating));
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save rating: {exception.Message}");
            }
        }

        public string RatingDisplayText => Rating <= 0 ? "Unrated" : $"{(int)Math.Round(Rating)} / 5";

        public LibraryItemViewModel? LibraryItemViewModel { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(GalleryCountText))]
        [NotifyPropertyChangedFor(nameof(HasGalleryImages))]
        public partial ObservableCollection<string>? GalleryImages { get; set; }

        public ObservableCollection<string> UserTags { get; } = [];
        public BatchObservableCollection<TagSuggestionItem> TagSuggestions { get; } = [];
        public List<string> AllAvailableTags { get; private set; } = [];

        [ObservableProperty]
        public partial string TagSearchText { get; set; } = string.Empty;

        partial void OnTagSearchTextChanged(string value) => UpdateTagSuggestions();

        public string Id => _libraryItem.MediaId;
        public string Title => !string.IsNullOrWhiteSpace(MediaItemData?.Title)
            ? MediaItemData.Title
            : (_libraryItem.Title ?? _libraryItem.MediaId);

        public string DisplayCover
        {
            get
            {
                if (string.IsNullOrEmpty(_libraryItem.CoverImagePath))
                {
                    return "ms-appx:///Assets/NoCover.png";
                }

                return Path.Combine(_settingsService.ImagePath, _libraryItem.CoverImagePath);
            }
        }

        public string? ContentId => MediaItemData?.ContentId;
        public string? ReleaseDate => MediaItemData?.ReleaseDate?.ToString("yyyy-MM-dd");
        public string? Runtime => MediaItemData?.Runtime?.ToString();

        public string FactsSummary
        {
            get
            {
                var parts = new List<string> { Id };
                if (!string.IsNullOrWhiteSpace(ReleaseDate))
                {
                    parts.Add(ReleaseDate);
                }

                if (MediaItemData?.Runtime is TimeSpan duration)
                {
                    parts.Add($"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2} ({(int)duration.TotalMinutes}m)");
                }

                if (!string.IsNullOrWhiteSpace(ContentId))
                {
                    parts.Add(ContentId);
                }

                return string.Join("  •  ", parts);
            }
        }

        public IReadOnlyList<string> Genres => MediaItemData?.Genres ?? [];
        public IReadOnlyList<string> VisibleGenres => IsGenresExpanded || Genres.Count <= _genreCollapseLimit
            ? Genres
            : [.. Genres.Take(_genreCollapseLimit)];

        public bool HasHiddenGenres => Genres.Count > _genreCollapseLimit;
        public string ToggleGenresButtonText => IsGenresExpanded
            ? "Show less"
            : $"+{Genres.Count - _genreCollapseLimit} more";

        public void SetGenreCollapseLimit(int limit)
        {
            var clampedLimit = Math.Max(1, limit);
            if (_genreCollapseLimit == clampedLimit)
            {
                return;
            }

            _genreCollapseLimit = clampedLimit;
            OnPropertyChanged(nameof(VisibleGenres));
            OnPropertyChanged(nameof(HasHiddenGenres));
            OnPropertyChanged(nameof(ToggleGenresButtonText));
        }

        public IReadOnlyList<string> Series => MediaItemData?.Series ?? [];
        public string SeriesSummary => Series.Count > 0 ? string.Join(", ", Series) : "—";
        public IReadOnlyList<string> Studios => MediaItemData?.Studios ?? [];
        public string PrimaryStudio => Studios.Count > 0 ? string.Join(", ", Studios) : "—";
        public IReadOnlyList<string> Staff => MediaItemData?.Staff ?? [];
        public bool HasStaff => Staff.Count > 0;
        public bool NoStaff => !HasStaff;
        public IReadOnlyList<string> Cast => MediaItemData?.Cast ?? [];
        public bool HasCast => Cast.Count > 0;
        public bool NoCast => !HasCast;

        public bool HasMultipleScenes => Scenes != null && Scenes.Count > 1;
        public bool IsSingleScene => Scenes == null || Scenes.Count <= 1;
        public string ScenesSummaryText
        {
            get
            {
                var sceneCount = Scenes?.Count ?? 0;
                var highlightCount = Scenes?.Sum(scene => scene.Highlights.Count) ?? 0;
                var sceneWord = sceneCount == 1 ? "scene" : "scenes";
                var highlightWord = highlightCount == 1 ? "saved highlight" : "saved highlights";
                return $"{sceneCount} {sceneWord} • {highlightCount} {highlightWord}";
            }
        }

        public string GalleryCountText => $"({GalleryImages?.Count ?? 0})";
        public bool HasGalleryImages => GalleryImages != null && GalleryImages.Count > 0;

        public bool IsDetailsLoaded => MediaItemData != null;

        public void ToggleGenresExpanded() => IsGenresExpanded = !IsGenresExpanded;

        public List<TagSuggestionItem> GetTagSuggestions(string query)
        {
            var trimmedQuery = query.Trim();
            var suggestions = AllAvailableTags
                .Where(tag => !UserTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                .Where(tag => string.IsNullOrEmpty(trimmedQuery) || tag.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase))
                .Select(tag => new TagSuggestionItem(tag, false))
                .ToList();

            if (!string.IsNullOrEmpty(trimmedQuery))
            {
                var existsInAvailable = AllAvailableTags.Any(tag => string.Equals(tag, trimmedQuery, StringComparison.OrdinalIgnoreCase));
                var existsInCurrent = UserTags.Any(tag => string.Equals(tag, trimmedQuery, StringComparison.OrdinalIgnoreCase));
                if (!existsInAvailable && !existsInCurrent)
                {
                    suggestions.Add(new TagSuggestionItem(trimmedQuery, true));
                }
            }

            return suggestions;
        }

        private void UpdateTagSuggestions() => TagSuggestions.ReplaceAll(GetTagSuggestions(TagSearchText));

        public async Task AddUserTagAsync(string tagName)
        {
            var trimmedTagName = tagName.Trim();
            if (string.IsNullOrWhiteSpace(trimmedTagName))
            {
                return;
            }

            if (UserTags.Any(tag => string.Equals(tag, trimmedTagName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var canonicalTagName = AllAvailableTags.FirstOrDefault(tag =>
                string.Equals(tag, trimmedTagName, StringComparison.OrdinalIgnoreCase))
                ?? trimmedTagName;

            using (_databaseChangeMonitor?.BeginInternalWrite())
            {
                await _mediaProvider.AddUserTagAsync(Id, canonicalTagName);
            }

            UserTags.Add(canonicalTagName);

            if (!AllAvailableTags.Contains(canonicalTagName, StringComparer.OrdinalIgnoreCase))
            {
                AllAvailableTags.Add(canonicalTagName);
                AllAvailableTags.Sort(StringComparer.OrdinalIgnoreCase);
            }

            UpdateTagSuggestions();
            _messenger.Send(new MediaUserTagAddedMessage(Id, canonicalTagName));
        }

        public async Task RemoveUserTagAsync(string tagName)
        {
            var existing = UserTags.FirstOrDefault(tag => string.Equals(tag, tagName, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                return;
            }

            using (_databaseChangeMonitor?.BeginInternalWrite())
            {
                await _mediaProvider.RemoveUserTagAsync(Id, existing);
            }

            UserTags.Remove(existing);
            UpdateTagSuggestions();
            _messenger.Send(new MediaUserTagRemovedMessage(Id, existing));
        }

        public async Task LoadDetailsAsync()
        {
            if (IsDetailsLoaded)
            {
                return;
            }

            MediaItemData = await _mediaProvider.GetMediaItemAsync(Id);
            AllAvailableTags = [.. await _mediaProvider.GetUserTagsAsync()];

            UserTags.Clear();
            if (MediaItemData != null)
            {
                foreach (var tag in MediaItemData.UserTags)
                {
                    UserTags.Add(tag);
                }

                _isInitializingRating = true;
                Rating = MediaItemData.Rating <= 0 ? -1 : MediaItemData.Rating;
                _isInitializingRating = false;
            }

            UpdateTagSuggestions();

            var scenes = await _sceneProvider.GetMediaScenesAsync(Id);
            var thumbnails = await _thumbnailProvider.GetThumbnailsAsync(Id);
            var scenePosterDictionary = thumbnails.Any()
                ? thumbnails
                    .GroupBy(thumbnail => thumbnail.Scene)
                    .ToDictionary(
                        group => group.Key,
                        group =>
                        {
                            var list = group.OrderBy(thumbnail => thumbnail.Timestamp).ToList();
                            if (list.Count == 0)
                            {
                                return null;
                            }

                            var minimum = list[0].Timestamp;
                            var maximum = list[^1].Timestamp;
                            var target = minimum + 0.75 * (maximum - minimum);
                            var best = list.OrderBy(thumbnail => Math.Abs(thumbnail.Timestamp - target)).First();
                            return Path.Combine(_settingsService.ImagePath, best.Path);
                        }
                    )
                    .Where(kvp => kvp.Value != null)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!)
                : [];

            IEnumerable<Data.Entities.MediaHighlight> highlights = [];
            try
            {
                highlights = await _highlightProvider.GetHighlightsAsync(Id);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load highlights from database (table might not exist yet): {exception.Message}");
            }

            var highlightsGrouped = highlights
                .GroupBy(highlight => highlight.Scene)
                .ToDictionary(group => group.Key, group => group.Select(highlight => highlight.Timestamp).ToList());
            var thumbnailsGrouped = thumbnails
                .GroupBy(thumbnail => thumbnail.Scene)
                .ToDictionary(group => group.Key, group => group.ToList());

            var viewModels = scenes.Select(scene =>
            {
                string? posterPath = null;
                var sceneNumber = scene.SceneNumber ?? 1;
                if (scene.SceneNumber.HasValue && scenePosterDictionary.TryGetValue(sceneNumber, out var path))
                {
                    posterPath = path;
                }

                highlightsGrouped.TryGetValue(sceneNumber, out var sceneHighlights);
                thumbnailsGrouped.TryGetValue(sceneNumber, out var sceneThumbnails);

                var sceneViewModel = new MediaSceneViewModel(
                    scene,
                    posterPath,
                    sceneHighlights ?? [],
                    sceneThumbnails ?? [],
                    _highlightProvider,
                    _settingsService.ImagePath,
                    _databaseChangeMonitor);
                sceneViewModel.InitializeDimensions();
                return sceneViewModel;
            }).ToList();

            Scenes = new ObservableCollection<MediaSceneViewModel>(viewModels);
            SelectedScene = Scenes.FirstOrDefault();

            foreach (var sceneViewModel in viewModels)
            {
                sceneViewModel.Highlights.CollectionChanged += (sender, eventArgs) => UpdateHighlightCount();
            }

            UpdateHighlightCount();

            var gallery = await _imageProvider.GetGalleryPathsAsync(Id);
            var fullPaths = gallery.Select(path => Path.Combine(_settingsService.ImagePath, path));
            GalleryImages = new ObservableCollection<string>(fullPaths);
        }

        private void UpdateHighlightCount()
        {
            if (Scenes != null)
            {
                var totalHighlights = Scenes.Sum(scene => scene.Highlights.Count);
                LibraryItemViewModel?.HighlightCount = totalHighlights;

                _messenger.Send(new MediaHighlightCountChangedMessage(Id, totalHighlights));
            }

            OnPropertyChanged(nameof(ScenesSummaryText));
        }
    }
}
