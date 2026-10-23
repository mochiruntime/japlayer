using FlaUI.Core.Tools;
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

        [TestMethod]
        public void MainWindow_TitleAndBasicProperties_AreCorrect()
        {
            Assert.IsNotNull(_fixture?.MainWindow);
            Assert.IsTrue(_fixture.MainWindow.BoundingRectangle.Width >= 600);
            Assert.IsTrue(_fixture.MainWindow.BoundingRectangle.Height >= 400);

            // In WinUI 3 with custom title bars, the title is hosted in AppTitleTextBlock
            var appTitle = _fixture.MainWindow.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("AppTitleTextBlock"));
            Assert.IsNotNull(appTitle, "AppTitleTextBlock should be present in the custom title bar.");
            Assert.AreEqual("Japlayer", appTitle.Name);
        }

        [TestMethod]
        public void SearchFiltering_WithDebounce_FiltersAndRestoresCards()
        {
            Assert.IsNotNull(_libraryPage);

            // Wait for initial cards to appear
            var initialCard = Retry.WhileNull(
                () => _libraryPage.FindCard("TEST-001"),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(250)).Result;
            Assert.IsNotNull(initialCard, "Initial card TEST-001 should be present.");

            // Perform search
            _libraryPage.Search("Beta");
            Thread.Sleep(500);

            var betaCard = _libraryPage.FindCard("TEST-002");
            Assert.IsNotNull(betaCard, "Card TEST-002 should be visible when searching 'Beta'.");

            // Verify TEST-001 is not visible
            var alphaCard = _libraryPage.FindCard("TEST-001");
            Assert.IsNull(alphaCard, "Card TEST-001 should be filtered out when searching 'Beta'.");

            // Clear search
            _libraryPage.ClearSearch();
            Thread.Sleep(500);

            var restoredCard = Retry.WhileNull(
                () => _libraryPage.FindCard("TEST-001"),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(250)).Result;
            Assert.IsNotNull(restoredCard, "Card TEST-001 should be restored after clearing search.");
        }

        [TestMethod]
        public void SortComboBox_CanSelectOptions()
        {
            Assert.IsNotNull(_libraryPage);

            var comboBox = _libraryPage.SortComboBox;
            Assert.IsNotNull(comboBox, "Sort ComboBox should be present.");

            _libraryPage.SelectSortOption("ID");
            Thread.Sleep(300);

            Assert.IsNotNull(_libraryPage.FindCard("TEST-001"));
        }

        [TestMethod]
        public void Navigation_ClickCardNavigatesToMediaItemPage_AndBackButtonReturns()
        {
            Assert.IsNotNull(_libraryPage);
            Assert.IsNotNull(_fixture?.MainWindow);

            var card = Retry.WhileNull(
                () => _libraryPage.FindCard("TEST-001"),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(250)).Result;
            Assert.IsNotNull(card);

            _libraryPage.ClickCard("TEST-001");
            Thread.Sleep(500);

            var mediaItemPage = new MediaItemPageObject(_fixture.MainWindow);
            var backButton = Retry.WhileNull(
                () => mediaItemPage.BackButton,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(250)).Result;

            Assert.IsNotNull(backButton, "BackButton should appear on MediaItemPage.");

            mediaItemPage.ClickBack();
            Thread.Sleep(500);

            var searchBox = Retry.WhileNull(
                () => _libraryPage.SearchBox,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(250)).Result;
            Assert.IsNotNull(searchBox, "SearchBox should be visible after navigating back to LibraryPage.");
        }
    }
}
