using FlaUI.Core.Tools;
using Japlayer.UITests.Fixtures;
using Japlayer.UITests.PageObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UITests.Specs
{
    [TestClass]
    public class WrapPanelLayoutUITests
    {
        private TestAppFixture? _fixture;
        private LibraryPageObject? _libraryPage;

        [TestInitialize]
        public void Setup()
        {
            _fixture = new TestAppFixture();
            _fixture.StartApp();
            Assert.IsNotNull(_fixture.MainWindow, "Main window failed to initialize.");
            _libraryPage = new LibraryPageObject(_fixture.MainWindow);
            Assert.IsNotNull(_libraryPage.WaitForCard("TEST-001"), "Initial card TEST-001 should be present.");
        }

        [TestCleanup]
        public void Teardown() => _fixture?.Dispose();

        [TestMethod]
        public void ActiveFiltersBar_IncludeIndieAndExcludeVR_StayOnSingleRow()
        {
            Assert.IsNotNull(_libraryPage);

            _libraryPage.OpenFacetFlyout("Tags");
            _libraryPage.ClickFacetItem("Indie");
            _libraryPage.ClickFacetItem("VR");
            _libraryPage.ClickFacetItem("VR");
            _libraryPage.CloseFlyout();

            var chipsReady = Retry.WhileFalse(
                () => _libraryPage.GetActiveFilterChipRemoveButtons().Length == 2,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(100)).Success;
            Assert.IsTrue(chipsReady, "Expected 2 active filter chips (+Indie and -VR).");

            var chips = _libraryPage.GetActiveFilterChipRemoveButtons();
            var activeFiltersBar = _libraryPage.WaitForElement(() => _libraryPage.ActiveFiltersBar);
            Assert.IsNotNull(activeFiltersBar, "ActiveFiltersBar should be visible.");

            Assert.IsTrue(
                Math.Abs(chips[0].BoundingRectangle.Top - chips[1].BoundingRectangle.Top) <= 2,
                $"Expected +Indie and -VR chips on 1 row, but Tops were {chips[0].BoundingRectangle.Top} and {chips[1].BoundingRectangle.Top}.");

            foreach (var chip in chips)
            {
                Assert.IsTrue(
                    chip.BoundingRectangle.Bottom <= activeFiltersBar.BoundingRectangle.Bottom,
                    $"Chip bottom ({chip.BoundingRectangle.Bottom}) overflowed ActiveFiltersBar bottom ({activeFiltersBar.BoundingRectangle.Bottom}).");
            }
        }

        [TestMethod]
        public void ActiveFiltersBar_MultiRowFilters_ContainerHeightEnclosesAllRows()
        {
            Assert.IsNotNull(_libraryPage);

            _libraryPage.OpenFacetFlyout("Tags");
            _libraryPage.ClickFacetItem("Indie");
            _libraryPage.ClickFacetItem("VR");
            _libraryPage.ClickFacetItem("VR");

            _libraryPage.OpenFacetFlyout("Genres");
            foreach (var genre in new[] { "Action", "Adventure", "Animation", "Comedy", "Cyberpunk", "Documentary", "Drama", "Fantasy", "Horror", "Mystery", "Romance", "Sci-Fi", "Thriller" })
            {
                _libraryPage.ClickFacetItem(genre);
            }

            _libraryPage.CloseFlyout();

            var chipsReady = Retry.WhileFalse(
                () => _libraryPage.GetActiveFilterChipRemoveButtons().Length == 15,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(100)).Success;
            Assert.IsTrue(chipsReady, "Expected 15 active filter chips.");

            var chips = _libraryPage.GetActiveFilterChipRemoveButtons();
            var activeFiltersBar = _libraryPage.WaitForElement(() => _libraryPage.ActiveFiltersBar);
            Assert.IsNotNull(activeFiltersBar, "ActiveFiltersBar should be visible.");

            var distinctRows = chips.Select(chip => chip.BoundingRectangle.Top).Distinct().Count();
            Assert.IsTrue(distinctRows >= 2, $"Expected at least 2 wrapped rows of filter chips, but found {distinctRows}.");

            foreach (var chip in chips)
            {
                Assert.IsTrue(
                    chip.BoundingRectangle.Bottom <= activeFiltersBar.BoundingRectangle.Bottom,
                    $"Chip bottom ({chip.BoundingRectangle.Bottom}) overflowed ActiveFiltersBar bottom ({activeFiltersBar.BoundingRectangle.Bottom}).");
            }
        }

        [TestMethod]
        public void MediaItemPage_ClipsAndHDRTags_StayOnSingleRow()
        {
            Assert.IsNotNull(_libraryPage);
            Assert.IsNotNull(_fixture?.MainWindow);

            _libraryPage.ClickCard("TEST-002");

            var mediaItemPage = new MediaItemPageObject(_fixture.MainWindow);
            Assert.IsNotNull(mediaItemPage.WaitForElement(() => mediaItemPage.BackButton), "BackButton should appear after navigating to MediaItemPage.");

            var tagsReady = Retry.WhileFalse(
                () => mediaItemPage.GetRemoveTagButtons().Length >= 2,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(100)).Success;
            Assert.IsTrue(tagsReady, "Expected seeded tags (VR, Clips, HDR) on TEST-002.");

            var tagButtons = mediaItemPage.GetRemoveTagButtons();
            var firstTop = tagButtons[0].BoundingRectangle.Top;
            foreach (var button in tagButtons)
            {
                Assert.IsTrue(
                    Math.Abs(button.BoundingRectangle.Top - firstTop) <= 2,
                    $"Expected tags on 1 row, but found Top={button.BoundingRectangle.Top} vs firstTop={firstTop}.");
            }
        }
    }
}
