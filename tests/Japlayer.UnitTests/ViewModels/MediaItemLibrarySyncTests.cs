using CommunityToolkit.Mvvm.Messaging;
using Japlayer.Contracts;
using Japlayer.Data.Contracts;
using Japlayer.Data.Entities;
using Japlayer.Data.Models;
using Japlayer.Services;
using Japlayer.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.ViewModels
{
    [TestClass]
    public class MediaItemLibrarySyncTests
    {
        private sealed class FakeDatabaseChangeMonitor : IDatabaseChangeMonitor
        {
            public int InternalWriteCount { get; private set; }
            public event EventHandler? DatabaseChangedExternally { add { } remove { } }
            public void Start() { }
            public void Dispose() { }

            public IDisposable BeginInternalWrite()
            {
                InternalWriteCount++;
                return new Scope();
            }

            private sealed class Scope : IDisposable
            {
                public void Dispose() { }
            }
        }

        private sealed class StubMediaProvider(LibraryItem item) : IMediaProvider
        {
            private readonly LibraryItem _item = item;
            public TaskCompletionSource? RatingSavedSignal { get; set; }

            public Task<IEnumerable<LibraryItem>> GetLibraryItemsAsync() => Task.FromResult<IEnumerable<LibraryItem>>([_item]);
            public Task<IEnumerable<string>> GetUserTagsAsync() => Task.FromResult<IEnumerable<string>>(_item.UserTags);
            public Task<IEnumerable<string>> GetGenresAsync() => Task.FromResult<IEnumerable<string>>([]);
            public Task<IEnumerable<string>> GetCastAsync() => Task.FromResult<IEnumerable<string>>([]);
            public Task<IEnumerable<string>> GetStudiosAsync() => Task.FromResult<IEnumerable<string>>([]);
            public Task<IEnumerable<string>> GetSeriesAsync() => Task.FromResult<IEnumerable<string>>([]);
            public Task<MediaItem> GetMediaItemAsync(string mediaId) => Task.FromResult(new MediaItem { MediaId = mediaId, UserTags = [.. _item.UserTags], Rating = _item.Rating });
            public Task AddUserTagAsync(string mediaId, string tagName) => Task.CompletedTask;
            public Task RemoveUserTagAsync(string mediaId, string tagName) => Task.CompletedTask;

            public Task SetRatingAsync(string mediaId, int rating)
            {
                RatingSavedSignal?.TrySetResult();
                return Task.CompletedTask;
            }
        }

        private sealed class StubImageProvider : IImageProvider
        {
            public Task<IEnumerable<string>> GetGalleryPathsAsync(string mediaId) => Task.FromResult<IEnumerable<string>>([]);
        }

        private sealed class StubSceneProvider : IMediaSceneProvider
        {
            public Task<IEnumerable<MediaScene>> GetMediaScenesAsync(string mediaId)
                => Task.FromResult<IEnumerable<MediaScene>>([new MediaScene { MediaId = mediaId, SceneNumber = 1, FilePaths = [] }]);
        }

        private sealed class StubThumbnailProvider : IMediaThumbnailProvider
        {
            public Task<IEnumerable<MediaThumbnail>> GetThumbnailsAsync(string mediaId) => Task.FromResult<IEnumerable<MediaThumbnail>>([]);
        }

        private sealed class StubHighlightProvider : IMediaHighlightProvider
        {
            public Task<IEnumerable<MediaHighlight>> GetHighlightsAsync(string mediaId) => Task.FromResult<IEnumerable<MediaHighlight>>([]);
            public Task AddHighlightAsync(string mediaId, int scene, int timestamp) => Task.CompletedTask;
            public Task RemoveHighlightAsync(string mediaId, int scene, int timestamp) => Task.CompletedTask;
        }

        private sealed class StubSettingsService : ISettingsService
        {
            public string ImagePath { get; set; } = @"C:\Images";
            public string SqliteDatabasePath { get; set; } = @"C:\Images\medias.db";
            public Task LoadAsync() => Task.CompletedTask;
            public Task SaveAsync() => Task.CompletedTask;
        }

        [TestMethod]
        public async Task MediaItemAndSceneEdits_SynchronizeEndToEndWithLibraryViewModel_AndMarkInternalWrites()
        {
            var item = new LibraryItem { MediaId = "M1", Title = "Test Movie", Rating = 2, UserTags = ["Initial"] };
            var mediaProvider = new StubMediaProvider(item);
            var monitor = new FakeDatabaseChangeMonitor();
            var messenger = new WeakReferenceMessenger();

            var services = new ServiceCollection();
            services.AddSingleton<ISettingsService, StubSettingsService>();
            services.AddSingleton<IMediaThumbnailProvider, StubThumbnailProvider>();
            var serviceProvider = services.BuildServiceProvider();

            var libraryViewModel = new LibraryViewModel(mediaProvider, new StubSceneProvider(), serviceProvider, monitor, messenger);
            await libraryViewModel.LoadDataAsync();

            var mediaItemViewModel = new MediaItemViewModel(
                item,
                new StubImageProvider(),
                new StubSceneProvider(),
                mediaProvider,
                new StubSettingsService(),
                new StubThumbnailProvider(),
                new StubHighlightProvider(),
                monitor,
                messenger);
            await mediaItemViewModel.LoadDetailsAsync();

            // 1. Add a brand-new tag via MediaItemViewModel
            await mediaItemViewModel.AddUserTagAsync("SyncNewTag");
            var addedFacetItem = libraryViewModel.TagsFacet.FilteredItems.FirstOrDefault(tag => tag.Name == "SyncNewTag");
            Assert.IsNotNull(addedFacetItem, "Newly created tag should immediately appear in TagsFacet.");
            Assert.AreEqual(1, addedFacetItem.ItemCount);

            // 2. Remove tag via MediaItemViewModel
            await mediaItemViewModel.RemoveUserTagAsync("SyncNewTag");
            Assert.AreEqual(0, addedFacetItem.ItemCount);

            // 3. Change rating via MediaItemViewModel
            var ratingSaved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            mediaProvider.RatingSavedSignal = ratingSaved;
            mediaItemViewModel.Rating = 5;
            await Task.WhenAny(ratingSaved.Task, Task.Delay(2000));
            await Task.Delay(20); // allow continuation after SetRatingAsync to send message

            var libraryItemViewModel = libraryViewModel.MediaItems.Single(media => media.Id == "M1");
            Assert.AreEqual(5, libraryItemViewModel.Rating);
            Assert.IsTrue(libraryItemViewModel.HasRating);

            // 4. Add highlight via MediaSceneViewModel
            await mediaItemViewModel.SelectedScene!.AddHighlightAsync(42);
            Assert.AreEqual(1, libraryItemViewModel.HighlightCount);
            Assert.AreEqual(4, monitor.InternalWriteCount, "Tag add, tag remove, rating save, and highlight add should all mark internal writes.");
        }
    }
}
