namespace Japlayer.Contracts
{
    public interface ISettingsService
    {
        string ImagePath { get; }
        string SqliteDatabasePath { get; }
    }
}
