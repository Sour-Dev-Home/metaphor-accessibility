namespace Metaphor.Core.Settings;

/// <summary>The settings file itself, for code that changes it (#754): the hook layer's file in the mod's user-config
/// folder, or a test's text.</summary>
public interface ISettingsStore
{
    /// <summary>The file's text, or null when there is none. Throws an <see cref="IOException"/> or an
    /// <see cref="UnauthorizedAccessException"/> when the file is there but can't be read, so nothing is written over
    /// it.</summary>
    string? Read();

    /// <summary>Replaces the file's text in one step, so its watcher never reads half of it. Throws when it can't.
    /// </summary>
    void Write(string text);
}

/// <summary>A store with no file behind it: every write fails, so a menu over it says "Not saved".</summary>
public sealed class NoSettingsStore : ISettingsStore
{
    public string? Read() => null;

    public void Write(string text) => throw new IOException("there is no settings file to write");
}

/// <summary>Changes one setting in the file and applies it at once (#754). The new settings are put in force before
/// the file is written, so the watcher's reload of the mod's own write finds nothing changed and says nothing: the
/// menu that asked says the new value itself. A write that fails puts the old file's settings back. Only the value
/// asked for is written: nothing else, the player's name or folders least of all, goes in the file.</summary>
public sealed class SettingsEditor(ISettingsStore store, SettingsReloader settings)
{
    private readonly Lock gate = new();

    /// <summary>Sets <paramref name="path"/> to <paramref name="value"/>, JSON text, keeping the rest of the file;
    /// false when the file can't be read, isn't a JSON object the mod can change, or the write failed.</summary>
    public bool Set(IReadOnlyList<string> path, string value)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(value);
        lock (gate)
        {
            string? before;
            try
            {
                before = store.Read();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return false;
            }
            if (SettingsText.Set(before, path, value) is not { } after)
            {
                return false;
            }
            settings.Load(after);
            try
            {
                store.Write(after);
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                settings.Load(before);
                return false;
            }
        }
    }
}
