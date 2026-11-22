using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Metaphor.Installer;

/// <summary>
/// How the exe starts the unpacked installer: exactly as install.cmd does (scripts/player/install.cmd), Windows
/// PowerShell 5.1 by its full path in System32, so no powershell file elsewhere runs instead, with
/// <c>-NoLogo -NoProfile -ExecutionPolicy Bypass -File install.ps1 -FromLauncher</c> and the exe's own arguments after
/// them. The policy bypass holds for that one process only. The host clears PSModulePath for it, as install.cmd does.
/// </summary>
internal sealed class InstallerCommand
{
    private InstallerCommand(string fileName, string arguments, string workingDirectory)
    {
        FileName = fileName;
        Arguments = arguments;
        WorkingDirectory = workingDirectory;
    }

    public string FileName { get; }

    /// <summary>The command line after the program's name, each argument quoted as Windows reads it back.</summary>
    public string Arguments { get; }

    /// <summary>The unpacked mod's folder, the one install.ps1 is in.</summary>
    public string WorkingDirectory { get; }

    /// <summary>The command for the installer in FOLDER/MODFOLDER, with SYSTEM32 the system folder.</summary>
    public static InstallerCommand For(string system32, string folder, string modFolder, IEnumerable<string> args)
    {
        var mod = Path.Combine(folder, modFolder);
        var script = Path.Combine(mod, "install.ps1");
        var line = new[] { "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-FromLauncher" }.Concat(args);
        return new InstallerCommand(
            Path.Combine(system32, "WindowsPowerShell", "v1.0", "powershell.exe"),
            string.Join(" ", line.Select(CommandLine.Quote)),
            mod);
    }
}

/// <summary>Windows' command-line quoting, the rules CommandLineToArgvW and the C runtime read arguments back by.</summary>
internal static class CommandLine
{
    /// <summary>ARGUMENT as it is when it has no space, tab or quote; else in quotes, with each quote escaped and the
    /// backslashes before a quote or the closing quote doubled.</summary>
    public static string Quote(string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny([' ', '\t', '\n', '\v', '"']) < 0)
        {
            return argument;
        }
        var quoted = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var c in argument)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }
            quoted.Append('\\', c == '"' ? (backslashes * 2) + 1 : backslashes);
            quoted.Append(c);
            backslashes = 0;
        }
        quoted.Append('\\', backslashes * 2);
        return quoted.Append('"').ToString();
    }
}
