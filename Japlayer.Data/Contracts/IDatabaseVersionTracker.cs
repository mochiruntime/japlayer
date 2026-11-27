namespace Japlayer.Data.Contracts;

/// <summary>
/// Tracks logical database version changes on a SQLite database file to distinguish
/// external commits from internal writes or passive WAL checkpoints.
/// </summary>
public interface IDatabaseVersionTracker : IDisposable
{
    public void Open(string databasePath);

    public void RefreshKnownVersion();

    public bool HasExternalChange();

    public void Reset();
}
