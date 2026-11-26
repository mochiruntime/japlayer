using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace Japlayer.UITests.PageObjects
{
    public class MediaItemPageObject(Window window)
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
        private readonly Window _window = window;

        public Button? BackButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("BackButton"))?.AsButton();

        public AutomationElement? TitleText =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("MediaItemTitle"));

        public AutomationElement? FactsSummaryText =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("MediaItemFactsSummary"));

        public AutomationElement? RatingControl =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("ItemRatingControl"));

        public AutomationElement? RatingDisplayText =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("RatingDisplayText"));

        public TextBox? TagSearchBox =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("TagAutoSuggestBox"))
                   ?.FindFirstDescendant(conditionFactory => conditionFactory.ByControlType(ControlType.Edit))?.AsTextBox()
            ?? _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("TagAutoSuggestBox"))?.AsTextBox();

        public Button? GalleryImageButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("GalleryImageButton"))?.AsButton();

        public AutomationElement? LightboxCounterText =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("LightboxCounterText"));

        public Button? LightboxPreviousButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("LightboxPreviousButton"))?.AsButton();

        public Button? LightboxNextButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("LightboxNextButton"))?.AsButton();

        public Button? LightboxCloseButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("LightboxCloseButton"))?.AsButton();

        public Button? FocusViewButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("FocusViewButton"))?.AsButton();

        public Button? GridViewButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("GridViewButton"))?.AsButton();

        public AutomationElement? SelectedSceneTitle =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("SelectedSceneTitle"));

        public Button? SceneOpenInButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("SceneOpenInButton"))?.AsButton();

        public Button? PlayOverlayButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("PlayOverlayButton"))?.AsButton();

        public AutomationElement? PlayerElement =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("Player"));

        public Button? SlowSeekBackwardButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("SlowSeekBackwardButton"))?.AsButton();

        public Button? SlowSeekForwardButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("SlowSeekForwardButton"))?.AsButton();

        public Button? FastSeekBackwardButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("FastSeekBackwardButton"))?.AsButton();

        public Button? FastSeekForwardButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("FastSeekForwardButton"))?.AsButton();

        public Button? SaveHighlightButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("SaveHighlightButton"))?.AsButton();

        public Button? SkipToNextHighlightButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("SkipToNextHighlightButton"))?.AsButton();

        public Button? FullWindowButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("FullWindowButton"))?.AsButton();

        public T? WaitForElement<T>(Func<T?> selector, TimeSpan? timeout = null) where T : class =>
            Retry.WhileNull(selector, timeout ?? DefaultTimeout, PollInterval).Result;

        public bool WaitUntilHidden(Func<AutomationElement?> selector, TimeSpan? timeout = null) =>
            Retry.WhileTrue(() => selector() != null, timeout ?? DefaultTimeout, PollInterval).Success;

        public void SetRating(int stars)
        {
            var rating = RequireElement(WaitForElement(() => RatingControl), nameof(RatingControl));
            if (rating.Patterns.RangeValue.IsSupported)
            {
                rating.Patterns.RangeValue.Pattern.SetValue(stars);
                Wait.UntilInputIsProcessed();
            }
            else
            {
                throw new InvalidOperationException("RatingControl does not support RangeValue pattern.");
            }
        }

        public void AddTag(string tagName)
        {
            _window.SetForeground();
            var box = RequireElement(WaitForElement(() => TagSearchBox), nameof(TagSearchBox));
            box.Focus();
            box.Text = tagName;
            Wait.UntilInputIsProcessed();
            Keyboard.Type(VirtualKeyShort.RETURN);
            Wait.UntilInputIsProcessed();
        }

        public void FocusTagSearchBox()
        {
            _window.SetForeground();
            var box = RequireElement(WaitForElement(() => TagSearchBox), nameof(TagSearchBox));
            box.Focus();
            Wait.UntilInputIsProcessed();
        }

        public bool SelectTagSuggestion(string tagName)
        {
            var suggestionItem = WaitForElement(() =>
                _window.FindAllDescendants(conditionFactory => conditionFactory.ByControlType(ControlType.ListItem))
                    .FirstOrDefault(item => string.Equals(item.Name, tagName, StringComparison.OrdinalIgnoreCase)
                        || item.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                            .Any(text => string.Equals(text.Name, tagName, StringComparison.OrdinalIgnoreCase))));
            if (suggestionItem == null)
            {
                return false;
            }

            if (suggestionItem.Patterns.Invoke.IsSupported)
            {
                suggestionItem.Patterns.Invoke.Pattern.Invoke();
            }
            else if (suggestionItem.Patterns.SelectionItem.IsSupported)
            {
                suggestionItem.Patterns.SelectionItem.Pattern.Select();
            }
            else
            {
                ActivateElement(suggestionItem);
            }

            Wait.UntilInputIsProcessed();
            return true;
        }

        public string GetTagSearchBoxText() => TagSearchBox?.Text ?? string.Empty;

        public bool HasTagText(string tagName) =>
            _window.FindAllDescendants(conditionFactory => conditionFactory.ByControlType(ControlType.Text))
                .Any(textElement => string.Equals(textElement.Properties.Name.ValueOrDefault, tagName, StringComparison.OrdinalIgnoreCase));

        public void RemoveFirstTag()
        {
            var removeButton = RequireElement(
                WaitForElement(() => _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("RemoveTagButton"))?.AsButton()),
                "RemoveTagButton");
            ActivateElement(removeButton);
            Wait.UntilInputIsProcessed();
        }

        public void OpenGalleryLightbox()
        {
            var imageButton = RequireElement(WaitForElement(() => GalleryImageButton), nameof(GalleryImageButton));
            ActivateElement(imageButton);
            Wait.UntilInputIsProcessed();
        }

        public void ClickLightboxNext()
        {
            var nextButton = RequireElement(WaitForElement(() => LightboxNextButton), nameof(LightboxNextButton));
            ActivateElement(nextButton);
            Wait.UntilInputIsProcessed();
        }

        public void ClickLightboxPrevious()
        {
            var prevButton = RequireElement(WaitForElement(() => LightboxPreviousButton), nameof(LightboxPreviousButton));
            ActivateElement(prevButton);
            Wait.UntilInputIsProcessed();
        }

        public void SwitchToGridView()
        {
            var gridButton = RequireElement(WaitForElement(() => GridViewButton), nameof(GridViewButton));
            ActivateElement(gridButton);
            Wait.UntilInputIsProcessed();
        }

        public void SwitchToFocusView()
        {
            var focusButton = RequireElement(WaitForElement(() => FocusViewButton), nameof(FocusViewButton));
            ActivateElement(focusButton);
            Wait.UntilInputIsProcessed();
        }

        public void ToggleFullscreen()
        {
            var fullWindow = RequireElement(WaitForElement(() => FullWindowButton), nameof(FullWindowButton));
            ActivateElement(fullWindow);
            Wait.UntilInputIsProcessed();
        }

        public void CloseGalleryLightbox()
        {
            var closeButton = RequireElement(WaitForElement(() => LightboxCloseButton), nameof(LightboxCloseButton));
            ActivateElement(closeButton);
            Wait.UntilInputIsProcessed();
        }

        public void ClickPlayOverlay()
        {
            var overlay = RequireElement(WaitForElement(() => PlayOverlayButton), nameof(PlayOverlayButton));
            ActivateElement(overlay);
            Wait.UntilInputIsProcessed();
        }

        public void ClickBack()
        {
            var back = RequireElement(WaitForElement(() => BackButton), nameof(BackButton));
            ActivateElement(back);
            Wait.UntilInputIsProcessed();
        }

        private static void ActivateElement(AutomationElement element)
        {
            if (element.Patterns.Invoke.IsSupported)
            {
                element.Patterns.Invoke.Pattern.Invoke();
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
