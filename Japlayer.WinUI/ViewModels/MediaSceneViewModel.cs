using Japlayer.Data.Models;
using System.Linq;

namespace Japlayer.ViewModels
{
    public class MediaSceneViewModel
    {
        private readonly MediaScene _scene;

        public MediaSceneViewModel(MediaScene scene)
        {
            _scene = scene;
        }

        public string MediaId => _scene.MediaId;
        public int? SceneNumber => _scene.SceneNumber;

        // TODO: support multiple file locations
        public string File => _scene.FilePaths.FirstOrDefault();
    }
}
