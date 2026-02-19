using System.Threading.Tasks;

namespace Japlayer.Contracts
{
    public interface ISettingsService
    {
        string ImagePath { get; set; }
        string SqliteDatabasePath { get; set; }

        Task LoadAsync();
        Task SaveAsync();
    }
}

