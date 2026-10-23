using Japlayer.Data.Contracts;
using Japlayer.Data.Entities;
using Japlayer.Data.Models;
using Japlayer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.ViewModels
{
    [TestClass]
    public class MediaSceneViewModelTests
    {
        private class StubHighlightProvider : IMediaHighlightProvider
        {
            public List<(string MediaId, int Scene, int Timestamp)> AddedHighlights { get; } = [];
            public List<(string MediaId, int Scene, int Timestamp)> RemovedHighlights { get; } = [];

            public Task<IEnumerable<MediaHighlight>> GetHighlightsAsync(string mediaId) => Task.FromResult<IEnumerable<MediaHighlight>>([]);

            public Task AddHighlightAsync(string mediaId, int scene, int timestamp)
            {
                AddedHighlights.Add((mediaId, scene, timestamp));
                return Task.CompletedTask;
            }

            public Task RemoveHighlightAsync(string mediaId, int scene, int timestamp)
            {
                RemovedHighlights.Add((mediaId, scene, timestamp));
                return Task.CompletedTask;
            }
        }

        [TestMethod]
        public void InitialState_PopulatesHighlightsAndProperties()
        {
            // Arrange
            var scene = new MediaScene
            {
                MediaId = "TEST-001",
                SceneNumber = 1,
                FilePaths = [@"C:\Videos\video.mp4"]
            };
            var initialHighlights = new List<int> { 50, 10, 30 };
            var highlightProvider = new StubHighlightProvider();

            // Act
            var viewModel = new MediaSceneViewModel(scene, "poster.jpg", initialHighlights, [], highlightProvider);

            // Assert
            Assert.AreEqual("TEST-001", viewModel.MediaId);
            Assert.AreEqual(1, viewModel.SceneNumber);
            Assert.AreEqual(@"C:\Videos\video.mp4", viewModel.File);
            Assert.AreEqual("poster.jpg", viewModel.PosterPath);
            Assert.IsTrue(viewModel.HasHighlights);
            // Verify highlights are ordered ascending
            Assert.AreEqual(10, viewModel.Highlights[0]);
            Assert.AreEqual(30, viewModel.Highlights[1]);
            Assert.AreEqual(50, viewModel.Highlights[2]);
        }

        [TestMethod]
        public async Task AddHighlightAsync_InsertsInSortedOrderAndCallsProvider()
        {
            // Arrange
            var scene = new MediaScene { MediaId = "TEST-001", SceneNumber = 1, FilePaths = [] };
            var highlightProvider = new StubHighlightProvider();
            var viewModel = new MediaSceneViewModel(scene, null, [10, 30], [], highlightProvider);

            // Act: insert 20 (should go between 10 and 30)
            await viewModel.AddHighlightAsync(20);

            // Assert
            Assert.AreEqual(3, viewModel.Highlights.Count);
            Assert.AreEqual(10, viewModel.Highlights[0]);
            Assert.AreEqual(20, viewModel.Highlights[1]);
            Assert.AreEqual(30, viewModel.Highlights[2]);
            Assert.AreEqual(1, highlightProvider.AddedHighlights.Count);
            Assert.AreEqual(("TEST-001", 1, 20), highlightProvider.AddedHighlights[0]);
        }

        [TestMethod]
        public async Task AddHighlightAsync_WhenDuplicate_IgnoresInsertion()
        {
            // Arrange
            var scene = new MediaScene { MediaId = "TEST-001", SceneNumber = 1, FilePaths = [] };
            var highlightProvider = new StubHighlightProvider();
            var viewModel = new MediaSceneViewModel(scene, null, [10, 30], [], highlightProvider);

            // Act
            await viewModel.AddHighlightAsync(10);

            // Assert
            Assert.AreEqual(2, viewModel.Highlights.Count);
            Assert.AreEqual(0, highlightProvider.AddedHighlights.Count);
        }

        [TestMethod]
        public async Task RemoveHighlightAsync_RemovesHighlightAndCallsProvider()
        {
            // Arrange
            var scene = new MediaScene { MediaId = "TEST-001", SceneNumber = 1, FilePaths = [] };
            var highlightProvider = new StubHighlightProvider();
            var viewModel = new MediaSceneViewModel(scene, null, [10, 30], [], highlightProvider);

            // Act
            await viewModel.RemoveHighlightAsync(10);

            // Assert
            Assert.AreEqual(1, viewModel.Highlights.Count);
            Assert.AreEqual(30, viewModel.Highlights[0]);
            Assert.AreEqual(1, highlightProvider.RemovedHighlights.Count);
            Assert.AreEqual(("TEST-001", 1, 10), highlightProvider.RemovedHighlights[0]);
        }

        [TestMethod]
        public async Task RemoveHighlightAsync_WhenLastHighlightRemoved_UpdatesHasHighlightsToFalse()
        {
            // Arrange
            var scene = new MediaScene { MediaId = "TEST-001", SceneNumber = 1, FilePaths = [] };
            var highlightProvider = new StubHighlightProvider();
            var viewModel = new MediaSceneViewModel(scene, null, [10], [], highlightProvider);

            Assert.IsTrue(viewModel.HasHighlights);

            // Act
            await viewModel.RemoveHighlightAsync(10);

            // Assert
            Assert.AreEqual(0, viewModel.Highlights.Count);
            Assert.IsFalse(viewModel.HasHighlights);
        }
    }
}
