using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace Japlayer.UITests.PageObjects
{
    public class LibraryPageObject(Window window)
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
        private readonly Window _window = window;

        public TextBox? SearchBox =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("SearchAutoSuggestBox"))
                   ?.FindFirstDescendant(conditionFactory => conditionFactory.ByControlType(ControlType.Edit))?.AsTextBox()
            ?? _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("SearchAutoSuggestBox"))?.AsTextBox();

        public Button? ShuffleButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("ShuffleButton"))?.AsButton();

        public Button? SortDropDownButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("SortDropDownButton"))?.AsButton();

        public Button? SortDirectionButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("SortDirectionButton"))?.AsButton();

        public Button? RatingFilterButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("RatingFilterButton"))?.AsButton();

        public Button? RuntimeFilterButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("RuntimeFilterButton"))?.AsButton();

        public AutomationElement? ClearAllFiltersButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("ClearAllFiltersButton"));

        public AutomationElement? EmptyStatePanel =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("EmptyStatePanel"));

        public Button? ResetAllFiltersButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("ResetAllFiltersButton"))?.AsButton();

        public AutomationElement? ItemCountTextBlock =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("ItemCountTextBlock"));

        public AutomationElement? MediaGrid =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("MediaGrid"));

        public Button? FindFacetButton(string facetLabel) =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId(facetLabel))?.AsButton();

        public AutomationElement? FindCard(string mediaId)
        {
            var grid = MediaGrid;
            if (grid == null)
            {
                return null;
            }

            var items = grid.FindAllDescendants(conditionFactory => conditionFactory.ByControlType(ControlType.ListItem));
            foreach (var item in items)
            {
                if (item.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId(mediaId)) != null)
                {
                    return item;
                }

                var textElements = item.FindAllDescendants(conditionFactory => conditionFactory.ByControlType(ControlType.Text));
                if (textElements.Any(textElement =>
                    textElement.Properties.Name.ValueOrDefault?.Contains(mediaId, StringComparison.OrdinalIgnoreCase) == true))
                {
                    return item;
                }
            }

            return null;
        }

        public AutomationElement? WaitForCard(string mediaId, TimeSpan? timeout = null) =>
            Retry.WhileNull(() => FindCard(mediaId), timeout ?? DefaultTimeout, PollInterval).Result;

        public bool WaitForCardRemoved(string mediaId, TimeSpan? timeout = null) =>
            Retry.WhileTrue(() => FindCard(mediaId) != null, timeout ?? DefaultTimeout, PollInterval).Success;

        public bool WaitForCardCount(int expectedCount, TimeSpan? timeout = null) =>
            Retry.WhileFalse(() => GetAllVisibleCards().Count == expectedCount, timeout ?? DefaultTimeout, PollInterval).Success;

        public T? WaitForElement<T>(Func<T?> selector, TimeSpan? timeout = null) where T : class =>
            Retry.WhileNull(selector, timeout ?? DefaultTimeout, PollInterval).Result;

        public bool WaitUntilHidden(Func<AutomationElement?> selector, TimeSpan? timeout = null) =>
            Retry.WhileTrue(() => selector() != null, timeout ?? DefaultTimeout, PollInterval).Success;

        public string GetItemCountText() =>
            ItemCountTextBlock?.Name ?? string.Empty;

        public bool WaitForItemCountText(string expectedText, TimeSpan? timeout = null) =>
            Retry.WhileFalse(() => string.Equals(GetItemCountText(), expectedText, StringComparison.Ordinal), timeout ?? DefaultTimeout, PollInterval).Success;

        public List<string> GetVisibleCardIds()
        {
            var ids = new List<string>();
            foreach (var item in GetAllVisibleCards())
            {
                var titleText = item.FindAllDescendants(conditionFactory => conditionFactory.ByControlType(ControlType.Text))
                    .Select(textElement => textElement.Properties.Name.ValueOrDefault)
                    .FirstOrDefault(name => !string.IsNullOrEmpty(name) && name.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase));
                if (titleText != null)
                {
                    ids.Add(titleText.Split(' ')[0]);
                }
            }

            return ids;
        }

        public bool WaitForFirstCardId(string expectedFirstId, TimeSpan? timeout = null) =>
            Retry.WhileFalse(
                () => GetVisibleCardIds().FirstOrDefault()?.Equals(expectedFirstId, StringComparison.OrdinalIgnoreCase) == true,
                timeout ?? DefaultTimeout,
                PollInterval).Success;

        public List<AutomationElement> GetAllVisibleCards()
        {
            var grid = MediaGrid;
            if (grid == null)
            {
                return [];
            }

            return [.. grid.FindAllDescendants(conditionFactory => conditionFactory.ByControlType(ControlType.ListItem))];
        }

        public void Search(string query)
        {
            var search = RequireElement(SearchBox, nameof(SearchBox));
            search.Focus();
            search.Text = query;
            Wait.UntilInputIsProcessed();
        }

        public void ClearSearch()
        {
            var search = RequireElement(SearchBox, nameof(SearchBox));
            search.Focus();
            search.Text = string.Empty;
            Wait.UntilInputIsProcessed();
        }

        public void OpenFacetFlyout(string facetLabel)
        {
            var button = RequireElement(FindFacetButton(facetLabel), $"Facet button '{facetLabel}'");
            ActivateElement(button);
            Wait.UntilInputIsProcessed();
            RequireElement(
                WaitForElement(() => _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("FacetSearchTextBox"))),
                "FacetSearchTextBox inside flyout");
        }

        public void FilterInOpenFlyout(string query)
        {
            var searchBox = RequireElement(
                WaitForElement(() => _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("FacetSearchTextBox"))?.AsTextBox()),
                "FacetSearchTextBox");
            searchBox.Text = query;
            Wait.UntilInputIsProcessed();
        }

        public void ClickFacetItem(string itemName)
        {
            var itemButton = RequireElement(
                WaitForElement(() => _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId(itemName))?.AsButton()),
                $"Facet item '{itemName}'");
            ActivateElement(itemButton);
            Wait.UntilInputIsProcessed();
        }

        public void ClickFacetClear()
        {
            var clearButton = RequireElement(
                WaitForElement(() => _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("FacetClearButton"))),
                "FacetClearButton");
            ActivateElement(clearButton);
            Wait.UntilInputIsProcessed();
        }

        public void CloseFlyout()
        {
            foreach (var label in new[] { "Tags", "Genres", "Cast", "Studios", "Series", "RatingFilterButton", "RuntimeFilterButton", "SortDropDownButton" })
            {
                var button = _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId(label));
                if (button != null && button.Patterns.ExpandCollapse.IsSupported &&
                    button.Patterns.ExpandCollapse.Pattern.ExpandCollapseState == ExpandCollapseState.Expanded)
                {
                    button.Patterns.ExpandCollapse.Pattern.Collapse();
                    Wait.UntilInputIsProcessed();
                    return;
                }
            }

            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Wait.UntilInputIsProcessed();
        }

        public void SelectSortField(string fieldDisplayName)
        {
            var sortButton = RequireElement(SortDropDownButton, nameof(SortDropDownButton));
            ActivateElement(sortButton);
            Wait.UntilInputIsProcessed();

            var targetItem = RequireElement(
                WaitForElement(() =>
                {
                    var listItems = _window.FindAllDescendants(conditionFactory => conditionFactory.ByControlType(ControlType.ListItem));
                    return listItems.FirstOrDefault(item =>
                        item.Name.Contains(fieldDisplayName, StringComparison.OrdinalIgnoreCase) ||
                        (item.FindFirstDescendant(conditionFactory => conditionFactory.ByControlType(ControlType.Text))?.Name?.Contains(fieldDisplayName, StringComparison.OrdinalIgnoreCase) ?? false));
                }),
                $"Sort option '{fieldDisplayName}'");

            ActivateElement(targetItem);
            Wait.UntilInputIsProcessed();
        }

        public void ToggleSortDirection()
        {
            var directionButton = RequireElement(SortDirectionButton, nameof(SortDirectionButton));
            ActivateElement(directionButton);
            Wait.UntilInputIsProcessed();
        }

        public void ClickShuffle()
        {
            var shuffleButton = RequireElement(ShuffleButton, nameof(ShuffleButton));
            ActivateElement(shuffleButton);
            Wait.UntilInputIsProcessed();
        }

        public void AdjustRangeSlider(Button? filterButton, string flyoutMarkerAutomationId, double minDeltaFraction, double maxDeltaFraction)
        {
            _window.SetForeground();
            var button = RequireElement(filterButton, $"Range filter button for '{flyoutMarkerAutomationId}'");
            ActivateElement(button);
            Wait.UntilInputIsProcessed();

            var rangeTextElement = RequireElement(
                WaitForElement(() => _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId(flyoutMarkerAutomationId))),
                flyoutMarkerAutomationId);
            var initialRangeText = rangeTextElement.Name;

            // Allow Flyout entrance animation to settle before reading thumb coordinates
            Thread.Sleep(250);

            var thumbs = _window.FindAllDescendants(conditionFactory => conditionFactory.ByControlType(ControlType.Thumb))
                .Where(thumb => thumb.BoundingRectangle.Width > 0 && thumb.BoundingRectangle.Width <= 40)
                .OrderBy(thumb => thumb.BoundingRectangle.Left)
                .ToArray();

            if (thumbs.Length < 2)
            {
                throw new InvalidOperationException($"Expected 2 range thumbs for '{flyoutMarkerAutomationId}', found {thumbs.Length}.");
            }

            var minThumb = thumbs[0];
            var maxThumb = thumbs[^1];
            var minCenter = minThumb.BoundingRectangle.Center();
            var maxCenter = maxThumb.BoundingRectangle.Center();
            var trackWidth = Math.Max(100, maxCenter.X - minCenter.X);

            if (minDeltaFraction != 0)
            {
                var targetX = minCenter.X + (int)Math.Round(trackWidth * minDeltaFraction);
                Mouse.LeftClick(new System.Drawing.Point(targetX, minCenter.Y));
                Wait.UntilInputIsProcessed();
            }

            if (maxDeltaFraction != 0)
            {
                var targetX = maxCenter.X + (int)Math.Round(trackWidth * maxDeltaFraction);
                Mouse.LeftClick(new System.Drawing.Point(targetX, maxCenter.Y));
                Wait.UntilInputIsProcessed();
            }

            Retry.WhileFalse(() => rangeTextElement.Name != initialRangeText, TimeSpan.FromSeconds(2), PollInterval);

            CloseFlyout();
        }

        public void ResetRangeFilter(Button? filterButton, string resetButtonAutomationId)
        {
            var button = RequireElement(filterButton, $"Range filter button for '{resetButtonAutomationId}'");
            ActivateElement(button);
            Wait.UntilInputIsProcessed();

            var resetButton = RequireElement(
                WaitForElement(() => _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId(resetButtonAutomationId))),
                resetButtonAutomationId);
            ActivateElement(resetButton);
            Wait.UntilInputIsProcessed();

            CloseFlyout();
        }

        public void RemoveFirstFilterChip()
        {
            var removeButton = RequireElement(
                WaitForElement(() => _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("RemoveFilterChipButton"))),
                "RemoveFilterChipButton");
            ActivateElement(removeButton);
            Wait.UntilInputIsProcessed();
        }

        public void ClickClearAllFilters()
        {
            var clearButton = RequireElement(
                WaitForElement(() => ClearAllFiltersButton),
                nameof(ClearAllFiltersButton));
            ActivateElement(clearButton);
            Wait.UntilInputIsProcessed();
        }

        public void ClickResetAllFilters()
        {
            var resetButton = RequireElement(
                WaitForElement(() => ResetAllFiltersButton),
                nameof(ResetAllFiltersButton));
            ActivateElement(resetButton);
            Wait.UntilInputIsProcessed();
        }

        public void ClickCard(string mediaId)
        {
            var card = RequireElement(WaitForCard(mediaId), $"Card '{mediaId}'");
            ActivateElement(card);
            Wait.UntilInputIsProcessed();
        }

        private static void ActivateElement(AutomationElement element)
        {
            if (element.Patterns.Invoke.IsSupported)
            {
                element.Patterns.Invoke.Pattern.Invoke();
            }
            else if (element.Patterns.ExpandCollapse.IsSupported)
            {
                element.Patterns.ExpandCollapse.Pattern.Expand();
            }
            else if (element.Patterns.SelectionItem.IsSupported)
            {
                element.Patterns.SelectionItem.Pattern.Select();
            }
            else
            {
                element.Click();
            }
        }

        private static T RequireElement<T>(T? element, string description) where T : class =>
            element ?? throw new InvalidOperationException($"Expected UI element '{description}' was not found.");
    }
}
