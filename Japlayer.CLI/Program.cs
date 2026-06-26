namespace Japlayer.CLI;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // 1. Discover all runnable apps
        var appTypes = typeof(IApp).Assembly.GetTypes()
            .Where(type => typeof(IApp).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
            .ToList();

        var apps = appTypes
            .Select(type => (IApp)Activator.CreateInstance(type)!)
            .OrderBy(app => app.Name)
            .ToList();

        // 2. Parse command arguments
        if (args.Length == 0 || IsHelpArgument(args[0]))
        {
            PrintUsage(apps);
            return;
        }

        var appName = args[0];
        var targetApp = apps.FirstOrDefault(app => string.Equals(app.Name, appName, StringComparison.OrdinalIgnoreCase));

        if (targetApp == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Unknown command/app: '{appName}'");
            Console.ResetColor();
            Console.WriteLine();
            PrintUsage(apps);
            Environment.ExitCode = 1;
            return;
        }

        // 3. Execute the selected app
        var appArgs = args.Skip(1).ToArray();
        try
        {
            await targetApp.RunAsync(appArgs);
        }
        catch (Exception exception)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FATAL ERROR] An unhandled exception occurred in '{targetApp.Name}':");
            Console.WriteLine(exception.ToString());
            Console.ResetColor();
            Environment.ExitCode = 1;
        }
    }

    private static bool IsHelpArgument(string arg)
    {
        return string.Equals(arg, "help", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(arg, "/?", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintUsage(List<IApp> apps)
    {
        Console.WriteLine("Japlayer CLI - Collection of Invokable Apps");
        Console.WriteLine("===========================================");
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project Japlayer.CLI -- <app-name> [arguments]");
        Console.WriteLine();
        Console.WriteLine("Available Apps:");

        var maxNameLen = apps.Max(app => app.Name.Length);
        foreach (var app in apps)
        {
            Console.WriteLine($"  {app.Name.PadRight(maxNameLen + 2)} - {app.Description}");
        }
        Console.WriteLine();
    }
}
