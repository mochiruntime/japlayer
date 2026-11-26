namespace Japlayer.Data.Helpers
{
    public static class ThumbnailFallbackSelector
    {
        public static int SelectFallbackIndex(int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            return Math.Clamp((int)Math.Round((count - 1) * 0.75), 0, count - 1);
        }

        public static T? SelectFallbackItem<T>(IReadOnlyList<T> items)
        {
            var index = SelectFallbackIndex(items.Count);
            return index >= 0 ? items[index] : default;
        }
    }
}
