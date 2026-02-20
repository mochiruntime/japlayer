#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Japlayer.Helpers;

/// <summary>
/// Represents a Win32 application capable of handling a specific file association.
/// </summary>
public class Win32FileHandler
{
    public string Name { get; set; }
    public string Command { get; set; }
    public string? ExePath { get; set; }

    public Win32FileHandler(string name, string command, string? exePath = null)
    {
        Name = name;
        Command = command;
        ExePath = exePath;
    }

    /// <summary>
    /// Invokes the handler for the specified file path.
    /// </summary>
    public virtual void Invoke(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(Command)) return;

            var (exe, args) = ParseCommand(Command, filePath);

            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error invoking Win32 handler: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a shell command string and expands the file path placeholder.
    /// </summary>
    public static (string Exe, string Args) ParseCommand(string command, string filePath)
    {
        string exe;
        string args;

        string trimmedCommand = command.Trim();

        // Handle quoted executable paths
        if (trimmedCommand.StartsWith('\"'))
        {
            int endQuote = trimmedCommand.IndexOf('\"', 1);
            if (endQuote > 0)
            {
                exe = trimmedCommand[1..endQuote];
                args = trimmedCommand[(endQuote + 1)..].Trim();
            }
            else
            {
                exe = trimmedCommand[1..];
                args = string.Empty;
            }
        }
        else
        {
            int firstSpace = trimmedCommand.IndexOf(' ');
            if (firstSpace > 0)
            {
                exe = trimmedCommand[..firstSpace];
                args = trimmedCommand[(firstSpace + 1)..].Trim();
            }
            else
            {
                exe = trimmedCommand;
                args = string.Empty;
            }
        }

        // Expand %1 or append file path
        if (args.Contains("%1", StringComparison.OrdinalIgnoreCase))
        {
            args = args.Replace("%1", $"\"{filePath}\"", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            args = string.IsNullOrEmpty(args) ? $"\"{filePath}\"" : $"{args} \"{filePath}\"";
        }

        return (exe, args);
    }
}

/// <summary>
/// Resolves installed application handlers for file extensions by querying the Windows Registry.
/// </summary>
public static class FileAssociationResolver
{
    /// <summary>
    /// Gets a list of Win32 application handlers associated with the given file extension.
    /// </summary>
    /// <param name="extension">The file extension (e.g., ".mp4").</param>
    public static List<Win32FileHandler> GetHandlers(string extension)
    {
        var handlers = new List<Win32FileHandler>();
        var addedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // 1. Check HKCU OpenWithList (Explorer's "Open with" history)
            QueryOpenWithList(Registry.CurrentUser, extension, handlers, addedCommands);

            // 2. Check HKCU OpenWithProgids (User-specific associations)
            QueryOpenWithProgids(Registry.CurrentUser, extension, handlers, addedCommands);

            // 3. Check HKCR OpenWithProgids (System-wide associations)
            QueryOpenWithProgids(Registry.ClassesRoot, extension, handlers, addedCommands);

            // 4. Check Default ProgID
            using (var extKey = Registry.ClassesRoot.OpenSubKey(extension))
            {
                if (extKey?.GetValue("") is string defaultProgId)
                {
                    AddHandlerFromProgId(defaultProgId, handlers, addedCommands);
                }
            }

            // 5. Check SystemFileAssociations (Modern Windows association mechanism)
            string systemKeyPath = $@"SystemFileAssociations\{extension}\shell\open\command";
            using (var sysKey = Registry.ClassesRoot.OpenSubKey(systemKeyPath))
            {
                if (sysKey?.GetValue("") is string cmd)
                {
                    string name = extension[1..].ToUpperInvariant() + " Viewer";
                    AddHandlerWithValidation(name, cmd, handlers, addedCommands);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Registry Enumeration Error : {ex.Message}");
        }

        // Final cleanup of names (e.g. removing "URL:" prefix from some registered handlers)
        foreach (var h in handlers)
        {
            if (h.Name.StartsWith("URL:", StringComparison.OrdinalIgnoreCase))
            {
                h.Name = h.Name[4..].Trim();
            }
        }

        return handlers;
    }

    private static void AddHandlerWithValidation(string name, string cmd, List<Win32FileHandler> handlers, HashSet<string> addedCommands)
    {
        if (string.IsNullOrEmpty(cmd) || !addedCommands.Add(cmd)) return;

        string exe = ExtractExePath(cmd);

        if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
        {
            // If the name is generic or looks like a ProgID, try to get the file description from the EXE
            if (IsNameGeneric(name, exe))
            {
                try
                {
                    var info = FileVersionInfo.GetVersionInfo(exe);
                    if (!string.IsNullOrEmpty(info.FileDescription))
                    {
                        name = info.FileDescription;
                    }
                }
                catch { }
            }

            handlers.Add(new Win32FileHandler(name, cmd, exe));
        }
    }

    private static string ExtractExePath(string cmd)
    {
        string trimmed = cmd.TrimStart();
        if (trimmed.StartsWith('\"'))
        {
            int endQuote = trimmed.IndexOf('\"', 1);
            return endQuote > 0 ? trimmed[1..endQuote] : trimmed[1..];
        }
        int firstSpace = trimmed.IndexOf(' ');
        return firstSpace > 0 ? trimmed[..firstSpace] : trimmed;
    }

    private static bool IsNameGeneric(string name, string exe)
    {
        return name.Contains("Video", StringComparison.OrdinalIgnoreCase) ||
               name.Contains(Path.GetExtension(exe), StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("AppX", StringComparison.OrdinalIgnoreCase) ||
               name.Length < 3;
    }

    private static void QueryOpenWithList(RegistryKey root, string extension, List<Win32FileHandler> handlers, HashSet<string> addedCommands)
    {
        string path = $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\OpenWithList";
        using var key = root.OpenSubKey(path);
        if (key == null) return;

        foreach (var valName in key.GetValueNames())
        {
            if (valName == "MRUList") continue;
            if (key.GetValue(valName) is string appName)
            {
                using var appKey = Registry.ClassesRoot.OpenSubKey($@"Applications\{appName}\shell\open\command");
                if (appKey?.GetValue("") is string cmd)
                {
                    string friendlyName = GetFriendlyNameForApp(appName);
                    AddHandlerWithValidation(friendlyName, cmd, handlers, addedCommands);
                }
            }
        }
    }

    private static void QueryOpenWithProgids(RegistryKey root, string extension, List<Win32FileHandler> handlers, HashSet<string> addedCommands)
    {
        string path = root == Registry.ClassesRoot
            ? $@"{extension}\OpenWithProgids"
            : $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\OpenWithProgids";

        using var key = root.OpenSubKey(path);
        if (key == null) return;

        foreach (var progId in key.GetValueNames())
        {
            AddHandlerFromProgId(progId, handlers, addedCommands);
        }
    }

    private static void AddHandlerFromProgId(string progId, List<Win32FileHandler> handlers, HashSet<string> addedCommands)
    {
        using var progKey = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
        if (progKey?.GetValue("") is string cmd)
        {
            string? name = null;
            using (var nameKey = Registry.ClassesRoot.OpenSubKey(progId))
            {
                name = nameKey?.GetValue("") as string;
            }

            AddHandlerWithValidation(name ?? progId, cmd, handlers, addedCommands);
        }
    }

    private static string GetFriendlyNameForApp(string appName)
    {
        using var key = Registry.ClassesRoot.OpenSubKey($@"Applications\{appName}");
        if (key?.GetValue("FriendlyAppName") is string name) return name;
        return Path.GetFileNameWithoutExtension(appName);
    }
}
