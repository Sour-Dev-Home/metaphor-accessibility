using System.Diagnostics.CodeAnalysis;
using Metaphor.Core.Settings;

namespace Metaphor.Hooks;

/// <summary>Reads the settings file from the mod's user-config folder at start and again whenever it changes (#104),
/// handing its text to a <see cref="SettingsReloader"/>. The parsing and the announcing are in Core; this is only the
/// file system. Nothing here may throw into the game.</summary>
internal sealed class SettingsFile : IDisposable
{
    private readonly string path;
    private readonly SettingsReloader reloader;
    private readonly Action<string> log;
    private readonly FileSystemWatcher? watcher;

    private SettingsFile(string folder, SettingsReloader reloader, Action<string> log)
    {
        path = Path.Combine(folder, ModSettings.FileName);
        this.reloader = reloader;
        this.log = log;
        reloader.Load(Read());
        watcher = Watch(folder);
    }

    /// <summary>Reads the file in <paramref name="folder"/> now and watches it. A folder that can't be watched leaves
    /// the file read once, at start.</summary>
    public static SettingsFile Open(string folder, SettingsReloader reloader, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(reloader);
        ArgumentNullException.ThrowIfNull(log);
        return new SettingsFile(folder, reloader, log);
    }

    public void Dispose() => watcher?.Dispose();

    [SuppressMessage("Design", "CA1031", Justification = "A settings problem must never stop the mod.")]
    private FileSystemWatcher? Watch(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var created = new FileSystemWatcher(folder, ModSettings.FileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };
            // An editor may save by writing a temporary file and renaming it, so a rename counts as a change.
            created.Changed += (_, _) => Reload();
            created.Created += (_, _) => Reload();
            created.Deleted += (_, _) => Reload();
            created.Renamed += (_, _) => Reload();
            created.EnableRaisingEvents = true;
            return created;
        }
        catch (Exception e)
        {
            log($"settings file not watched: {e.Message}");
            return null;
        }
    }

    [SuppressMessage("Design", "CA1031", Justification = "Runs on a watcher thread, where an exception would end the process.")]
    private void Reload()
    {
        try
        {
            reloader.Reload(Read());
        }
        catch (Exception e)
        {
            log($"settings file not applied: {e.Message}");
        }
    }

    // The editor may still hold the file as the first event arrives; a few short retries ride that out, and the
    // write's later events would apply it anyway.
    [SuppressMessage("Design", "CA1031", Justification = "An unreadable file is the same as a missing one.")]
    private string? Read()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
            catch (Exception e)
            {
                log($"settings file unreadable: {e.Message}");
                return null;
            }
        }
        log("settings file stayed locked; using defaults");
        return null;
    }
}
