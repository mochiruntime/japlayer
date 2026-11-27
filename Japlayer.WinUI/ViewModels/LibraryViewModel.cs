#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Japlayer.Collections;
using Japlayer.Data.Contracts;
using Japlayer.Data.Models;
using Japlayer.Messages;
using Japlayer.Models;
using Japlayer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace Japlayer.ViewModels
{
    public partial class LibraryViewModel :
        ObservableObject,
        IRecipient<MediaRatingChangedMessage>,
        IRecipient<MediaUserTagAddedMessage>,
        IRecipient<MediaUserTagRemovedMessage>,
        IRecipient<MediaHighlightCountChangedMessage>
    {
        private readonly IMediaProvider _mediaProvider;
        private readonly IMediaSceneProvider _sceneProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDatabaseChangeMonitor? _databaseChangeMonitor;
        private readonly IMessenger _messenger;
        private readonly ILibraryPresetProvider? _presetProvider;
        private readonly IReadOnlyList<FacetPresetBinding> _presetFacetBindings;
        private List<LibraryItemViewModel> _allMediaItems = [];
        private DispatcherTimer? _filterDebounceTimer;
        // Flag to track if the sort order changed so the grid only scrolls to top on explicit sort changes.
        private bool _isSortOrderChanging = true;
        private bool _isSyncingSort;
        private bool _isApplyingBatchChanges;
        private bool _isPageActive = true;
        private bool _hasPendingFilterRefresh;
        private bool _hasPendingExternalReload;
        private bool _isReloading;
        private bool _reloadAgainRequested;
        private int? _activePresetId;
        private int _randomSortSeed = Random.Shared.Next();

        public LibraryViewModel(
            IMediaProvider mediaProvider,
            IMediaSceneProvider sceneProvider,
            IServiceProvider serviceProvider,
            IDatabaseChangeMonitor? databaseChangeMonitor = null,
            IMessenger? messenger = null,
            ILibraryPresetProvider? presetProvider = null)
        {
            _mediaProvider = mediaProvider;
            _sceneProvider = sceneProvider;
            _serviceProvider = serviceProvider;
            _databaseChangeMonitor = databaseChangeMonitor;
            _messenger = messenger ?? WeakReferenceMessenger.Default;
            _presetProvider = presetProvider;

            TagsFacet = new FacetFilterGroup("Tags", "Tag", QueueApplyFilter);
            GenresFacet = new FacetFilterGroup("Genres", "Genre", QueueApplyFilter);
            CastFacet = new FacetFilterGroup("Cast", "Cast", QueueApplyFilter);
            StudiosFacet = new FacetFilterGroup("Studios", "Studio", QueueApplyFilter);
            SeriesFacet = new FacetFilterGroup("Series", "Series", QueueApplyFilter);

            AllFacets = [TagsFacet, GenresFacet, CastFacet, StudiosFacet, SeriesFacet];
            _presetFacetBindings =
            [
                new(TagsFacet, model => model.Tags, (model, selection) => model.Tags = selection),
                new(GenresFacet, model => model.Genres, (model, selection) => model.Genres = selection),
                new(CastFacet, model => model.Cast, (model, selection) => model.Cast = selection),
                new(StudiosFacet, model => model.Studios, (model, selection) => model.Studios = selection),
                new(SeriesFacet, model => model.Series, (model, selection) => model.Series = selection)
            ];
            SelectedSortField = SortFieldOptions.First(option => option.Field == LibrarySortField.AddedDate);

            _messenger.RegisterAll(this);

            if (_databaseChangeMonitor != null)
            {
                _databaseChangeMonitor.DatabaseChangedExternally += OnDatabaseChangedExternally;
            }
        }

        public event EventHandler? SortOrderApplied;

        public FacetFilterGroup TagsFacet { get; }
        public FacetFilterGroup GenresFacet { get; }
        public FacetFilterGroup CastFacet { get; }
        public FacetFilterGroup StudiosFacet { get; }
        public FacetFilterGroup SeriesFacet { get; }
        public IReadOnlyList<FacetFilterGroup> AllFacets { get; }

        // Compatibility accessors for existing tests and bindings
        public BatchObservableCollection<FilterItem> TagFilterItems => TagsFacet.FilteredItems;
        public BatchObservableCollection<FilterItem> GenreFilterItems => GenresFacet.FilteredItems;

        public string TagSearchText
        {
            get => TagsFacet.SearchText;
            set => TagsFacet.SearchText = value;
        }

        public string GenreSearchText
        {
            get => GenresFacet.SearchText;
            set => GenresFacet.SearchText = value;
        }

        public BatchObservableCollection<LibraryItemViewModel> MediaItems { get; } = [];

        public ObservableCollection<ActiveFilterChip> ActiveFilterChips { get; } = [];

        public BatchObservableCollection<LibraryPresetViewModel> Presets { get; } = [];

        public IReadOnlyList<LibrarySortOption> SortOptions { get; } = LibrarySortOption.All;
        public IReadOnlyList<LibrarySortFieldOption> SortFieldOptions { get; } = LibrarySortOption.FieldOptions;

        public Task<IEnumerable<MediaScene>> GetMediaScenesAsync(string mediaId) => _sceneProvider.GetMediaScenesAsync(mediaId);

        [ObservableProperty]
        public partial bool IsDataLoaded { get; set; }

        [ObservableProperty]
        public partial bool IsLoading { get; set; }

        [ObservableProperty]
        public partial string SearchText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial LibrarySortOption SortOrder { get; set; } = LibrarySortOption.AddedDateDescending;

        [ObservableProperty]
        public partial LibrarySortFieldOption SelectedSortField { get; set; }

        [ObservableProperty]
        public partial bool IsSortDescending { get; set; } = true;

        public bool IsNotRandomSort => SelectedSortField.Field != LibrarySortField.Random;
        public string SortDirectionGlyph => IsSortDescending ? "\uE74B" : "\uE74A";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRatingFilterActive), nameof(RatingButtonLabel), nameof(RatingRangeDisplay))]
        public partial double RatingMin { get; set; } = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRatingFilterActive), nameof(RatingButtonLabel), nameof(RatingRangeDisplay))]
        public partial double RatingMax { get; set; } = 5;

        public bool IsRatingFilterActive => RatingMin > 0 || RatingMax < 5;
        public string RatingButtonLabel => IsRatingFilterActive ? $"{(int)RatingMin}–{(int)RatingMax}" : "Rating";
        public string RatingRangeDisplay => $"★ {(int)RatingMin} – {(int)RatingMax}";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRuntimeFilterActive), nameof(RuntimeButtonLabel), nameof(RuntimeRangeDisplay))]
        public partial double RuntimeMaxBound { get; set; } = 180;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRuntimeFilterActive), nameof(RuntimeButtonLabel), nameof(RuntimeRangeDisplay))]
        public partial double RuntimeMin { get; set; } = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRuntimeFilterActive), nameof(RuntimeButtonLabel), nameof(RuntimeRangeDisplay))]
        public partial double RuntimeMax { get; set; } = 180;

        public bool IsRuntimeFilterActive => RuntimeMin > 0 || RuntimeMax < RuntimeMaxBound;
        public string RuntimeButtonLabel => IsRuntimeFilterActive ? $"{(int)RuntimeMin}–{(int)RuntimeMax}m" : "Runtime";
        public string RuntimeRangeDisplay => $"{(int)RuntimeMin}m – {(int)RuntimeMax}m";

        public string ItemCountText => MediaItems.Count == _allMediaItems.Count
            ? $"{_allMediaItems.Count} items"
            : $"{MediaItems.Count} of {_allMediaItems.Count}";

        public bool HasActiveFilters => ActiveFilterChips.Count > 0;
        public bool HasPresets => Presets.Count > 0;
        public bool ShowPresetsBar => !HasActiveFilters && HasPresets;
        public bool ShowFilterOrPresetsBar => HasActiveFilters || HasPresets;
        public bool IsEmptyResult => IsDataLoaded && MediaItems.Count == 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SavePresetButtonText), nameof(CanSaveCurrentPreset))]
        public partial bool IsCurrentFilterSavedAsPreset { get; set; }

        public string SavePresetButtonText => IsCurrentFilterSavedAsPreset ? "Saved" : "Save preset";
        public bool CanSaveCurrentPreset => HasActiveFilters && !IsCurrentFilterSavedAsPreset && _presetProvider != null;

        partial void OnSearchTextChanged(string value) => QueueApplyFilter();
        partial void OnRatingMinChanged(double value) => QueueApplyFilter();
        partial void OnRatingMaxChanged(double value) => QueueApplyFilter();
        partial void OnRuntimeMinChanged(double value) => QueueApplyFilter();
        partial void OnRuntimeMaxChanged(double value) => QueueApplyFilter();

        partial void OnSortOrderChanged(LibrarySortOption value)
        {
            if (!_isSyncingSort)
            {
                _isSyncingSort = true;
                (var field, var descending) = DecomposeSortOption(value);
                SelectedSortField = SortFieldOptions.First(option => option.Field == field);
                IsSortDescending = descending;
                _isSyncingSort = false;
            }

            OnPropertyChanged(nameof(IsNotRandomSort));
            OnPropertyChanged(nameof(SortDirectionGlyph));
            if (value == LibrarySortOption.Random)
            {
                RotateRandomSortSeed();
            }

            _isSortOrderChanging = true;
            QueueApplyFilter();
        }

        partial void OnSelectedSortFieldChanged(LibrarySortFieldOption value)
        {
            OnPropertyChanged(nameof(IsNotRandomSort));
            if (_isSyncingSort)
            {
                return;
            }

            _isSyncingSort = true;
            SortOrder = ComposeSortOption(value.Field, IsSortDescending);
            _isSyncingSort = false;
        }

        partial void OnIsSortDescendingChanged(bool value)
        {
            OnPropertyChanged(nameof(SortDirectionGlyph));
            if (_isSyncingSort)
            {
                return;
            }

            _isSyncingSort = true;
            SortOrder = ComposeSortOption(SelectedSortField.Field, value);
            _isSyncingSort = false;
        }

        [RelayCommand]
        private void ToggleSortDirection() => IsSortDescending = !IsSortDescending;

        [RelayCommand]
        private void SelectSortField(LibrarySortFieldOption option) => SelectedSortField = option;

        [RelayCommand]
        private void Shuffle()
        {
            _isSortOrderChanging = true;
            if (SelectedSortField.Field != LibrarySortField.Random)
            {
                SelectedSortField = SortFieldOptions.First(option => option.Field == LibrarySortField.Random);
            }
            else
            {
                RotateRandomSortSeed();
                QueueApplyFilter();
            }
        }

        [RelayCommand]
        private void ResetRatingFilter()
        {
            RatingMin = 0;
            RatingMax = 5;
        }

        [RelayCommand]
        private void ResetRuntimeFilter()
        {
            RuntimeMin = 0;
            RuntimeMax = RuntimeMaxBound;
        }

        [RelayCommand]
        private void ClearAllFilters()
        {
            _isApplyingBatchChanges = true;
            try
            {
                SearchText = string.Empty;
                foreach (var facet in AllFacets)
                {
                    facet.ResetSelectionsSilently();
                }

                RatingMin = 0;
                RatingMax = 5;
                RuntimeMin = 0;
                RuntimeMax = RuntimeMaxBound;
            }
            finally
            {
                _isApplyingBatchChanges = false;
            }

            UpdateActivePresetHighlight(null);
            IsCurrentFilterSavedAsPreset = false;
            ApplyFilter();
        }

        [RelayCommand]
        public async Task SaveCurrentAsPresetAsync()
        {
            if (_presetProvider == null || !HasActiveFilters || IsCurrentFilterSavedAsPreset)
            {
                return;
            }

            var autoName = BuildDefaultPresetName();
            var uniqueName = EnsureUniquePresetName(autoName);
            var model = CaptureCurrentPresetModel(uniqueName, isCustomName: false);

            LibraryPresetModel created;
            using (_databaseChangeMonitor?.BeginInternalWrite())
            {
                created = await _presetProvider.CreatePresetAsync(model);
            }

            var updatedPresets = Presets.ToList();
            updatedPresets.Add(CreatePresetViewModel(created));
            Presets.ReplaceAll(updatedPresets);

            IsCurrentFilterSavedAsPreset = true;
            NotifyPresetBarProperties();
        }

        [RelayCommand]
        public void ApplyPreset(LibraryPresetViewModel? preset)
        {
            if (preset == null)
            {
                return;
            }

            var model = preset.Model;
            _filterDebounceTimer?.Stop();
            _isApplyingBatchChanges = true;
            try
            {
                SearchText = model.SearchText;
                RatingMin = Math.Clamp(model.RatingMin, 0, 5);
                RatingMax = Math.Clamp(model.RatingMax, RatingMin, 5);

                var targetRuntimeMax = model.RuntimeMax ?? RuntimeMaxBound;
                RuntimeMax = Math.Clamp(targetRuntimeMax, 0, RuntimeMaxBound);
                RuntimeMin = Math.Clamp(model.RuntimeMin, 0, RuntimeMax);

                foreach (var binding in _presetFacetBindings)
                {
                    var selection = binding.Getter(model);
                    binding.Facet.RestoreSelectionsSilently(selection.Included, selection.Excluded);
                }

                var targetField = Enum.TryParse<LibrarySortField>(model.SortField, out var parsedField)
                    ? parsedField
                    : LibrarySortField.AddedDate;

                _isSyncingSort = true;
                SelectedSortField = SortFieldOptions.First(option => option.Field == targetField);
                IsSortDescending = model.IsSortDescending;
                var composedSort = ComposeSortOption(targetField, model.IsSortDescending);
                if (SortOrder != composedSort)
                {
                    SortOrder = composedSort;
                    _isSortOrderChanging = true;
                }
                _isSyncingSort = false;
            }
            finally
            {
                _isApplyingBatchChanges = false;
            }

            UpdateActivePresetHighlight(preset.Id);
            ApplyFilter();
            IsCurrentFilterSavedAsPreset = true;
        }

        public bool TryValidatePresetName(string candidateName, int? excludingPresetId, out string? errorMessage)
        {
            var trimmed = candidateName.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                errorMessage = "Preset name cannot be empty.";
                return false;
            }

            var duplicateExists = Presets.Any(preset =>
                preset.Id != excludingPresetId &&
                string.Equals(preset.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase));

            if (duplicateExists)
            {
                errorMessage = "A preset with this name already exists.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public async Task<bool> RenamePresetAsync(LibraryPresetViewModel preset, string newName)
        {
            if (_presetProvider == null || !TryValidatePresetName(newName, preset.Id, out _))
            {
                return false;
            }

            var trimmed = newName.Trim();
            preset.Model.Name = trimmed;
            preset.Model.IsCustomName = true;

            using (_databaseChangeMonitor?.BeginInternalWrite())
            {
                await _presetProvider.UpdatePresetAsync(preset.Model);
            }

            preset.UpdateFrom(preset.Model, BuildPresetSummary(preset.Model), BuildPresetTooltip(preset.Model));
            return true;
        }

        [RelayCommand]
        public async Task UpdatePresetFromCurrentAsync(LibraryPresetViewModel? preset)
        {
            if (_presetProvider == null || preset == null)
            {
                return;
            }

            var isCustomName = preset.Model.IsCustomName;
            var targetName = isCustomName
                ? preset.Model.Name
                : EnsureUniquePresetName(BuildDefaultPresetName(), excludingPresetId: preset.Id);

            var updatedModel = CaptureCurrentPresetModel(targetName, isCustomName);
            updatedModel.Id = preset.Id;
            updatedModel.CreatedAt = preset.Model.CreatedAt;

            using (_databaseChangeMonitor?.BeginInternalWrite())
            {
                await _presetProvider.UpdatePresetAsync(updatedModel);
            }

            preset.UpdateFrom(updatedModel, BuildPresetSummary(updatedModel), BuildPresetTooltip(updatedModel));
            UpdateActivePresetHighlight(preset.Id);
            IsCurrentFilterSavedAsPreset = true;
        }

        [RelayCommand]
        public async Task DeletePresetAsync(LibraryPresetViewModel? preset)
        {
            if (_presetProvider == null || preset == null)
            {
                return;
            }

            using (_databaseChangeMonitor?.BeginInternalWrite())
            {
                await _presetProvider.DeletePresetAsync(preset.Id);
            }

            if (_activePresetId == preset.Id)
            {
                _activePresetId = null;
            }

            Presets.ReplaceAll(Presets.Where(entry => entry.Id != preset.Id));
            IsCurrentFilterSavedAsPreset = false;
            NotifyPresetBarProperties();
        }

        public string BuildDefaultPresetName() => BuildPresetSummary(CaptureCurrentPresetModel(string.Empty, isCustomName: false));

        public async Task LoadDataAsync()
        {
            _databaseChangeMonitor?.Start();
            await ReloadLibraryAsync();
        }

        public async Task SetPageActiveAsync(bool isActive)
        {
            _isPageActive = isActive;
            if (!isActive || !IsDataLoaded)
            {
                return;
            }

            if (_hasPendingExternalReload)
            {
                await ReloadLibraryAsync();
            }
            else if (_hasPendingFilterRefresh)
            {
                _hasPendingFilterRefresh = false;
                ApplyFilter();
            }
        }

        [RelayCommand]
        public async Task ReloadLibraryAsync()
        {
            if (_isReloading)
            {
                _reloadAgainRequested = true;
                return;
            }

            _isReloading = true;
            IsLoading = true;
            try
            {
                do
                {
                    _reloadAgainRequested = false;
                    await ReloadCoreAsync();
                }
                while (_reloadAgainRequested);
            }
            finally
            {
                _isReloading = false;
                IsLoading = false;
            }
        }

        private async Task ReloadCoreAsync()
        {
            var isInitialLoad = !IsDataLoaded;
            var wasRuntimeUnbounded = isInitialLoad || !IsRuntimeFilterActive || RuntimeMax >= RuntimeMaxBound;

            var savedFacetSelections = AllFacets.ToDictionary(
                facet => facet,
                facet => (Required: (IReadOnlyCollection<string>)facet.RequiredValues, Excluded: (IReadOnlyCollection<string>)facet.ExcludedValues));

            var itemsTask = _mediaProvider.GetLibraryItemsAsync();
            var tagsTask = _mediaProvider.GetUserTagsAsync();
            var genresTask = _mediaProvider.GetGenresAsync();
            var castTask = _mediaProvider.GetCastAsync();
            var studiosTask = _mediaProvider.GetStudiosAsync();
            var seriesTask = _mediaProvider.GetSeriesAsync();
            var presetsTask = _presetProvider?.GetPresetsAsync() ?? Task.FromResult<IReadOnlyList<LibraryPresetModel>>([]);

            await Task.WhenAll(itemsTask, tagsTask, genresTask, castTask, studiosTask, seriesTask, presetsTask);

            var items = await itemsTask;
            var existingById = _allMediaItems.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
            var reconciledItems = new List<LibraryItemViewModel>();

            foreach (var item in items)
            {
                if (existingById.TryGetValue(item.MediaId, out var existingViewModel))
                {
                    existingViewModel.UpdateFrom(item);
                    reconciledItems.Add(existingViewModel);
                }
                else
                {
                    reconciledItems.Add(ActivatorUtilities.CreateInstance<LibraryItemViewModel>(_serviceProvider, item));
                }
            }

            _allMediaItems = reconciledItems;

            var maxRuntime = _allMediaItems
                .Where(item => item.RuntimeMinutes.HasValue)
                .Select(item => item.RuntimeMinutes!.Value)
                .DefaultIfEmpty(180)
                .Max();
            var roundedMaxRuntime = Math.Max(180, ((maxRuntime + 14) / 15) * 15);
            RuntimeMaxBound = roundedMaxRuntime;

            if (wasRuntimeUnbounded)
            {
                RuntimeMax = roundedMaxRuntime;
            }
            else
            {
                RuntimeMax = Math.Min(RuntimeMax, roundedMaxRuntime);
                RuntimeMin = Math.Min(RuntimeMin, RuntimeMax);
            }

            TagsFacet.Populate(await tagsTask, name => _allMediaItems.Count(item => item.LibraryItem.UserTags.Contains(name, StringComparer.OrdinalIgnoreCase)));
            GenresFacet.Populate(await genresTask, name => _allMediaItems.Count(item => item.LibraryItem.Genres.Contains(name, StringComparer.OrdinalIgnoreCase)));
            CastFacet.Populate(await castTask, name => _allMediaItems.Count(item => item.LibraryItem.Cast.Contains(name, StringComparer.OrdinalIgnoreCase)));
            StudiosFacet.Populate(await studiosTask, name => _allMediaItems.Count(item => item.LibraryItem.Studios.Contains(name, StringComparer.OrdinalIgnoreCase)));
            SeriesFacet.Populate(await seriesTask, name => _allMediaItems.Count(item => item.LibraryItem.Series.Contains(name, StringComparer.OrdinalIgnoreCase)));

            if (!isInitialLoad)
            {
                foreach (var facet in AllFacets)
                {
                    var (required, excluded) = savedFacetSelections[facet];
                    facet.RestoreSelectionsSilently(required, excluded);
                }
            }

            var loadedPresets = await presetsTask;
            Presets.ReplaceAll(loadedPresets.Select(CreatePresetViewModel));
            UpdateActivePresetHighlight(_activePresetId);

            _hasPendingExternalReload = false;
            _hasPendingFilterRefresh = false;

            ApplyFilter();
            IsDataLoaded = true;
            NotifyPresetBarProperties();
            OnPropertyChanged(nameof(ItemCountText));
            OnPropertyChanged(nameof(IsEmptyResult));
        }

        private async void OnDatabaseChangedExternally(object? sender, EventArgs eventArgs)
        {
            if (!_isPageActive)
            {
                _hasPendingExternalReload = true;
                return;
            }

            try
            {
                await ReloadLibraryAsync();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"External library reload failed: {exception.Message}");
            }
        }

        public void Receive(MediaRatingChangedMessage message)
        {
            var item = FindMediaItem(message.MediaId);
            if (item == null || item.Rating == message.Rating)
            {
                return;
            }

            item.Rating = message.Rating;
            if (IsRatingFilterActive || SelectedSortField.Field == LibrarySortField.Rating)
            {
                ScheduleOrApplyRefresh();
            }
        }

        public void Receive(MediaUserTagAddedMessage message)
        {
            var item = FindMediaItem(message.MediaId);
            if (item == null || !item.AddUserTag(message.TagName))
            {
                return;
            }

            var count = _allMediaItems.Count(media => media.LibraryItem.UserTags.Contains(message.TagName, StringComparer.OrdinalIgnoreCase));
            TagsFacet.SyncItemCount(message.TagName, count, insertIfMissing: true);

            if (TagsFacet.HasActiveFilters || !string.IsNullOrWhiteSpace(SearchText))
            {
                ScheduleOrApplyRefresh();
            }
        }

        public void Receive(MediaUserTagRemovedMessage message)
        {
            var item = FindMediaItem(message.MediaId);
            if (item == null || !item.RemoveUserTag(message.TagName))
            {
                return;
            }

            var count = _allMediaItems.Count(media => media.LibraryItem.UserTags.Contains(message.TagName, StringComparer.OrdinalIgnoreCase));
            TagsFacet.SyncItemCount(message.TagName, count, insertIfMissing: false);

            if (TagsFacet.HasActiveFilters || !string.IsNullOrWhiteSpace(SearchText))
            {
                ScheduleOrApplyRefresh();
            }
        }

        public void Receive(MediaHighlightCountChangedMessage message)
        {
            var item = FindMediaItem(message.MediaId);
            if (item == null || item.HighlightCount == message.HighlightCount)
            {
                return;
            }

            item.HighlightCount = message.HighlightCount;
            if (SelectedSortField.Field == LibrarySortField.Highlights)
            {
                ScheduleOrApplyRefresh();
            }
        }

        private LibraryItemViewModel? FindMediaItem(string mediaId) =>
            _allMediaItems.FirstOrDefault(item => string.Equals(item.Id, mediaId, StringComparison.OrdinalIgnoreCase));

        private void ScheduleOrApplyRefresh()
        {
            if (_isPageActive)
            {
                ApplyFilter();
            }
            else
            {
                _hasPendingFilterRefresh = true;
            }
        }

        public void ApplyFilterDirect() => ApplyFilter();

        private void QueueApplyFilter()
        {
            if (_isApplyingBatchChanges)
            {
                return;
            }

            UpdateActivePresetHighlight(null);
            IsCurrentFilterSavedAsPreset = false;

            if (Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread() == null)
            {
                ApplyFilter();
                return;
            }

            try
            {
                if (_filterDebounceTimer == null)
                {
                    _filterDebounceTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(150)
                    };
                    _filterDebounceTimer.Tick += (sender, e) =>
                    {
                        _filterDebounceTimer.Stop();
                        ApplyFilter();
                    };
                }

                _filterDebounceTimer.Stop();
                _filterDebounceTimer.Start();
            }
            catch
            {
                ApplyFilter();
            }
        }

        private void ApplyFilter()
        {
            var filteredItems = _allMediaItems.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchTerms = SearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var term in searchTerms)
                {
                    var normalizedTerm = NormalizeForSearch(term);
                    filteredItems = filteredItems.Where(item =>
                        item.NormalizedId.Contains(normalizedTerm, StringComparison.Ordinal) ||
                        (item.OriginalTitle != null && item.OriginalTitle.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        item.LibraryItem.UserTags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        item.LibraryItem.Genres.Any(genre => genre.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        item.LibraryItem.Cast.Any(person => person.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        item.LibraryItem.Studios.Any(studio => studio.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        item.LibraryItem.Series.Any(series => series.Contains(term, StringComparison.OrdinalIgnoreCase))
                    );
                }
            }

            filteredItems = ApplyFacetFilter(filteredItems, TagsFacet, item => item.LibraryItem.UserTags);
            filteredItems = ApplyFacetFilter(filteredItems, GenresFacet, item => item.LibraryItem.Genres);
            filteredItems = ApplyFacetFilter(filteredItems, CastFacet, item => item.LibraryItem.Cast);
            filteredItems = ApplyFacetFilter(filteredItems, StudiosFacet, item => item.LibraryItem.Studios);
            filteredItems = ApplyFacetFilter(filteredItems, SeriesFacet, item => item.LibraryItem.Series);

            if (IsRatingFilterActive)
            {
                var minRating = (int)RatingMin;
                var maxRating = (int)RatingMax;
                filteredItems = filteredItems.Where(item => item.Rating >= minRating && item.Rating <= maxRating);
            }

            if (IsRuntimeFilterActive)
            {
                var minRuntime = (int)RuntimeMin;
                var maxRuntime = (int)RuntimeMax;
                filteredItems = filteredItems.Where(item =>
                    item.RuntimeMinutes.HasValue &&
                    item.RuntimeMinutes.Value >= minRuntime &&
                    item.RuntimeMinutes.Value <= maxRuntime);
            }

            filteredItems = SortOrder switch
            {
                _ when SortOrder == LibrarySortOption.AlphabeticalAscending => filteredItems.OrderBy(item => item.Title),
                _ when SortOrder == LibrarySortOption.AlphabeticalDescending => filteredItems.OrderByDescending(item => item.Title),
                _ when SortOrder == LibrarySortOption.ReleaseDateAscending => filteredItems.OrderBy(item => item.ReleaseDate ?? DateOnly.MinValue),
                _ when SortOrder == LibrarySortOption.ReleaseDateDescending => filteredItems.OrderByDescending(item => item.ReleaseDate ?? DateOnly.MinValue),
                _ when SortOrder == LibrarySortOption.AddedDateAscending => filteredItems.OrderBy(item => item.LibraryItem.CreatedAt ?? DateTime.MinValue),
                _ when SortOrder == LibrarySortOption.AddedDateDescending => filteredItems.OrderByDescending(item => item.LibraryItem.CreatedAt ?? DateTime.MinValue),
                _ when SortOrder == LibrarySortOption.HighlightsAscending => filteredItems.OrderBy(item => item.HighlightCount),
                _ when SortOrder == LibrarySortOption.HighlightsDescending => filteredItems.OrderByDescending(item => item.HighlightCount),
                _ when SortOrder == LibrarySortOption.RatingAscending => filteredItems.OrderBy(item => item.Rating),
                _ when SortOrder == LibrarySortOption.RatingDescending => filteredItems.OrderByDescending(item => item.Rating),
                _ when SortOrder == LibrarySortOption.RuntimeAscending => filteredItems.OrderBy(item => item.RuntimeMinutes ?? 0),
                _ when SortOrder == LibrarySortOption.RuntimeDescending => filteredItems.OrderByDescending(item => item.RuntimeMinutes ?? 0),
                _ when SortOrder == LibrarySortOption.Random => filteredItems.OrderBy(item => ComputeShuffleKey(item.Id, _randomSortSeed)),
                _ => filteredItems.OrderBy(item => item.Title)
            };

            var targetList = filteredItems.ToList();
            var sortChanged = _isSortOrderChanging;
            _isSortOrderChanging = false;

            MediaItems.ReplaceAll(targetList);

            if (sortChanged)
            {
                SortOrderApplied?.Invoke(this, EventArgs.Empty);
            }

            RebuildActiveFilterChips();
            OnPropertyChanged(nameof(ItemCountText));
            OnPropertyChanged(nameof(IsEmptyResult));
        }

        private static IEnumerable<LibraryItemViewModel> ApplyFacetFilter(
            IEnumerable<LibraryItemViewModel> items,
            FacetFilterGroup facet,
            Func<LibraryItemViewModel, IReadOnlyList<string>> selector)
        {
            var required = facet.RequiredValues;
            if (required.Count > 0)
            {
                items = items.Where(item => required.All(value => selector(item).Contains(value, StringComparer.OrdinalIgnoreCase)));
            }

            var excluded = facet.ExcludedValues;
            if (excluded.Count > 0)
            {
                items = items.Where(item => excluded.All(value => !selector(item).Contains(value, StringComparer.OrdinalIgnoreCase)));
            }

            return items;
        }

        private void RebuildActiveFilterChips()
        {
            ActiveFilterChips.Clear();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                ActiveFilterChips.Add(new ActiveFilterChip(
                    $"Search: \"{SearchText.Trim()}\"",
                    false,
                    false,
                    true,
                    () => SearchText = string.Empty));
            }

            foreach (var facet in AllFacets)
            {
                foreach (var filterItem in facet.ActiveItems)
                {
                    var isInclude = filterItem.IsSelected == true;
                    var prefix = isInclude ? "+" : "−";
                    ActiveFilterChips.Add(new ActiveFilterChip(
                        $"{prefix} {facet.CategoryPrefix}: {filterItem.Name}",
                        isInclude,
                        !isInclude,
                        false,
                        () => filterItem.IsSelected = false));
                }
            }

            if (IsRatingFilterActive)
            {
                ActiveFilterChips.Add(new ActiveFilterChip(
                    RatingRangeDisplay,
                    false,
                    false,
                    true,
                    ResetRatingFilter));
            }

            if (IsRuntimeFilterActive)
            {
                ActiveFilterChips.Add(new ActiveFilterChip(
                    $"⏱ {RuntimeRangeDisplay}",
                    false,
                    false,
                    true,
                    ResetRuntimeFilter));
            }

            OnPropertyChanged(nameof(HasActiveFilters));
            OnPropertyChanged(nameof(CanSaveCurrentPreset));
            NotifyPresetBarProperties();
        }

        private void NotifyPresetBarProperties()
        {
            OnPropertyChanged(nameof(HasPresets));
            OnPropertyChanged(nameof(ShowPresetsBar));
            OnPropertyChanged(nameof(ShowFilterOrPresetsBar));
            OnPropertyChanged(nameof(CanSaveCurrentPreset));
        }

        private void UpdateActivePresetHighlight(int? activePresetId)
        {
            _activePresetId = activePresetId;
            foreach (var preset in Presets)
            {
                preset.IsActive = activePresetId.HasValue && preset.Id == activePresetId.Value;
            }
        }

        private LibraryPresetViewModel CreatePresetViewModel(LibraryPresetModel model)
            => new(model, BuildPresetSummary(model), BuildPresetTooltip(model));

        private LibraryPresetModel CaptureCurrentPresetModel(string name, bool isCustomName)
        {
            var model = new LibraryPresetModel
            {
                Name = name,
                IsCustomName = isCustomName,
                SearchText = SearchText.Trim(),
                SortField = SelectedSortField.Field.ToString(),
                IsSortDescending = IsSortDescending,
                RatingMin = RatingMin,
                RatingMax = RatingMax,
                RuntimeMin = RuntimeMin,
                RuntimeMax = IsRuntimeFilterActive && RuntimeMax < RuntimeMaxBound ? RuntimeMax : null
            };

            foreach (var binding in _presetFacetBindings)
            {
                binding.Setter(model, new LibraryPresetFacetSelection
                {
                    Included = [.. binding.Facet.RequiredValues],
                    Excluded = [.. binding.Facet.ExcludedValues]
                });
            }

            return model;
        }

        private string EnsureUniquePresetName(string baseName, int? excludingPresetId = null)
        {
            if (TryValidatePresetName(baseName, excludingPresetId, out _))
            {
                return baseName;
            }

            var suffix = 2;
            while (true)
            {
                var candidate = $"{baseName} ({suffix})";
                if (TryValidatePresetName(candidate, excludingPresetId, out _))
                {
                    return candidate;
                }

                suffix++;
            }
        }

        private string BuildPresetSummary(LibraryPresetModel model)
        {
            var tokens = new List<string>();

            if (!string.IsNullOrWhiteSpace(model.SearchText))
            {
                tokens.Add($"\"{model.SearchText.Trim()}\"");
            }

            foreach (var binding in _presetFacetBindings)
            {
                var selection = binding.Getter(model);
                foreach (var included in selection.Included)
                {
                    tokens.Add($"+{included}");
                }

                foreach (var excluded in selection.Excluded)
                {
                    tokens.Add($"−{excluded}");
                }
            }

            if (model.RatingMin > 0 || model.RatingMax < 5)
            {
                tokens.Add($"★ {(int)model.RatingMin}–{(int)model.RatingMax}");
            }

            if (model.RuntimeMin > 0 || model.RuntimeMax.HasValue)
            {
                var maxLabel = model.RuntimeMax.HasValue ? $"{(int)model.RuntimeMax.Value}m" : "max";
                tokens.Add($"⏱ {(int)model.RuntimeMin}m–{maxLabel}");
            }

            var sortField = Enum.TryParse<LibrarySortField>(model.SortField, out var parsed)
                ? parsed
                : LibrarySortField.AddedDate;
            var sortFieldDisplay = SortFieldOptions.First(option => option.Field == sortField).DisplayName;
            var sortLabel = sortField == LibrarySortField.Random
                ? sortFieldDisplay
                : $"{sortFieldDisplay} {(model.IsSortDescending ? "↓" : "↑")}";

            if (tokens.Count == 0)
            {
                return sortLabel;
            }

            const int maxSummaryTokens = 3;
            var displayedTokens = tokens.Take(maxSummaryTokens).ToList();
            var summary = string.Join(", ", displayedTokens);
            if (tokens.Count > maxSummaryTokens)
            {
                summary += $" (+{tokens.Count - maxSummaryTokens})";
            }

            var isDefaultSort = sortField == LibrarySortField.AddedDate && model.IsSortDescending;
            return isDefaultSort ? summary : $"{summary} • {sortLabel}";
        }

        private string BuildPresetTooltip(LibraryPresetModel model)
        {
            var lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(model.SearchText))
            {
                lines.Add($"Search: \"{model.SearchText.Trim()}\"");
            }

            foreach (var binding in _presetFacetBindings)
            {
                var selection = binding.Getter(model);
                var parts = new List<string>();
                parts.AddRange(selection.Included.Select(value => $"+{value}"));
                parts.AddRange(selection.Excluded.Select(value => $"−{value}"));
                if (parts.Count > 0)
                {
                    lines.Add($"{binding.Facet.Label}: {string.Join(", ", parts)}");
                }
            }

            if (model.RatingMin > 0 || model.RatingMax < 5)
            {
                lines.Add($"Rating: {(int)model.RatingMin}–{(int)model.RatingMax} stars");
            }

            if (model.RuntimeMin > 0 || model.RuntimeMax.HasValue)
            {
                var maxLabel = model.RuntimeMax.HasValue ? $"{(int)model.RuntimeMax.Value}m" : "max";
                lines.Add($"Runtime: {(int)model.RuntimeMin}m–{maxLabel}");
            }

            var sortField = Enum.TryParse<LibrarySortField>(model.SortField, out var parsed)
                ? parsed
                : LibrarySortField.AddedDate;
            var sortFieldDisplay = SortFieldOptions.First(option => option.Field == sortField).DisplayName;
            var sortLabel = sortField == LibrarySortField.Random
                ? sortFieldDisplay
                : $"{sortFieldDisplay} {(model.IsSortDescending ? "↓" : "↑")}";
            lines.Add($"Sort: {sortLabel}");

            return string.Join(Environment.NewLine, lines);
        }

        private sealed record FacetPresetBinding(
            FacetFilterGroup Facet,
            Func<LibraryPresetModel, LibraryPresetFacetSelection> Getter,
            Action<LibraryPresetModel, LibraryPresetFacetSelection> Setter);

        private static (LibrarySortField Field, bool Descending) DecomposeSortOption(LibrarySortOption option)
        {
            if (option == LibrarySortOption.AlphabeticalAscending)
            {
                return (LibrarySortField.Title, false);
            }

            if (option == LibrarySortOption.AlphabeticalDescending)
            {
                return (LibrarySortField.Title, true);
            }

            if (option == LibrarySortOption.ReleaseDateAscending)
            {
                return (LibrarySortField.ReleaseDate, false);
            }

            if (option == LibrarySortOption.ReleaseDateDescending)
            {
                return (LibrarySortField.ReleaseDate, true);
            }

            if (option == LibrarySortOption.AddedDateAscending)
            {
                return (LibrarySortField.AddedDate, false);
            }

            if (option == LibrarySortOption.AddedDateDescending)
            {
                return (LibrarySortField.AddedDate, true);
            }

            if (option == LibrarySortOption.HighlightsAscending)
            {
                return (LibrarySortField.Highlights, false);
            }

            if (option == LibrarySortOption.HighlightsDescending)
            {
                return (LibrarySortField.Highlights, true);
            }

            if (option == LibrarySortOption.RatingAscending)
            {
                return (LibrarySortField.Rating, false);
            }

            if (option == LibrarySortOption.RatingDescending)
            {
                return (LibrarySortField.Rating, true);
            }

            if (option == LibrarySortOption.RuntimeAscending)
            {
                return (LibrarySortField.Runtime, false);
            }

            if (option == LibrarySortOption.RuntimeDescending)
            {
                return (LibrarySortField.Runtime, true);
            }

            return (LibrarySortField.Random, true);
        }

        private static LibrarySortOption ComposeSortOption(LibrarySortField field, bool descending) => field switch
        {
            LibrarySortField.Title => descending ? LibrarySortOption.AlphabeticalDescending : LibrarySortOption.AlphabeticalAscending,
            LibrarySortField.ReleaseDate => descending ? LibrarySortOption.ReleaseDateDescending : LibrarySortOption.ReleaseDateAscending,
            LibrarySortField.AddedDate => descending ? LibrarySortOption.AddedDateDescending : LibrarySortOption.AddedDateAscending,
            LibrarySortField.Highlights => descending ? LibrarySortOption.HighlightsDescending : LibrarySortOption.HighlightsAscending,
            LibrarySortField.Rating => descending ? LibrarySortOption.RatingDescending : LibrarySortOption.RatingAscending,
            LibrarySortField.Runtime => descending ? LibrarySortOption.RuntimeDescending : LibrarySortOption.RuntimeAscending,
            LibrarySortField.Random => LibrarySortOption.Random,
            _ => LibrarySortOption.AddedDateDescending
        };

        private void RotateRandomSortSeed()
        {
            var nextSeed = Random.Shared.Next();
            if (nextSeed == _randomSortSeed)
            {
                nextSeed = unchecked(_randomSortSeed + 1);
            }

            _randomSortSeed = nextSeed;
        }

        private static ulong ComputeShuffleKey(string mediaId, int seed)
        {
            const ulong fnvOffsetBasis = 14695981039346656037UL;
            const ulong fnvPrime = 1099511628211UL;

            var hash = fnvOffsetBasis ^ unchecked((uint)seed);
            foreach (var character in mediaId)
            {
                hash ^= char.ToUpperInvariant(character);
                hash *= fnvPrime;
            }

            // SplitMix64 finalizer for uniform dispersion across sequential IDs
            hash ^= hash >> 30;
            hash *= 0xbf58476d1ce4e5b9UL;
            hash ^= hash >> 27;
            hash *= 0x94d049bb133111ebUL;
            hash ^= hash >> 31;
            return hash;
        }

        private static string NormalizeForSearch(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            return new string([.. input.Where(char.IsLetterOrDigit)]).ToLowerInvariant();
        }
    }

    public partial class FacetFilterGroup(string label, string categoryPrefix, Action onChanged) : ObservableObject
    {
        private readonly List<FilterItem> _allItems = [];
        private readonly Action _onChanged = onChanged;
        private bool _suppressCallbacks;
        private bool _isFlyoutOpen;

        public string Label { get; } = label;
        public string CategoryPrefix { get; } = categoryPrefix;
        public BatchObservableCollection<FilterItem> FilteredItems { get; } = [];

        [ObservableProperty]
        public partial string SearchText { get; set; } = string.Empty;

        public int ActiveCount => _allItems.Count(item => item.IsSelected != false);
        public bool HasActiveFilters => ActiveCount > 0;
        public string ActiveCountText => $"{ActiveCount} active";

        public IReadOnlyList<string> RequiredValues => [.. _allItems.Where(item => item.IsSelected == true).Select(item => item.Name)];
        public IReadOnlyList<string> ExcludedValues => [.. _allItems.Where(item => item.IsSelected == null).Select(item => item.Name)];
        public IEnumerable<FilterItem> ActiveItems => _allItems.Where(item => item.IsSelected != false);

        partial void OnSearchTextChanged(string value) => RebuildFilteredItemsFromCurrentOrder();

        public void BeginInteraction() => _isFlyoutOpen = true;

        public void EndInteraction()
        {
            _isFlyoutOpen = false;
            CommitDisplayOrder();
        }

        public void Populate(IEnumerable<string> names, Func<string, int> countSelector)
        {
            _allItems.Clear();
            foreach (var name in names)
            {
                _allItems.Add(new FilterItem(name, countSelector(name), HandleItemChanged));
            }

            CommitDisplayOrder();
            NotifyActiveStateChanged();
        }

        [RelayCommand]
        public void Clear()
        {
            ResetSelectionsSilently();
            _onChanged();
        }

        public void ResetSelectionsSilently()
        {
            _suppressCallbacks = true;
            foreach (var item in _allItems)
            {
                item.IsSelected = false;
            }
            _suppressCallbacks = false;
            RefreshFilteredItems();
            NotifyActiveStateChanged();
        }

        public void RestoreSelectionsSilently(IReadOnlyCollection<string> requiredValues, IReadOnlyCollection<string> excludedValues)
        {
            var requiredSet = new HashSet<string>(requiredValues, StringComparer.OrdinalIgnoreCase);
            var excludedSet = new HashSet<string>(excludedValues, StringComparer.OrdinalIgnoreCase);

            _suppressCallbacks = true;
            foreach (var item in _allItems)
            {
                if (requiredSet.Contains(item.Name))
                {
                    item.IsSelected = true;
                }
                else if (excludedSet.Contains(item.Name))
                {
                    item.IsSelected = null;
                }
                else
                {
                    item.IsSelected = false;
                }
            }
            _suppressCallbacks = false;

            RefreshFilteredItems();
            NotifyActiveStateChanged();
        }

        public void SyncItemCount(string name, int count, bool insertIfMissing)
        {
            var existing = _allItems.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.ItemCount = count;
                return;
            }

            if (!insertIfMissing)
            {
                return;
            }

            _allItems.Add(new FilterItem(name, count, HandleItemChanged));
            if (!_isFlyoutOpen)
            {
                CommitDisplayOrder();
            }
            else
            {
                RebuildFilteredItemsFromCurrentOrder();
            }
        }

        public void RefreshFilteredItems()
        {
            if (_isFlyoutOpen)
            {
                return;
            }

            CommitDisplayOrder();
        }

        private void CommitDisplayOrder()
        {
            var orderedItems = _allItems
                .OrderByDescending(item => item.IsSelected != false)
                .ThenByDescending(item => item.IsSelected == true)
                .ThenBy(item => item.Name)
                .ToList();

            _allItems.Clear();
            _allItems.AddRange(orderedItems);
            RebuildFilteredItemsFromCurrentOrder();
        }

        private void RebuildFilteredItemsFromCurrentOrder()
        {
            var matchingItems = _allItems
                .Where(item => string.IsNullOrEmpty(SearchText) || item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            FilteredItems.ReplaceAll(matchingItems);
        }

        private void HandleItemChanged()
        {
            if (_suppressCallbacks)
            {
                return;
            }

            NotifyActiveStateChanged();
            _onChanged();
        }

        private void NotifyActiveStateChanged()
        {
            OnPropertyChanged(nameof(ActiveCount));
            OnPropertyChanged(nameof(HasActiveFilters));
            OnPropertyChanged(nameof(ActiveCountText));
        }
    }

    public partial class FilterItem(string name, int itemCount, Action onChanged) : ObservableObject
    {
        public FilterItem(string name, Action onChanged) : this(name, 0, onChanged)
        {
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsIncluded), nameof(IsExcluded), nameof(StateSymbol))]
        public partial bool? IsSelected { get; set; } = false;

        public string Name { get; } = name;

        [ObservableProperty]
        public partial int ItemCount { get; set; } = itemCount;
        public bool IsIncluded => IsSelected == true;
        public bool IsExcluded => IsSelected == null;
        public string StateSymbol => IsSelected switch
        {
            true => "+",
            null => "−",
            false => string.Empty
        };

        [RelayCommand]
        private void CycleState()
        {
            IsSelected = IsSelected switch
            {
                false => true,
                true => null,
                null => false
            };
        }

        partial void OnIsSelectedChanged(bool? value) => onChanged();
    }

    public sealed class ActiveFilterChip(string label, bool isInclude, bool isExclude, bool isRange, Action removeAction)
    {
        public string Label { get; } = label;
        public bool IsInclude { get; } = isInclude;
        public bool IsExclude { get; } = isExclude;
        public bool IsRange { get; } = isRange;
        public RelayCommand RemoveCommand { get; } = new(removeAction);
    }
}
