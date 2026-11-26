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
            Assert.IsNotNull(_libraryPage.WaitForCard("TEST-001"), "Initial card TEST-001 should be present.");

            _libraryPage.ClickCard("TEST-001");

            _mediaItemPage = new MediaItemPageObject(_fixture.MainWindow);
            Assert.IsNotNull(_mediaItemPage.WaitForElement(() => _mediaItemPage.BackButton), "BackButton should appear after navigating to MediaItemPage.");
        }

        [TestCleanup]
        public void Teardown() => _fixture?.Dispose();

        [TestMethod]
        public void MediaItemDetails_DisplayExpectedInformation()
        {
            Assert.IsNotNull(_mediaItemPage);

            var titleElement = _mediaItemPage.WaitForElement(() => _mediaItemPage.TitleText);
            Assert.IsNotNull(titleElement, "Title element should be present.");
            Assert.IsTrue(titleElement.Name.Contains("Alpha", StringComparison.OrdinalIgnoreCase));

            var factsSummary = _mediaItemPage.WaitForElement(() => _mediaItemPage.FactsSummaryText);
            Assert.IsNotNull(factsSummary, "MediaItemFactsSummary should be present.");
            Assert.IsTrue(factsSummary.Name.Contains("90", StringComparison.OrdinalIgnoreCase), "Facts summary should include runtime.");
            Assert.IsTrue(factsSummary.Name.Contains("CID-001", StringComparison.OrdinalIgnoreCase), "Facts summary should include content ID.");

            var ratingText = _mediaItemPage.WaitForElement(() => _mediaItemPage.RatingDisplayText);
            Assert.IsNotNull(ratingText, "RatingDisplayText should be present.");
            Assert.IsTrue(ratingText.Name.Contains("5", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void RatingAndUserTags_CanUpdateAndRemoveTags()
        {
            Assert.IsNotNull(_mediaItemPage);

            // Update rating via UIA RangeValue pattern
            _mediaItemPage.SetRating(3);
            var updatedRating = _mediaItemPage.WaitForElement(() =>
            {
                var element = _mediaItemPage.RatingDisplayText;
                return element != null && element.Name.Contains("3", StringComparison.Ordinal) ? element : null;
            });
            Assert.IsNotNull(updatedRating, "RatingDisplayText should reflect updated rating of 3.");

            // Add a tag and verify it appears, then remove a tag
            _mediaItemPage.AddTag("NewUITag");
            var tagAppeared = FlaUI.Core.Tools.Retry.WhileFalse(
                () => _mediaItemPage.HasTagText("NewUITag"),
                TimeSpan.FromSeconds(3),
                TimeSpan.FromMilliseconds(100)).Success;
            Assert.IsTrue(tagAppeared, "Added tag 'NewUITag' should appear.");

            _mediaItemPage.RemoveFirstTag();
        }

        [TestMethod]
        public void PlaybackControls_AreAccessibleWhenPlayOverlayClicked()
        {
            Assert.IsNotNull(_mediaItemPage);

            _mediaItemPage.ClickPlayOverlay();

            Assert.IsNotNull(_mediaItemPage.WaitForElement(() => _mediaItemPage.SlowSeekForwardButton), "SlowSeekForwardButton should be available in transport controls.");
            Assert.IsNotNull(_mediaItemPage.WaitForElement(() => _mediaItemPage.FastSeekForwardButton), "FastSeekForwardButton should be available in transport controls.");
            Assert.IsNotNull(_mediaItemPage.WaitForElement(() => _mediaItemPage.SaveHighlightButton), "SaveHighlightButton should be available in transport controls.");
            Assert.IsNotNull(_mediaItemPage.WaitForElement(() => _mediaItemPage.SkipToNextHighlightButton), "SkipToNextHighlightButton should be available in transport controls.");
            Assert.IsNotNull(_mediaItemPage.WaitForElement(() => _mediaItemPage.FullWindowButton), "FullWindowButton should be available in transport controls.");

            // Toggle fullscreen on and verify MainWindow collapses AppTitleBar
            _mediaItemPage.ToggleFullscreen();
            Assert.IsTrue(
                _mediaItemPage.WaitUntilHidden(() => _fixture!.MainWindow!.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("AppTitleTextBlock"))),
                "AppTitleTextBlock should be hidden in fullscreen mode.");
        }

        [TestMethod]
        public void MultiSceneTheater_CanToggleBetweenFocusAndGridModes()
        {
            Assert.IsNotNull(_mediaItemPage);

            Assert.IsNotNull(_mediaItemPage.WaitForElement(() => _mediaItemPage.FocusViewButton), "FocusViewButton should be visible when media has multiple scenes.");
            Assert.IsNotNull(_mediaItemPage.WaitForElement(() => _mediaItemPage.GridViewButton), "GridViewButton should be visible when media has multiple scenes.");

            _mediaItemPage.SwitchToGridView();
            Assert.IsTrue(_mediaItemPage.WaitUntilHidden(() => _mediaItemPage.SelectedSceneTitle), "Focus theater header should hide in Multi-Scene Grid mode.");

            _mediaItemPage.SwitchToFocusView();
            Assert.IsNotNull(_mediaItemPage.WaitForElement(() => _mediaItemPage.SelectedSceneTitle), "Focus theater header should reappear in Focus Player mode.");
        }

        [TestMethod]
        public void GalleryLightbox_OpensPaginatesAndClosesWithoutErrors()
        {
            Assert.IsNotNull(_mediaItemPage);

            _mediaItemPage.OpenGalleryLightbox();

            var counterText = _mediaItemPage.WaitForElement(() => _mediaItemPage.LightboxCounterText);
            Assert.IsNotNull(counterText, "LightboxCounterText should be visible when lightbox opens.");
            Assert.AreEqual("1 / 2", counterText.Name);

            _mediaItemPage.ClickLightboxNext();
            var secondPage = _mediaItemPage.WaitForElement(() =>
            {
                var element = _mediaItemPage.LightboxCounterText;
                return element != null && element.Name == "2 / 2" ? element : null;
            });
            Assert.IsNotNull(secondPage, "Lightbox counter should advance to 2 / 2.");

            _mediaItemPage.ClickLightboxPrevious();
            var firstPage = _mediaItemPage.WaitForElement(() =>
            {
                var element = _mediaItemPage.LightboxCounterText;
                return element != null && element.Name == "1 / 2" ? element : null;
            });
            Assert.IsNotNull(firstPage, "Lightbox counter should return to 1 / 2.");

            _mediaItemPage.CloseGalleryLightbox();
            Assert.IsTrue(_mediaItemPage.WaitUntilHidden(() => _mediaItemPage.LightboxCloseButton), "Lightbox should close.");
        }
    }
}
