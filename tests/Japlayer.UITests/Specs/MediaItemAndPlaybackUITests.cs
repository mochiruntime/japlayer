using Japlayer.UITests.Fixtures;
using Japlayer.UITests.PageObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UITests.Specs
{
    [TestClass]
    public class MediaItemAndPlaybackUITests
    {
        private static TestAppFixture? _fixture;
        private static LibraryPageObject? _libraryPage;
        private static MediaItemPageObject? _mediaItemPage;

        [ClassInitialize]
        public static void ClassSetup(TestContext _)
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

        [ClassCleanup]
        public static void ClassTeardown()
        {
            _fixture?.Dispose();
            _fixture = null;
            _libraryPage = null;
            _mediaItemPage = null;
        }

        [TestInitialize]
        public void Setup()
        {
            Assert.IsNotNull(_libraryPage);
            Assert.IsNotNull(_mediaItemPage);

            // Navigate back and reopen TEST-001 to guarantee a fresh Transient MediaItemViewModel,
            // closed lightbox, Focus theater mode, and exited fullscreen (via MediaItemPage_Unloaded).
            if (_mediaItemPage.BackButton != null)
            {
                _mediaItemPage.ClickBack();
                Assert.IsNotNull(_libraryPage.WaitForCard("TEST-001"), "LibraryPage should appear after navigating back.");
            }

            _libraryPage.ClickCard("TEST-001");
            Assert.IsNotNull(_mediaItemPage.WaitForElement(() => _mediaItemPage.BackButton), "BackButton should appear after navigating to MediaItemPage.");
        }

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
        public void TagDropdown_ClosesWhenClickingNeutralContainer_AndReopensOnInputClick()
        {
            Assert.IsNotNull(_mediaItemPage);

            _mediaItemPage.ClickTagSearchBox();
            Assert.IsTrue(_mediaItemPage.HasTagSuggestion("2D"), "Suggestion dropdown should open when clicking tag input.");

            _mediaItemPage.ClickNeutralTitleArea();
            Assert.IsTrue(_mediaItemPage.WaitUntilTagSuggestionsClosed(), "Suggestion dropdown should close when clicking a neutral non-interactable element.");

            _mediaItemPage.ClickTagSearchBox();
            Assert.IsTrue(_mediaItemPage.HasTagSuggestion("2D"), "Suggestion dropdown should reopen cleanly when clicking tag input again.");
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

    [TestClass]
    public class MediaItemDatabaseUITests
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
        public void TagSuggestionSelection_DoesNotLeakRecordToStringIntoInput()
        {
            Assert.IsNotNull(_mediaItemPage);

            // Focus tag input to open existing suggestions (e.g., "2D"), select it, then interact with rating
            _mediaItemPage.FocusTagSearchBox();
            Assert.IsTrue(_mediaItemPage.SelectTagSuggestion("2D"), "Existing suggestion '2D' should appear in the dropdown.");
            _mediaItemPage.SetRating(4);

            var textAfterSelection = _mediaItemPage.GetTagSearchBoxText();
            Assert.IsFalse(
                textAfterSelection.Contains("TagSuggestionItem", StringComparison.Ordinal),
                $"Tag input leaked record ToString(): '{textAfterSelection}'.");
            Assert.AreEqual(string.Empty, textAfterSelection, "Tag input should remain empty after selecting a suggestion and moving focus.");
        }

        [TestMethod]
        public void TagDropdown_RefreshesSuggestionsAfterCreatingAndRemovingTag_WithoutReloading()
        {
            Assert.IsNotNull(_mediaItemPage);
            Assert.IsNotNull(_libraryPage);

            _mediaItemPage.FocusTagSearchBox();
            _mediaItemPage.SetTagSearchBoxText("BrandNewUITag");
            Assert.IsTrue(
                _mediaItemPage.SelectTagSuggestion("+ Create \"BrandNewUITag\""),
                "Creating a new tag should show and allow selecting '+ Create \"BrandNewUITag\"'.");

            var tagAppeared = FlaUI.Core.Tools.Retry.WhileFalse(
                () => _mediaItemPage.HasTagText("BrandNewUITag"),
                TimeSpan.FromSeconds(3),
                TimeSpan.FromMilliseconds(100)).Success;
            Assert.IsTrue(tagAppeared, "Added tag 'BrandNewUITag' should appear.");
            Assert.IsTrue(_mediaItemPage.WaitUntilTagSuggestionsClosed(), "Suggestion dropdown should close after submitting a new tag.");
            Assert.AreEqual(string.Empty, _mediaItemPage.GetTagSearchBoxText(), "Tag input should clear after submitting a new tag.");

            // Removing the tag should immediately make it available in the dropdown without leaving the item view
            _mediaItemPage.RemoveTag("BrandNewUITag");
            var tagRemoved = FlaUI.Core.Tools.Retry.WhileTrue(
                () => _mediaItemPage.HasTagText("BrandNewUITag"),
                TimeSpan.FromSeconds(3),
                TimeSpan.FromMilliseconds(100)).Success;
            Assert.IsTrue(tagRemoved, "Removed tag 'BrandNewUITag' should disappear from tag chips.");
            Assert.IsTrue(_mediaItemPage.WaitUntilTagSuggestionsClosed(), "Removing a tag chip should not unexpectedly reopen the dropdown.");

            _mediaItemPage.SetRating(4);
            _mediaItemPage.FocusTagSearchBox();
            Assert.IsTrue(_mediaItemPage.HasTagSuggestion("2D"), "Dropdown should reopen with existing unassigned tags.");
            Assert.IsTrue(_mediaItemPage.HasTagSuggestion("BrandNewUITag"), "Removed tag 'BrandNewUITag' should appear in dropdown suggestions immediately.");

            // Navigating to another item should also expose the created tag
            _mediaItemPage.ClickBack();
            Assert.IsNotNull(_libraryPage.WaitForCard("TEST-002"), "TEST-002 should be visible on LibraryPage.");
            _libraryPage.ClickCard("TEST-002");
            Assert.IsNotNull(_mediaItemPage.WaitForElement(() => _mediaItemPage.BackButton), "BackButton should appear on TEST-002.");

            _mediaItemPage.FocusTagSearchBox();
            Assert.IsTrue(_mediaItemPage.HasTagSuggestion("BrandNewUITag"), "Created tag 'BrandNewUITag' should be available in TEST-002 dropdown.");
        }

        [TestMethod]
        public void RatingChangeOnMediaItemPage_SynchronizesBackToLibraryCard()
        {
            Assert.IsNotNull(_mediaItemPage);
            Assert.IsNotNull(_libraryPage);

            _mediaItemPage.SetRating(2);
            var updatedRating = _mediaItemPage.WaitForElement(() =>
            {
                var element = _mediaItemPage.RatingDisplayText;
                return element != null && element.Name.Contains("2", StringComparison.Ordinal) ? element : null;
            });
            Assert.IsNotNull(updatedRating, "RatingDisplayText should reflect updated rating of 2.");

            _mediaItemPage.ClickBack();
            Assert.IsNotNull(_libraryPage.WaitForCard("TEST-001"), "Should navigate back to LibraryPage.");

            var cardUpdated = FlaUI.Core.Tools.Retry.WhileFalse(() =>
            {
                var card = _libraryPage.FindCard("TEST-001");
                if (card == null)
                {
                    return false;
                }

                var texts = card.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
                return texts.Any(text => text.Properties.Name.ValueOrDefault == "2");
            }, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100)).Success;

            Assert.IsTrue(cardUpdated, "Library card rating badge for TEST-001 should update to 2 after returning from MediaItemPage.");
        }
    }

    [TestClass]
    public class LibraryNavigationStateUITests
    {
        private TestAppFixture? _fixture;
        private LibraryPageObject? _libraryPage;

        [TestInitialize]
        public void Setup()
        {
            _fixture = new TestAppFixture(extraSeedItemCount: 15);
            _fixture.StartApp();
            Assert.IsNotNull(_fixture.MainWindow, "Main window failed to initialize.");
            _libraryPage = new LibraryPageObject(_fixture.MainWindow);
            Assert.IsNotNull(_libraryPage.WaitForCard("TEST-001"), "Initial card TEST-001 should be present.");
            Assert.IsTrue(_libraryPage.WaitForItemCountText("18 items"), "All 18 seeded items should load.");
        }

        [TestCleanup]
        public void Teardown() => _fixture?.Dispose();

        [TestMethod]
        public void RandomSort_EditItemWithMatchingFilter_PreservesOrderSelectionAndViewportOnBack()
        {
            Assert.IsNotNull(_libraryPage);
            Assert.IsNotNull(_fixture?.MainWindow);

            // Filter by +VR (matches TEST-001, TEST-002, and TEST-004..TEST-018 = 17 items) and activate Random sort
            _libraryPage.OpenFacetFlyout("Tags");
            _libraryPage.ClickFacetItem("VR");
            _libraryPage.CloseFlyout();
            Assert.IsTrue(_libraryPage.WaitForItemCountText("17 of 18"), "17 items should match +VR.");

            var preShuffleOrder = _libraryPage.GetVisibleCardIds();
            _libraryPage.ClickShuffle();
            Assert.IsTrue(_libraryPage.WaitUntilHidden(() => _libraryPage.SortDirectionButton), "SortDirectionButton should hide when Random sort activates.");
            var shuffled = FlaUI.Core.Tools.Retry.WhileFalse(
                () => !_libraryPage.GetVisibleCardIds().SequenceEqual(preShuffleOrder),
                TimeSpan.FromSeconds(3),
                TimeSpan.FromMilliseconds(50)).Success;
            Assert.IsTrue(shuffled, "Expected card order to update after Random shuffle debounce.");

            // Pick a realized card near the bottom and scroll it into view before snapshotting orderBefore
            var initialVisible = _libraryPage.GetVisibleCardIds();
            Assert.IsTrue(initialVisible.Count >= 10, $"Expected at least 10 visible/realized cards in shuffled list, got {initialVisible.Count}.");

            var targetMediaId = initialVisible[^1];
            _libraryPage.ScrollCardIntoView(targetMediaId);
            Assert.IsTrue(_libraryPage.IsCardInViewport(targetMediaId), $"Target card '{targetMediaId}' should be scrolled into viewport before clicking.");

            var orderBefore = _libraryPage.GetVisibleCardIds();
            _libraryPage.ClickCard(targetMediaId);

            var mediaItemPage = new MediaItemPageObject(_fixture.MainWindow);
            Assert.IsNotNull(mediaItemPage.WaitForElement(() => mediaItemPage.BackButton), "BackButton should appear on MediaItemPage.");

            // Remove the first non-VR tag via UIA Invoke (queues _hasPendingFilterRefresh and writes to SQLite WAL)
            var initialTagCount = mediaItemPage.GetRemoveTagButtons().Length;
            Assert.IsTrue(initialTagCount >= 2, $"Expected at least 2 tags on '{targetMediaId}' before editing.");
            mediaItemPage.RemoveFirstTag();
            var tagRemoved = FlaUI.Core.Tools.Retry.WhileFalse(
                () => mediaItemPage.GetRemoveTagButtons().Length == initialTagCount - 1,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromMilliseconds(100)).Success;
            Assert.IsTrue(tagRemoved, "Removing first tag should persist and update UI.");

            mediaItemPage.ClickBack();
            Assert.IsNotNull(_libraryPage.WaitForElement(() => _libraryPage.MediaGrid), "LibraryPage MediaGrid should appear after navigating back.");

            var selectionRestored = FlaUI.Core.Tools.Retry.WhileFalse(
                () => _libraryPage.IsCardSelected(targetMediaId) && _libraryPage.IsCardInViewport(targetMediaId),
                TimeSpan.FromSeconds(3),
                TimeSpan.FromMilliseconds(50)).Success;

            var orderAfter = _libraryPage.GetVisibleCardIds();
            CollectionAssert.AreEqual(orderBefore, orderAfter, "Random card ordering should be preserved when navigating back after editing an item.");
            Assert.IsTrue(selectionRestored, $"Previously clicked card '{targetMediaId}' should remain selected and in viewport on Back navigation.");
        }
    }
}
