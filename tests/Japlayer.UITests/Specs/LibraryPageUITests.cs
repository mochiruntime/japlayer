using Japlayer.UITests.Fixtures;
using Japlayer.UITests.PageObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UITests.Specs
{
    [TestClass]
    public class LibraryPageUITests
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
        }

        [TestCleanup]
        public void Teardown() => _fixture?.Dispose();

        private void WaitForInitialLoad()
        {
            Assert.IsNotNull(_libraryPage);
            Assert.IsNotNull(_libraryPage.WaitForCard("TEST-001"), "Initial card TEST-001 should be present.");
            Assert.IsTrue(_libraryPage.WaitForCardCount(3), "All 3 seeded cards should be visible initially.");
        }

        [TestMethod]
        public void MainWindow_TitleAndBasicProperties_AreCorrect()
        {
            Assert.IsNotNull(_fixture?.MainWindow);
            Assert.IsTrue(_fixture.MainWindow.BoundingRectangle.Width >= 600);
            Assert.IsTrue(_fixture.MainWindow.BoundingRectangle.Height >= 400);

            var appTitle = _fixture.MainWindow.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("AppTitleTextBlock"));
            Assert.IsNotNull(appTitle, "AppTitleTextBlock should be present in the custom title bar.");
            Assert.AreEqual("Japlayer", appTitle.Name);
        }

        [TestMethod]
        public void SearchFiltering_WithDebounce_FiltersAndRestoresCards()
        {
            WaitForInitialLoad();

            _libraryPage!.Search("Deep Ocean");
            Assert.IsTrue(_libraryPage.WaitForCardCount(1), "Only TEST-002 should match 'Deep Ocean'.");
            Assert.IsTrue(_libraryPage.WaitForItemCountText("1 of 3"), "ItemCountTextBlock should show '1 of 3'.");
            Assert.IsNotNull(_libraryPage.FindCard("TEST-002"));
            Assert.IsNull(_libraryPage.FindCard("TEST-001"));

            _libraryPage.ClearSearch();
            Assert.IsTrue(_libraryPage.WaitForCardCount(3), "All 3 cards should be restored after clearing search.");
            Assert.IsTrue(_libraryPage.WaitForItemCountText("3 items"), "ItemCountTextBlock should show '3 items'.");
        }

        [TestMethod]
        public void FacetFlyout_TriStateIncludeExcludeAndPerFlyoutClear_FiltersCorrectly()
        {
            WaitForInitialLoad();

            // 1. Open Tags flyout, filter for "VR", click once to Include (+)
            _libraryPage!.OpenFacetFlyout("Tags");
            _libraryPage.FilterInOpenFlyout("VR");
            _libraryPage.ClickFacetItem("VR");
            _libraryPage.CloseFlyout();

            Assert.IsTrue(_libraryPage.WaitForCardCount(2), "TEST-001 and TEST-002 should match VR tag.");
            Assert.IsNotNull(_libraryPage.FindCard("TEST-001"));
            Assert.IsNotNull(_libraryPage.FindCard("TEST-002"));
            Assert.IsNull(_libraryPage.FindCard("TEST-003"));

            // 2. Click VR again to Exclude (−) -> only TEST-003 remains
            _libraryPage.OpenFacetFlyout("Tags");
            _libraryPage.ClickFacetItem("VR");
            _libraryPage.CloseFlyout();

            Assert.IsTrue(_libraryPage.WaitForCardCount(1), "Only TEST-003 should remain when VR is excluded.");
            Assert.IsNotNull(_libraryPage.FindCard("TEST-003"));

            // 3. Use per-flyout Clear link to restore all 3 cards
            _libraryPage.OpenFacetFlyout("Tags");
            _libraryPage.ClickFacetClear();
            _libraryPage.CloseFlyout();

            Assert.IsTrue(_libraryPage.WaitForCardCount(3), "All 3 cards should be restored after per-flyout Clear.");
        }

        [TestMethod]
        public void FacetFlyout_DirectSwitchBetweenFilters_AndNoChipOverlap()
        {
            WaitForInitialLoad();

            // 1. Open Tags and select VR (without closing)
            _libraryPage!.OpenFacetFlyout("Tags");
            _libraryPage.ClickFacetItem("VR");

            // 2. Directly open Cast while Tags is still open (no intermediate CloseFlyout)
            _libraryPage.OpenFacetFlyout("Cast");
            _libraryPage.ClickFacetItem("Actor Alpha");
            _libraryPage.CloseFlyout();

            Assert.IsTrue(_libraryPage.WaitForCardCount(2), "Direct switch from Tags to Cast should apply both filters.");

            // 3. Verify filter buttons do not overlap the active filter chips bar below
            var tagsButton = _libraryPage.FindFacetButton("Tags");
            var clearAllButton = _libraryPage.ClearAllFiltersButton;
            Assert.IsNotNull(tagsButton);
            Assert.IsNotNull(clearAllButton);
            Assert.IsTrue(
                tagsButton.BoundingRectangle.Bottom <= clearAllButton.BoundingRectangle.Top,
                $"Filter button bottom ({tagsButton.BoundingRectangle.Bottom}) should sit above active chips ({clearAllButton.BoundingRectangle.Top}).");
        }

        [TestMethod]
        public void CastStudiosSeriesFacets_AndClearAllChips_WorkEndToEnd()
        {
            WaitForInitialLoad();

            // Include Actor Alpha (TEST-001, TEST-002)
            _libraryPage!.OpenFacetFlyout("Cast");
            _libraryPage.ClickFacetItem("Actor Alpha");
            _libraryPage.CloseFlyout();

            Assert.IsTrue(_libraryPage.WaitForCardCount(2), "TEST-001 and TEST-002 have Actor Alpha.");
            Assert.IsNotNull(_libraryPage.FindCard("TEST-001"));
            Assert.IsNotNull(_libraryPage.FindCard("TEST-002"));

            // Also include Studio Alpha -> narrows to TEST-001 only
            _libraryPage.OpenFacetFlyout("Studios");
            _libraryPage.ClickFacetItem("Studio Alpha");
            _libraryPage.CloseFlyout();

            Assert.IsTrue(_libraryPage.WaitForCardCount(1), "Only TEST-001 matches both Actor Alpha and Studio Alpha.");
            Assert.IsNotNull(_libraryPage.FindCard("TEST-001"));

            // Remove the first active chip (Actor Alpha) -> widens to TEST-001 + TEST-003 (both Studio Alpha)
            _libraryPage.RemoveFirstFilterChip();
            Assert.IsTrue(_libraryPage.WaitForCardCount(2), "Removing Actor Alpha chip should leave 2 Studio Alpha items.");

            // Clear remaining chip via Clear all
            _libraryPage.ClickClearAllFilters();
            Assert.IsTrue(_libraryPage.WaitForCardCount(3), "All 3 cards should be restored after Clear all.");
        }

        [TestMethod]
        public void RangeFilters_RatingAndRuntime_FilterCards()
        {
            WaitForInitialLoad();

            // Click Rating track at ~40% from left (closer to min thumb -> Rating >= 2, excludes TEST-003 at rating 1)
            _libraryPage!.AdjustRangeSlider(_libraryPage.RatingFilterButton, "RatingRangeText", minDeltaFraction: 0.4, maxDeltaFraction: 0);

            Assert.IsTrue(_libraryPage.WaitForCardRemoved("TEST-003"), "TEST-003 (rating 1) should be filtered out.");
            Assert.IsNotNull(_libraryPage.FindCard("TEST-001"));

            // Reset Rating via the Reset link inside the Rating flyout
            _libraryPage.ResetRangeFilter(_libraryPage.RatingFilterButton, "ResetRatingButton");
            Assert.IsTrue(_libraryPage.WaitForCardCount(3), "ResetRatingButton should restore all 3 cards.");

            // Click Runtime track at ~45% left of max thumb (~95m -> snaps <= 90m, excludes TEST-003 at 110m)
            _libraryPage.AdjustRangeSlider(_libraryPage.RuntimeFilterButton, "RuntimeRangeText", minDeltaFraction: 0, maxDeltaFraction: -0.45);

            Assert.IsTrue(_libraryPage.WaitForCardRemoved("TEST-003"), "TEST-003 (110m) should be filtered out.");

            _libraryPage.ResetRangeFilter(_libraryPage.RuntimeFilterButton, "ResetRuntimeButton");
            Assert.IsTrue(_libraryPage.WaitForCardCount(3), "ResetRuntimeButton should restore all 3 cards.");
        }

        [TestMethod]
        public void SortDropDownAndDirection_SortsAndHidesDirectionOnRandom()
        {
            WaitForInitialLoad();

            Assert.IsNotNull(_libraryPage!.SortDropDownButton, "SortDropDownButton should be present.");
            Assert.IsNotNull(_libraryPage.SortDirectionButton, "SortDirectionButton should be visible for non-Random sort.");

            // Default is AddedDateDescending (TEST-001, TEST-002, TEST-003).
            // Selecting Title with descending still active sorts Z-A (TEST-003 first).
            _libraryPage.SelectSortField("Title");
            Assert.IsTrue(_libraryPage.WaitForFirstCardId("TEST-003"), "Title descending should place TEST-003 first.");

            // Toggling direction sorts A-Z (TEST-001 first).
            _libraryPage.ToggleSortDirection();
            Assert.IsTrue(_libraryPage.WaitForFirstCardId("TEST-001"), "Title ascending should place TEST-001 first.");

            // Selecting Random (via ShuffleButton) hides SortDirectionButton
            _libraryPage.ClickShuffle();
            Assert.IsTrue(_libraryPage.WaitUntilHidden(() => _libraryPage.SortDirectionButton), "SortDirectionButton should be hidden when Random sort is active.");
        }

        [TestMethod]
        public void EmptyState_WhenNoMatches_ShowsResetButtonAndRestoresCards()
        {
            WaitForInitialLoad();

            _libraryPage!.Search("NONEXISTENT_QUERY_XYZ");
            Assert.IsTrue(_libraryPage.WaitForCardCount(0), "No cards should match nonexistent query.");
            Assert.IsNotNull(_libraryPage.WaitForElement(() => _libraryPage.ResetAllFiltersButton), "ResetAllFiltersButton should appear in empty state.");

            _libraryPage.ClickResetAllFilters();
            Assert.IsTrue(_libraryPage.WaitForCardCount(3), "All 3 cards should be restored after Reset all filters.");
        }

        [TestMethod]
        public void Navigation_ClickCardNavigatesToMediaItemPage_AndBackButtonReturns()
        {
            WaitForInitialLoad();

            _libraryPage!.ClickCard("TEST-001");

            var mediaItemPage = new MediaItemPageObject(_fixture!.MainWindow!);
            var backButton = _libraryPage.WaitForElement(() => mediaItemPage.BackButton);
            Assert.IsNotNull(backButton, "BackButton should appear on MediaItemPage.");

            mediaItemPage.ClickBack();

            var searchBox = _libraryPage.WaitForElement(() => _libraryPage.SearchBox);
            Assert.IsNotNull(searchBox, "SearchBox should be visible after navigating back to LibraryPage.");
        }

        [TestMethod]
        public void ExternalDatabaseInsert_AutoReloadsWhilePreservingActiveFilter()
        {
            WaitForInitialLoad();

            // Filter by Studio Alpha -> 2 cards (TEST-001, TEST-003)
            _libraryPage!.OpenFacetFlyout("Studios");
            _libraryPage.ClickFacetItem("Studio Alpha");
            _libraryPage.CloseFlyout();
            Assert.IsTrue(_libraryPage.WaitForCardCount(2), "Only TEST-001 and TEST-003 have Studio Alpha initially.");

            // Insert TEST-004 with Studio Alpha directly into SQLite while app is open
            var options = Microsoft.EntityFrameworkCore.SqliteDbContextOptionsBuilderExtensions
                .UseSqlite(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Data.Context.DatabaseContext>(), $"Data Source={_fixture!.DatabasePath}")
                .AddInterceptors(new Data.Context.SqlitePragmaInterceptor())
                .Options;

            using (var context = new Data.Context.DatabaseContext(options))
            {
                var studioAlpha = context.MediaStudios.First(studio => studio.Name == "Studio Alpha");
                var newMedia = new Data.Entities.Media { MediaId = "TEST-004" };
                newMedia.MediaLocations.Add(new Data.Entities.MediaLocation
                {
                    MediaId = "TEST-004",
                    Scene = 1,
                    Hostname = "localhost",
                    Path = _fixture.DummyVideoPath
                });
                newMedia.MediaMetadata.Add(new Data.Entities.MediaMetadata
                {
                    MediaId = "TEST-004",
                    MetadataUrl = "https://example.com/meta4",
                    Title = "External Watch Reloaded Movie"
                });
                newMedia.Studios.Add(studioAlpha);
                context.Media.Add(newMedia);
                context.SaveChanges();
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // DatabaseChangeMonitor should detect external DB/WAL write and reload without clearing Studio Alpha filter
            Assert.IsNotNull(_libraryPage.WaitForCard("TEST-004", TimeSpan.FromSeconds(8)), "External insert TEST-004 should auto-reload into LibraryPage.");
            Assert.IsTrue(_libraryPage.WaitForCardCount(3), "Studio Alpha filter should remain active and show 3 matching cards.");
        }
    }
}
