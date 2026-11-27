#nullable enable
using System;
using Japlayer.Helpers;
using Microsoft.UI.Xaml.Data;

namespace Japlayer.Converters
{
    public sealed class PathToImageSourceConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object parameter, string language)
            => DecodedImageCache.GetOrCreate(value as string, parameter);

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}
