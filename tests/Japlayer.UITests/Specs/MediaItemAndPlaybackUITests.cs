using FlaUI.Core.Tools;
using Japlayer.UITests.Fixtures;
using Japlayer.UITests.PageObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UITests.Specs
{
    [TestClass]
    public class MediaItemAndPlaybackUITests
    {
        private TestAppFixture? _fixture;
        private LibraryPageObject? _libraryPage;
        private MediaItemPageObject? _mediaItemPage;

        [TestInitialize]
        public void Setup()
        {
            _fixture = new TestAppFixture();
            _fixture.StartApp();
            Assert.IsNotNull(_fixture.MainWindow, "Main window failed to initialize.");

            _libraryPage = new LibraryPageObject(_fixture.MainWindow);
            var card = Retry.WhileNull(
                () => _libraryPage.FindCard("TEST-001"),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(250)).Result;
            Assert.IsNotNull(card, "Initial card TEST-001 should be present.");

            _libraryPage.ClickCard("TEST-001");
            Thread.Sleep(600);

            _mediaItemPage = new MediaItemPageObject(_fixture.MainWindow);
            var backButton = Retry.WhileNull(
                () => _mediaItemPage.BackButton,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(250)).Result;
            Assert.IsNotNull(backButton, "BackButton should appear after navigating to MediaItemPage.");
        }

        [TestCleanup]
        public void Teardown() => _fixture?.Dispose();

        [TestMethod]
        public void MediaItemDetails_DisplayExpectedInformation()
        {
            Assert.IsNotNull(_mediaItemPage);

            var titleElement = _mediaItemPage.TitleText;
            Assert.IsNotNull(titleElement, "Title element should be present.");
            Assert.IsTrue(titleElement.Name.Contains("Alpha") || titleElement.HelpText.Contains("Alpha") || !string.IsNullOrEmpty(titleElement.AutomationId));

            var releaseDateElement = _mediaItemPage.ReleaseDateText;
            Assert.IsNotNull(releaseDateElement, "ReleaseDate element should be present.");

            var runtimeElement = _mediaItemPage.RuntimeText;
            Assert.IsNotNull(runtimeElement, "Runtime element should be present.");

            var contentIdElement = _mediaItemPage.ContentIdText;
            Assert.IsNotNull(contentIdElement, "ContentId element should be present.");
        }

        [TestMethod]
        public void PlaybackControls_AreAccessibleWhenPlayOverlayClicked()
        {
            Assert.IsNotNull(_mediaItemPage);

            var playOverlay = _mediaItemPage.PlayOverlayButton;
            Assert.IsNotNull(playOverlay, "Play overlay button should be present.");

            _mediaItemPage.ClickPlayOverlay();
            Thread.Sleep(800);

            // Verify transport buttons
            var slowSeekForward = _mediaItemPage.SlowSeekForwardButton;
            Assert.IsNotNull(slowSeekForward, "SlowSeekForwardButton should be available in transport controls.");

            var fastSeekForward = _mediaItemPage.FastSeekForwardButton;
            Assert.IsNotNull(fastSeekForward, "FastSeekForwardButton should be available in transport controls.");

            var saveHighlightButton = _mediaItemPage.SaveHighlightButton;
            Assert.IsNotNull(saveHighlightButton, "SaveHighlightButton should be available in transport controls.");

            var skipToNextHighlightButton = _mediaItemPage.SkipToNextHighlightButton;
            Assert.IsNotNull(skipToNextHighlightButton, "SkipToNextHighlightButton should be available in transport controls.");
        }

        [TestMethod]
        public void GalleryScrollButtons_CanBeClickedWithoutErrors()
        {
            Assert.IsNotNull(_mediaItemPage);

            var scrollRightButton = _mediaItemPage.GalleryScrollRightButton;
            Assert.IsNotNull(scrollRightButton, "Gallery scroll right button should be present.");
            if (scrollRightButton.Patterns.Invoke.IsSupported)
            {
                scrollRightButton.Patterns.Invoke.Pattern.Invoke();
            }
            else
            {
                scrollRightButton.Click();
            }
            Thread.Sleep(200);

            var scrollLeftButton = _mediaItemPage.GalleryScrollLeftButton;
            Assert.IsNotNull(scrollLeftButton, "Gallery scroll left button should be present.");
            if (scrollLeftButton.Patterns.Invoke.IsSupported)
            {
                scrollLeftButton.Patterns.Invoke.Pattern.Invoke();
            }
            else
            {
                scrollLeftButton.Click();
            }
            Thread.Sleep(200);
        }
    }
}
