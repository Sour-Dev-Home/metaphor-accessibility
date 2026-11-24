namespace Metaphor.Features.Menus;

/// <summary>Reads a save slot's header.</summary>
public interface ISaveSlotHeaders
{
    /// <summary>Slot <paramref name="slot"/>'s header, or null when it can't be read.</summary>
    SaveSlotHeader? Read(int slot);
}

/// <summary>Reads each slot's header from the player's own save folder, read-only (#773): the first
/// <see cref="SaveSlotHeader.Size"/> bytes of <c>save%04d.sav</c>, opened for reading while the game may write it, and
/// nothing else. Nothing read is written or sent anywhere. A missing folder or file, or a failed read, gives null.
/// Safe to use from several threads.</summary>
/// <remarks>The maintainer allowed reading the save files on 2026-11-24 (#773). The folder is the Steam version's,
/// <c>%APPDATA%\SEGA\METAPHOR\Steam\&lt;Steam ID&gt;</c> (docs/findings/save-slot-header-in-save-files.md), found from
/// the environment; with several Steam IDs, the one with the newest save file is the player's. Other stores' folders
/// aren't known, so they read nothing.</remarks>
public sealed class SaveSlotFiles : ISaveSlotHeaders
{
    private readonly Func<string?> folder;

    /// <summary>Reads from the folder <paramref name="folder"/> names at each read, or nothing when it gives null.</summary>
    public SaveSlotFiles(Func<string?> folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        this.folder = folder;
    }

    /// <summary>Reads from the Steam version's save folder under <paramref name="appData"/>, the roaming application
    /// data folder.</summary>
    public static SaveSlotFiles Steam(string appData)
    {
        ArgumentNullException.ThrowIfNull(appData);
        return new SaveSlotFiles(() => SteamFolder(appData));
    }

    /// <summary>Reads from the Steam version's save folder under the current user's roaming application data.</summary>
    public static SaveSlotFiles FromEnvironment() =>
        Steam(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    /// <summary>The Steam ID folder under <paramref name="appData"/> whose newest save file is newest, or null.</summary>
    public static string? SteamFolder(string appData)
    {
        ArgumentNullException.ThrowIfNull(appData);
        try
        {
            var root = new DirectoryInfo(Path.Combine(appData, "SEGA", "METAPHOR", "Steam"));
            if (!root.Exists)
            {
                return null;
            }
            var hidden = new EnumerationOptions { AttributesToSkip = 0 };
            return root.EnumerateDirectories("*", hidden)
                .Select(user => (user.FullName, Newest: user.EnumerateFiles("save*.sav", hidden)
                    .Select(file => file.LastWriteTimeUtc).DefaultIfEmpty(DateTime.MinValue).Max()))
                .Where(user => user.Newest != DateTime.MinValue)
                .OrderByDescending(user => user.Newest)
                .Select(user => user.FullName)
                .FirstOrDefault();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public SaveSlotHeader? Read(int slot)
    {
        if (slot is < 0 or > 9999 || folder() is not { } path)
        {
            return null;
        }
        try
        {
            using var file = new FileStream(
                Path.Combine(path, $"save{slot:D4}.sav"), FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, bufferSize: 1);
            var header = new byte[SaveSlotHeader.Size];
            file.ReadExactly(header);
            return SaveSlotHeader.Parse(header, slot);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }
}
