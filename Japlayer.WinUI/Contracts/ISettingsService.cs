using System.Threading.Tasks;

namespace Japlayer.Contracts
{
    public interface ISettingsService
    {
        public string ImagePath { get; set; }
        public string SqliteDatabasePath { get; set; }

        public Task LoadAsync();
        public Task SaveAsync();
    }
}

