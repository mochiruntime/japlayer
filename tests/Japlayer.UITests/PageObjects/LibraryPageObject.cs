using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;

namespace Japlayer.UITests.PageObjects
{
    public class LibraryPageObject(Window window)
    {
        private readonly Window _window = window;

        public TextBox? SearchBox =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("SearchAutoSuggestBox"))
                   ?.FindFirstDescendant(conditionFactory => conditionFactory.ByControlType(ControlType.Edit))?.AsTextBox()
            ?? _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("SearchAutoSuggestBox"))?.AsTextBox();

        public ComboBox? SortComboBox =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("SortComboBox"))?.AsComboBox();

        public TextBox? TagSearchBox =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("TagSearchTextBox"))?.AsTextBox();

        public TextBox? GenreSearchBox =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("GenreSearchTextBox"))?.AsTextBox();

        public AutomationElement? MediaGrid =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("MediaGrid"));

        public AutomationElement? FindCard(string mediaId)
        {
            return Retry.WhileNull(
                () =>
                {
                    var grid = MediaGrid;
                    if (grid == null)
                    {
                        return null;
                    }

                    var items = grid.FindAllDescendants(conditionFactory => conditionFactory.ByControlType(ControlType.ListItem));
                    foreach (var item in items)
                    {
                        var textElement = item.FindFirstDescendant(conditionFactory => conditionFactory.ByControlType(ControlType.Text));
                        var textName = textElement?.Properties.Name.ValueOrDefault;
                        if (textName != null && textName.Contains(mediaId, StringComparison.OrdinalIgnoreCase))
                        {
                            return item;
                        }
                    }

                    return null;
                },
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(200)).Result;
        }

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
            var search = SearchBox;
            if (search != null)
            {
                search.Focus();
                search.Text = query;
                Wait.UntilInputIsProcessed();
                Thread.Sleep(500);
            }
        }

        public void ClearSearch()
        {
            var search = SearchBox;
            if (search != null)
            {
                search.Focus();
                search.Text = string.Empty;
                Wait.UntilInputIsProcessed();
                Thread.Sleep(500);
            }
        }

        public void SelectSortOption(string optionText)
        {
            var comboBox = SortComboBox;
            if (comboBox != null)
            {
                comboBox.Expand();
                Wait.UntilInputIsProcessed();
                var targetItem = comboBox.Items.FirstOrDefault(comboBoxItem => comboBoxItem.Text.Contains(optionText, StringComparison.OrdinalIgnoreCase));
                if (targetItem != null)
                {
                    targetItem.Select();
                    Wait.UntilInputIsProcessed();
                    Thread.Sleep(200);
                }
                else
                {
                    comboBox.Collapse();
                }
            }
        }

        public CheckBox? FindTagCheckbox(string tagName) => _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId(tagName))?.AsCheckBox();

        public void ClickCard(string mediaId)
        {
            var card = FindCard(mediaId);
            if (card != null)
            {
                if (card.Patterns.Invoke.IsSupported)
                {
                    card.Patterns.Invoke.Pattern.Invoke();
                }
                else
                {
                    card.Click();
                }
                Wait.UntilInputIsProcessed();
                Thread.Sleep(500);
            }
        }
    }
}
