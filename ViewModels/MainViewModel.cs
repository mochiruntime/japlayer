using Japlayer.Contracts;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Japlayer.ViewModels
{
    public class MainViewModel
    {
        private readonly IMediaProvider _mediaProvider;
        private readonly IImageProvider _imageProvider;
        private readonly IMediaSceneProvider _sceneProvider;
        private List<MediaItemViewModel> _allMediaItems = new();

        public ObservableCollection<MediaItemViewModel> MediaItems { get; } = new();
        public ObservableCollection<GenreViewModel> Genres { get; } = new();
        public bool IsDataLoaded { get; private set; }

        public MainViewModel(IMediaProvider mediaProvider, IImageProvider imageProvider, IMediaSceneProvider sceneProvider)
        {
            _mediaProvider = mediaProvider;
            _imageProvider = imageProvider;
            _sceneProvider = sceneProvider;
        }

        public async Task LoadDataAsync()
        {
            var items = await _mediaProvider.GetAllItemsAsync();
            _allMediaItems = items.Select(item => new MediaItemViewModel(item, _imageProvider, _sceneProvider)).ToList();
            
            // Extract all unique genres
            var allGenres = _allMediaItems
                .SelectMany(m => m.Genres)
                .Distinct()
                .OrderBy(g => g)
                .ToList();

            Genres.Clear();
            foreach (var genreName in allGenres)
            {
                var genreVm = new GenreViewModel(genreName);
                genreVm.OnSelectionChanged += OnGenreSelectionChanged;
                Genres.Add(genreVm);
            }

            ApplyFilter();
            IsDataLoaded = true;
        }

        private void OnGenreSelectionChanged(GenreViewModel sender)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var selectedGenres = new HashSet<string>(Genres.Where(g => g.IsSelected).Select(g => g.Name));

            var filteredItems = _allMediaItems.Where(item => 
            {
                // Strict subset: Item is displayed IF AND ONLY IF its set of genres is entirely selected.
                // i.e. All genres of the item must be in the selectedGenres set.
                return item.Genres.All(g => selectedGenres.Contains(g));
            });

            MediaItems.Clear();
            foreach (var item in filteredItems)
            {
                MediaItems.Add(item);
            }
        }
    }
}
