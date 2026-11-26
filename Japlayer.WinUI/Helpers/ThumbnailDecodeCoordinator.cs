#nullable enable
using System;
using System.Collections.Generic;

namespace Japlayer.Helpers
{
    /// <summary>
    /// Coordinates thumbnail image decodes with time-based coalescing, keyframe sampling,
    /// card-width-derived decode sizing, and scrub percentage guards.
    /// </summary>
    public sealed class ThumbnailDecodeCoordinator
    {
        public const long DefaultMinDecodeIntervalMs = 25;
        public const int DefaultMaxPrewarmKeyframes = 12;
        public const int DefaultDecodePixelWidth = 360;
        public const int MaxDecodePixelWidth = 720;

        private long _lastDispatchTimestampMs = -1;
        private string? _pendingPath;

        public string? PendingPath => _pendingPath;

        public static bool IsThumbnailPath(string path)
        {
            return path.Contains("thumbs/", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("thumbs\\", StringComparison.OrdinalIgnoreCase);
        }

        public static int CalculateDecodePixelWidth(double actualWidth)
        {
            if (double.IsNaN(actualWidth) || double.IsInfinity(actualWidth) || actualWidth <= 0)
            {
                return DefaultDecodePixelWidth;
            }

            return Math.Clamp((int)Math.Ceiling(actualWidth * 1.2), DefaultDecodePixelWidth, MaxDecodePixelWidth);
        }

        public static IReadOnlyList<int> SelectPrewarmIndices(int totalCount, int maxKeyframes = DefaultMaxPrewarmKeyframes)
        {
            if (totalCount <= 0 || maxKeyframes <= 0)
            {
                return [];
            }

            if (totalCount <= maxKeyframes)
            {
                var allIndices = new int[totalCount];
                for (var index = 0; index < totalCount; index++)
                {
                    allIndices[index] = index;
                }

                return allIndices;
            }

            if (maxKeyframes == 1)
            {
                return [0];
            }

            var selectedIndices = new int[maxKeyframes];
            for (var step = 0; step < maxKeyframes; step++)
            {
                selectedIndices[step] = (int)Math.Round(step * (totalCount - 1) / (double)(maxKeyframes - 1));
            }

            return selectedIndices;
        }

        public static bool TryCalculateScrubPercent(double pointerX, double actualWidth, out double percent)
        {
            if (double.IsNaN(actualWidth) || double.IsInfinity(actualWidth) || actualWidth <= 0 ||
                double.IsNaN(pointerX) || double.IsInfinity(pointerX))
            {
                percent = 0;
                return false;
            }

            percent = Math.Clamp(pointerX / actualWidth, 0.0, 1.0);
            return true;
        }

        /// <summary>
        /// Requests a thumbnail decode for <paramref name="path"/> at <paramref name="nowTimestampMs"/>.
        /// Returns <c>true</c> if enough time has elapsed since the previous dispatch and the caller
        /// should start decoding immediately; <c>false</c> if the request was coalesced into <see cref="PendingPath"/>.
        /// </summary>
        public bool RequestOrCoalesce(string path, long nowTimestampMs, long minIntervalMs = DefaultMinDecodeIntervalMs)
        {
            if (_lastDispatchTimestampMs < 0 || (nowTimestampMs - _lastDispatchTimestampMs) >= minIntervalMs)
            {
                _lastDispatchTimestampMs = nowTimestampMs;
                _pendingPath = null;
                return true;
            }

            _pendingPath = path;
            return false;
        }

        /// <summary>
        /// Consumes any coalesced pending thumbnail path if it still matches <paramref name="currentSourcePath"/>.
        /// </summary>
        public string? ConsumePending(long nowTimestampMs, string? currentSourcePath)
        {
            var nextPath = _pendingPath;
            _pendingPath = null;

            if (!string.IsNullOrEmpty(nextPath) && currentSourcePath == nextPath)
            {
                _lastDispatchTimestampMs = nowTimestampMs;
                return nextPath;
            }

            return null;
        }

        /// <summary>
        /// Clears any pending coalesced thumbnail request and resets the dispatch timestamp.
        /// </summary>
        public void Reset()
        {
            _pendingPath = null;
            _lastDispatchTimestampMs = -1;
        }
    }
}
