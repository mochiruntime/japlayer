using Microsoft.UI.Xaml.Data;
using System;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Japlayer.Converters
{
    public class PathToMediaSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string path && !string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    if (System.IO.Path.IsPathRooted(path))
                    {
                        return MediaSource.CreateFromUri(new Uri(path));
                    }
                    
                    if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
                    {
                        return MediaSource.CreateFromUri(uri);
                    }
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
