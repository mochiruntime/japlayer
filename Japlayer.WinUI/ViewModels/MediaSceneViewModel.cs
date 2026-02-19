using System.Linq;
using Japlayer.Data.Models;

namespace Japlayer.ViewModels
{
    public class MediaSceneViewModel(MediaScene scene)
    {
        private readonly MediaScene _scene = scene;

        public string MediaId => _scene.MediaId;
        public int? SceneNumber => _scene.SceneNumber;

        // TODO: support multiple file locations
        public string File => _scene.FilePaths.FirstOrDefault();
    }
}
