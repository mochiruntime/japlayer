using Japlayer.Contracts;
using Japlayer.Data.Contracts;
using Japlayer.Data.Entities;
using Japlayer.Data.Models;
using Japlayer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.ViewModels
{
    [TestClass]
    public class LibraryItemViewModelTests
    {
        private class StubSettingsService : ISettingsService
        {
            public string ImagePath { get; set; } = @"C:\MediaImages";
            public string SqliteDatabasePath { get; set; } = @"C:\MediaImages\medias.db";
            public Task LoadAsync() => Task.CompletedTask;
            public Task SaveAsync() => Task.CompletedTask;
        }

        private class StubThumbnailProvider : IMediaThumbnailProvider
        {
            public List<MediaThumbnail> ThumbnailsToReturn { get; set; } = [];

            public Task<IEnumerable<MediaThumbnail>> GetThumbnailsAsync(string mediaId) => Task.FromResult<IEnumerable<MediaThumbnail>>(ThumbnailsToReturn);
        }

        [TestMethod]
        public void Title_ConcatenatesMediaIdAndTitle()
        {
            // Arrange
            var libraryItem = new LibraryItem
            {
                MediaId = "TEST-001",
                Title = "Sample Title"
            };
            var settingsService = new StubSettingsService();
            var thumbnailProvider = new StubThumbnailProvider();

            // Act
            var viewModel = new LibraryItemViewModel(libraryItem, settingsService, thumbnailProvider);

            // Assert
            Assert.AreEqual("TEST-001 Sample Title", viewModel.Title);
            Assert.AreEqual("TEST-001", viewModel.Id);
            Assert.AreEqual("Sample Title", viewModel.OriginalTitle);
        }

        [TestMethod]
        public void DisplayCover_WhenCoverImagePathExists_CombinesWithImagePath()
        {
            // Arrange
            var libraryItem = new LibraryItem
            {
                MediaId = "TEST-001",
                CoverImagePath = "covers/cover.jpg"
            };
            var settingsService = new StubSettingsService { ImagePath = @"C:\Images" };
            var thumbnailProvider = new StubThumbnailProvider();

            // Act
            var viewModel = new LibraryItemViewModel(libraryItem, settingsService, thumbnailProvider);

            // Assert
            Assert.AreEqual(@"C:\Images\covers/cover.jpg", viewModel.DisplayCover);
        }

        [TestMethod]
        public void DisplayCover_WhenNoCoverAndNoThumbnails_ReturnsFallbackPlaceholder()
        {
            // Arrange
            var libraryItem = new LibraryItem
            {
                MediaId = "TEST-001",
                CoverImagePath = null
            };
            var settingsService = new StubSettingsService();
            var thumbnailProvider = new StubThumbnailProvider();

            // Act
            var viewModel = new LibraryItemViewModel(libraryItem, settingsService, thumbnailProvider);

            // Assert
            Assert.AreEqual("ms-appx:///Assets/NoCover.png", viewModel.DisplayCover);
        }

        [TestMethod]
        public async Task ScrubPreview_AfterLoadingThumbnails_UpdatesCurrentImageSource()
        {
            // Arrange
            var libraryItem = new LibraryItem { MediaId = "TEST-001", CoverImagePath = "cover.jpg" };
            var settingsService = new StubSettingsService { ImagePath = @"C:\Images" };
            var thumbnailProvider = new StubThumbnailProvider
            {
                ThumbnailsToReturn =
                [
                    new() { MediaId = "TEST-001", Scene = 1, Timestamp = 10, Path = "thumb1.jpg" },
                    new() { MediaId = "TEST-001", Scene = 1, Timestamp = 20, Path = "thumb2.jpg" },
                    new() { MediaId = "TEST-001", Scene = 1, Timestamp = 30, Path = "thumb3.jpg" },
                    new() { MediaId = "TEST-001", Scene = 1, Timestamp = 40, Path = "thumb4.jpg" }
                ]
            };

            var viewModel = new LibraryItemViewModel(libraryItem, settingsService, thumbnailProvider);
            await viewModel.LoadThumbnailsAsync();

            // Act 1: 0% scrub -> first thumbnail
            viewModel.ScrubPreview(0.0);
            Assert.AreEqual(@"C:\Images\thumb1.jpg", viewModel.CurrentImageSource);

            // Act 2: 50% scrub -> middle thumbnail
            viewModel.ScrubPreview(0.5);
            Assert.AreEqual(@"C:\Images\thumb3.jpg", viewModel.CurrentImageSource);

            // Act 3: 100% scrub -> last thumbnail
            viewModel.ScrubPreview(1.0);
            Assert.AreEqual(@"C:\Images\thumb4.jpg", viewModel.CurrentImageSource);

            // Act 4: Reset
            viewModel.ResetPreview();
            Assert.IsNull(viewModel.CurrentImageSource);
            Assert.AreEqual(@"C:\Images\cover.jpg", viewModel.DisplayImageSource);
        }
    }
}
