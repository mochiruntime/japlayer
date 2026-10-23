using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;

namespace Japlayer.UITests.PageObjects
{
    public class MediaItemPageObject(Window window)
    {
        private readonly Window _window = window;

        public Button? BackButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("BackButton"))?.AsButton();

        public AutomationElement? TitleText =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("MediaItemTitle"));

        public AutomationElement? ReleaseDateText =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("MediaItemReleaseDate"));

        public AutomationElement? RuntimeText =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("MediaItemRuntime"));

        public AutomationElement? ContentIdText =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("MediaItemContentId"));

        public Button? GalleryScrollLeftButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("GalleryScrollLeftButton"))?.AsButton();

        public Button? GalleryScrollRightButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("GalleryScrollRightButton"))?.AsButton();

        public Button? SceneOpenInButton =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("SceneOpenInButton"))?.AsButton();

        public Button? PlayOverlayButton =>
            Retry.WhileNull(
                () => _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("PlayOverlay"))?.AsButton(),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(200)).Result;

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

        public AutomationElement? HighlightsCanvas =>
            _window.FindFirstDescendant(conditionFactory => conditionFactory.ByAutomationId("HighlightsCanvas"));

        public void ClickPlayOverlay()
        {
            var overlay = PlayOverlayButton;
            if (overlay != null)
            {
                if (overlay.Patterns.Invoke.IsSupported)
                {
                    overlay.Patterns.Invoke.Pattern.Invoke();
                }
                else
                {
                    overlay.Click();
                }
                Wait.UntilInputIsProcessed();
                Thread.Sleep(800);
            }
        }

        public void ClickBack()
        {
            var back = BackButton;
            if (back != null)
            {
                if (back.Patterns.Invoke.IsSupported)
                {
                    back.Patterns.Invoke.Pattern.Invoke();
                }
                else
                {
                    back.Click();
                }
                Wait.UntilInputIsProcessed();
                Thread.Sleep(500);
            }
        }
    }
}
