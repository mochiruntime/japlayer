namespace Japlayer.Contracts
{
    public interface ISettingsService
    {
        string MediaPath { get; }
        string ImagePath { get; }
        string LocalMediaStorage { get; }
    }
}
