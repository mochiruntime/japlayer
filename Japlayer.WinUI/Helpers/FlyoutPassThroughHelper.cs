#nullable enable
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace Japlayer.Helpers
{
    /// <summary>
    /// Configures a <see cref="DropDownButton"/> and its <see cref="FlyoutBase"/> so pointer clicks
    /// inside a shared toolbar/header pass through light-dismiss to other controls, while clicking
    /// the already-open button toggles its flyout closed without immediately re-opening.
    /// </summary>
    public static class FlyoutPassThroughHelper
    {
        public static void Attach(
            DropDownButton button,
            Func<DependencyObject?> passThroughElementProvider,
            Action? onOpening = null,
            Action? onClosed = null)
        {
            if (button.Flyout is not FlyoutBase flyout)
            {
                return;
            }

            FlyoutBase? suppressedFlyout = null;
            long lastClosedTicks = 0;
            var isClosingFromSameButton = false;

            flyout.Opening += (_, _) =>
            {
                flyout.OverlayInputPassThroughElement = passThroughElementProvider();
                onOpening?.Invoke();
            };

            flyout.Closed += (_, _) =>
            {
                lastClosedTicks = Environment.TickCount64;
                suppressedFlyout = flyout;
                onClosed?.Invoke();
            };

            button.AddHandler(
                UIElement.PointerPressedEvent,
                new PointerEventHandler((_, _) =>
                {
                    if (suppressedFlyout == flyout &&
                        Environment.TickCount64 - lastClosedTicks < 80 &&
                        button.Flyout == flyout)
                    {
                        isClosingFromSameButton = true;
                        button.Flyout = null;
                    }
                }),
                handledEventsToo: true);

            void RestoreFlyout()
            {
                if (!isClosingFromSameButton)
                {
                    return;
                }

                isClosingFromSameButton = false;
                button.Flyout = flyout;
                suppressedFlyout = null;
            }

            button.Click += (_, _) => RestoreFlyout();
            button.PointerCaptureLost += (_, _) => RestoreFlyout();
            button.PointerCanceled += (_, _) => RestoreFlyout();
        }
    }
}
