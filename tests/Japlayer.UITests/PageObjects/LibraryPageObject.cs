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

        public AutomationElement? ActiveFiltersBar =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("ActiveFiltersBar"));

        public AutomationElement[] GetActiveFilterChipRemoveButtons() =>
            _window.FindAllDescendants(conditionFactory => conditionFactory.ByAutomationId("RemoveFilterChipButton"));

        public Button? SavePresetButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("SavePresetButton"))?.AsButton();

        public AutomationElement? LibraryPresetsList =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("LibraryPresetsList"));

        public Button? FindPresetPill(string partialName)
        {
            var container = LibraryPresetsList ?? (AutomationElement)_window;
            return container.FindAllDescendants(conditionFactory => conditionFactory.ByControlType(ControlType.Button))
                .FirstOrDefault(button =>
                    button.Properties.Name.ValueOrDefault?.Contains(partialName, StringComparison.OrdinalIgnoreCase) == true ||
                    button.Properties.AutomationId.ValueOrDefault?.Contains(partialName, StringComparison.OrdinalIgnoreCase) == true)
                ?.AsButton();
        }

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

            // LibraryPage.xaml binds AutomationProperties.AutomationId="{x:Bind Id}" on the card's root Grid.
            var cardContent = grid.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId(mediaId));
            if (cardContent != null)
            {
                var current = cardContent;
                while (current != null && current.ControlType != ControlType.ListItem)
                {
                    current = current.Parent;
                }

                if (current != null)
                {
                    return current;
                }
            }

            var items = grid.FindAllDescendants(conditionFactory => conditionFactory.ByControlType(ControlType.ListItem));
            foreach (var item in items)
            {
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
                var directChildId = item.FindFirstChild()?.Properties.AutomationId.ValueOrDefault;
                if (!string.IsNullOrEmpty(directChildId) && directChildId.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase))
                {
                    ids.Add(directChildId);
                    continue;
                }

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
        }

        public void ClearSearch()
        {
            var search = RequireElement(SearchBox, nameof(SearchBox));
            search.Focus();
            search.Text = string.Empty;
        }

        public void OpenFacetFlyout(string facetLabel)
        {
            var button = RequireElement(FindFacetButton(facetLabel), $"Facet button '{facetLabel}'");
            ActivateElement(button);
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
        }

        public void CloseFlyout()
        {
            if (CloseAnyOpenFlyout())
            {
                return;
            }

            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Wait.UntilInputIsProcessed();
        }

        public bool CloseAnyOpenFlyout()
        {
            var flyoutIds = new HashSet<string>(StringComparer.Ordinal)
            {
                "Tags", "Genres", "Cast", "Studios", "Series",
                "RatingFilterButton", "RuntimeFilterButton", "SortDropDownButton"
            };

            foreach (var button in _window.FindAllDescendants(conditionFactory => conditionFactory.ByControlType(ControlType.Button)))
            {
                var automationId = button.Properties.AutomationId.ValueOrDefault;
                if (automationId != null &&
                    flyoutIds.Contains(automationId) &&
                    button.Patterns.ExpandCollapse.IsSupported &&
                    button.Patterns.ExpandCollapse.Pattern.ExpandCollapseState == ExpandCollapseState.Expanded)
                {
                    button.Patterns.ExpandCollapse.Pattern.Collapse();
                    Retry.WhileTrue(
                        () => button.Patterns.ExpandCollapse.Pattern.ExpandCollapseState == ExpandCollapseState.Expanded,
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromMilliseconds(25));
                    return true;
                }
            }

            return false;
        }

        public void SelectSortField(string fieldDisplayName)
        {
            var sortButton = RequireElement(SortDropDownButton, nameof(SortDropDownButton));
            ActivateElement(sortButton);

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
        }

        public void ToggleSortDirection()
        {
            var directionButton = RequireElement(SortDirectionButton, nameof(SortDirectionButton));
            ActivateElement(directionButton);
        }

        public void ClickShuffle()
        {
            var shuffleButton = RequireElement(ShuffleButton, nameof(ShuffleButton));
            ActivateElement(shuffleButton);
        }

        public void AdjustRangeSlider(Button? filterButton, string flyoutMarkerAutomationId, double minDeltaFraction, double maxDeltaFraction)
        {
            _window.SetForeground();
            var button = RequireElement(filterButton, $"Range filter button for '{flyoutMarkerAutomationId}'");
            ActivateElement(button);

            var rangeTextElement = RequireElement(
                WaitForElement(() => _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId(flyoutMarkerAutomationId))),
                flyoutMarkerAutomationId);
            var initialRangeText = rangeTextElement.Name;

            var thumbs = Retry.WhileNull(() =>
            {
                var found = _window.FindAllDescendants(conditionFactory => conditionFactory.ByControlType(ControlType.Thumb))
                    .Where(thumb => thumb.BoundingRectangle.Width > 0 && thumb.BoundingRectangle.Width <= 40)
                    .OrderBy(thumb => thumb.BoundingRectangle.Left)
                    .ToArray();
                return found.Length >= 2 ? found : null;
            }, DefaultTimeout, PollInterval).Result;

            if (thumbs == null || thumbs.Length < 2)
            {
                throw new InvalidOperationException($"Expected 2 range thumbs for '{flyoutMarkerAutomationId}', found {thumbs?.Length ?? 0}.");
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

            var resetButton = RequireElement(
                WaitForElement(() => _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId(resetButtonAutomationId))),
                resetButtonAutomationId);
            ActivateElement(resetButton);

            CloseFlyout();
        }

        public void RemoveFirstFilterChip()
        {
            var removeButton = RequireElement(
                WaitForElement(() => _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("RemoveFilterChipButton"))),
                "RemoveFilterChipButton");
            ActivateElement(removeButton);
        }

        public void ClickClearAllFilters()
        {
            var clearButton = RequireElement(
                WaitForElement(() => ClearAllFiltersButton),
                nameof(ClearAllFiltersButton));
            ActivateElement(clearButton);
        }

        public void ClickResetAllFilters()
        {
            var resetButton = RequireElement(
                WaitForElement(() => ResetAllFiltersButton),
                nameof(ResetAllFiltersButton));
            ActivateElement(resetButton);
        }

        public void ClickCard(string mediaId)
        {
            var card = RequireElement(WaitForCard(mediaId), $"Card '{mediaId}'");
            ActivateElement(card);
        }

        public void ScrollCardIntoView(string mediaId)
        {
            var card = RequireElement(WaitForCard(mediaId), $"Card '{mediaId}'");
            if (card.Patterns.ScrollItem.IsSupported)
            {
                card.Patterns.ScrollItem.Pattern.ScrollIntoView();
                Wait.UntilInputIsProcessed();
            }
        }

        public bool IsCardSelected(string mediaId)
        {
            var card = FindCard(mediaId);
            return card != null &&
                   card.Patterns.SelectionItem.IsSupported &&
                   card.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault;
        }

        public bool IsCardInViewport(string mediaId)
        {
            var grid = MediaGrid;
            var card = FindCard(mediaId);
            if (grid == null || card == null || card.IsOffscreen)
            {
                return false;
            }

            var gridRect = grid.BoundingRectangle;
            var cardRect = card.BoundingRectangle;
            return cardRect.Height > 0 &&
                   cardRect.Bottom > gridRect.Top &&
                   cardRect.Top < gridRect.Bottom;
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
