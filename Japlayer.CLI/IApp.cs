namespace Japlayer.CLI;

public interface IApp
{
    public string Name { get; }
    public string Description { get; }
    public Task RunAsync(string[] args);
}
