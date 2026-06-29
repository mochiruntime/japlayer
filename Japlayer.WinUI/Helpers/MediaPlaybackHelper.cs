#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Media.Playback;
using Windows.System;

namespace Japlayer.Helpers
{
    public static class MediaPlaybackHelper
    {
        public static void HandleArrowSeek(MediaPlayer player, VirtualKey key, bool isCtrlPressed, double normalSeek, double modifierSeek)
        {
            if (player == null || player.PlaybackSession == null)
            {
                return;
            }

            var seekAmount = isCtrlPressed ? modifierSeek : normalSeek;
            var position = player.Position;
            var duration = player.PlaybackSession.NaturalDuration;

            if (key == VirtualKey.Left)
            {
                player.Position = TimeSpan.FromSeconds(Math.Max(0, position.TotalSeconds - seekAmount));
            }
            else if (key == VirtualKey.Right)
            {
                player.Position = TimeSpan.FromSeconds(Math.Min(duration.TotalSeconds, position.TotalSeconds + seekAmount));
            }
        }

        public static double? GetNextHighlightTime(double currentSeconds, IEnumerable<int> highlights)
        {
            if (highlights == null || !highlights.Any())
            {
                return null;
            }

            var sortedHighlights = highlights.OrderBy(timestamp => timestamp).ToList();

            // Find the first highlight strictly greater than the current playback position
            var nextHighlight = sortedHighlights
                .Cast<int?>()
                .FirstOrDefault(timestamp => timestamp > currentSeconds + 0.5);

            // Loop back to the first highlight
            nextHighlight ??= sortedHighlights.First();

            return nextHighlight.Value;
        }
    }
}
