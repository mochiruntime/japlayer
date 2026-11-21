#nullable enable
using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Japlayer.Converters
{
    public sealed class PathToImageSourceConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object parameter, string language)
        {
            if (value is string path && !string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    if (System.IO.Path.IsPathRooted(path) || Uri.TryCreate(path, UriKind.Absolute, out _))
                    {
                        return new BitmapImage(new Uri(path));
                    }
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}
