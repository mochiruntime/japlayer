using Japlayer.Contracts;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Japlayer.ViewModels
{
    public class MainViewModel
    {
        private readonly IMediaProvider _mediaProvider;
        private readonly IImageProvider _imageProvider;

        public ObservableCollection<MediaItemViewModel> MediaItems { get; } = new();

        public MainViewModel(IMediaProvider mediaProvider, IImageProvider imageProvider)
        {
            _mediaProvider = mediaProvider;
            _imageProvider = imageProvider;
        }

        public async Task LoadDataAsync()
        {
            var items = await _mediaProvider.GetAllItemsAsync();
            MediaItems.Clear();
            foreach (var item in items)
            {
                MediaItems.Add(new MediaItemViewModel(item, _imageProvider));
            }
        }
    }
}
