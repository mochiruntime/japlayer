#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Japlayer.Helpers
{
    public enum DecodedImageAxis
    {
        Original,
        Width,
        Height
    }

    public readonly record struct DecodedImageCacheKey(string NormalizedPath, DecodedImageAxis Axis, int DecodePixelSize)
    {
        public bool Equals(DecodedImageCacheKey other)
            => Axis == other.Axis
               && DecodePixelSize == other.DecodePixelSize
               && StringComparer.OrdinalIgnoreCase.Equals(NormalizedPath, other.NormalizedPath);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(NormalizedPath, StringComparer.OrdinalIgnoreCase);
            hash.Add(Axis);
            hash.Add(DecodePixelSize);
            return hash.ToHashCode();
        }
    }

    public static class DecodedImageCache
    {
        private const int ThumbnailCacheCapacity = 128;
        private const int LargeImageCacheCapacity = 16;
        private const int ThumbnailMaxDecodePixelSize = 240;

        private static readonly BoundedLruCache<DecodedImageCacheKey, BitmapImage> ThumbnailCache = new(ThumbnailCacheCapacity);
        private static readonly BoundedLruCache<DecodedImageCacheKey, BitmapImage> LargeImageCache = new(LargeImageCacheCapacity);
        private static readonly object CacheGate = new();

        public static bool IsThumbnailBucket(int decodePixelSize)
            => decodePixelSize is > 0 and <= ThumbnailMaxDecodePixelSize;

        public static bool TryCreateAbsoluteUri(string? path, [NotNullWhen(true)] out Uri? uri, out string normalizedPath)
        {
            uri = null;
            normalizedPath = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                if (Path.IsPathRooted(path))
                {
                    var fullPath = Path.GetFullPath(path);
                    if (Uri.TryCreate(fullPath, UriKind.Absolute, out uri))
                    {
                        normalizedPath = fullPath;
                        return true;
                    }

                    return false;
                }

                if (Uri.TryCreate(path, UriKind.Absolute, out uri))
                {
                    normalizedPath = uri.AbsoluteUri;
                    return true;
                }
            }
            catch
            {
                uri = null;
                normalizedPath = string.Empty;
            }

            return false;
        }

        public static bool TryParseDecodeSpecification(object? parameter, out DecodedImageAxis axis, out int decodePixelSize)
        {
            if (parameter is null)
            {
                axis = DecodedImageAxis.Original;
                decodePixelSize = 0;
                return true;
            }

            if (parameter is int intValue)
            {
                if (intValue > 0)
                {
                    axis = DecodedImageAxis.Height;
                    decodePixelSize = intValue;
                    return true;
                }

                axis = default;
                decodePixelSize = 0;
                return false;
            }

            if (parameter is double doubleValue)
            {
                if (double.IsFinite(doubleValue) && doubleValue > 0 && doubleValue <= int.MaxValue)
                {
                    var rounded = (int)Math.Round(doubleValue, MidpointRounding.AwayFromZero);
                    if (rounded > 0)
                    {
                        axis = DecodedImageAxis.Height;
                        decodePixelSize = rounded;
                        return true;
                    }
                }

                axis = default;
                decodePixelSize = 0;
                return false;
            }

            if (parameter is string stringValue)
            {
                var trimmed = stringValue.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    axis = default;
                    decodePixelSize = 0;
                    return false;
                }

                if (TryParsePrefixedSize(trimmed, "width:", out var widthSize)
                    || TryParsePrefixedSize(trimmed, "w:", out widthSize))
                {
                    axis = DecodedImageAxis.Width;
                    decodePixelSize = widthSize;
                    return true;
                }

                if (TryParsePrefixedSize(trimmed, "height:", out var heightSize)
                    || TryParsePrefixedSize(trimmed, "h:", out heightSize))
                {
                    axis = DecodedImageAxis.Height;
                    decodePixelSize = heightSize;
                    return true;
                }

                if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bareSize)
                    && bareSize > 0)
                {
                    axis = DecodedImageAxis.Height;
                    decodePixelSize = bareSize;
                    return true;
                }
            }

            axis = default;
            decodePixelSize = 0;
            return false;
        }

        public static BitmapImage? GetOrCreate(string? path, object? decodeParameter = null)
        {
            if (!TryParseDecodeSpecification(decodeParameter, out var axis, out var decodePixelSize))
            {
                return null;
            }

            return GetOrCreate(path, axis, decodePixelSize);
        }

        public static BitmapImage? GetOrCreate(string? path, DecodedImageAxis axis, int decodePixelSize)
        {
            if (axis != DecodedImageAxis.Original && decodePixelSize <= 0)
            {
                return null;
            }

            if (!TryCreateAbsoluteUri(path, out var uri, out var normalizedPath))
            {
                return null;
            }

            var normalizedSize = axis == DecodedImageAxis.Original ? 0 : decodePixelSize;
            var key = new DecodedImageCacheKey(normalizedPath, axis, normalizedSize);
            var cache = IsThumbnailBucket(key.DecodePixelSize) ? ThumbnailCache : LargeImageCache;

            lock (CacheGate)
            {
                if (cache.TryGetValue(key, out var cachedBitmap))
                {
                    return cachedBitmap;
                }

                var bitmapImage = new BitmapImage
                {
                    DecodePixelType = DecodePixelType.Logical
                };

                if (key.Axis == DecodedImageAxis.Width)
                {
                    bitmapImage.DecodePixelWidth = key.DecodePixelSize;
                }
                else if (key.Axis == DecodedImageAxis.Height)
                {
                    bitmapImage.DecodePixelHeight = key.DecodePixelSize;
                }

                bitmapImage.UriSource = uri;
                cache.Set(key, bitmapImage);
                return bitmapImage;
            }
        }

        public static void Clear()
        {
            lock (CacheGate)
            {
                ThumbnailCache.Clear();
                LargeImageCache.Clear();
            }
        }

        private static bool TryParsePrefixedSize(string value, string prefix, out int parsedSize)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var suffix = value[prefix.Length..].Trim();
                if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedSize)
                    && parsedSize > 0)
                {
                    return true;
                }
            }

            parsedSize = 0;
            return false;
        }
    }
}
