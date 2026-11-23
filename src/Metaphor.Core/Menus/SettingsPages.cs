namespace Metaphor.Core.Menus;

// Issue #560: the hook layer reads and logs what this names, kept here so it is tested without the game.

/// <summary>The four settings pages (System, Sound, Graphics, Display, in the category list's order) and where each keeps
/// its working copy of the values, on game build 548ddc95… (Steam build 18330018).</summary>
public static class SettingsPages
{
    /// <summary>The pages' screen classes as run-time type information names them
    /// (docs/raw-sources/settings-category-labels-session1-2026-11-20.md).</summary>
    public static IReadOnlyList<string> Classes { get; } =
    [
        "?$ConfigMenuChoicePcT@$0HG@@ope@ui@app",
        "?$ConfigMenuChoicePcT@$0HH@@ope@ui@app",
        "?$ConfigMenuChoicePcT@$0HI@@ope@ui@app",
        "?$ConfigMenuChoicePcT@$0HJ@@ope@ui@app",
    ];

    /// <summary>A page keeps a dword per setting ID in a container at <c>+0x4c8</c> of its choice, its length at
    /// <c>+0x4d0</c> (docs/findings/settings-screens-inventory-static.md). The container reaches its data through a
    /// virtual call; the sessions' dumps read as the dwords inline from <c>+0x10</c> here (<see cref="SettingValues"/>,
    /// <c>[NEEDS VERIFICATION]</c> in the exe). The hook reads <see cref="WorkingCopyLength"/> bytes from here on a
    /// page, for its values (#96) and, in Debug, a log of which bytes change (#558's check 2).</summary>
    public const int WorkingCopyOffset = 0x4c8;

    /// <summary>How many bytes the Debug log dumps: the container's header and, if its data is inline, about 60 IDs
    /// (the PC settings go up to 49).</summary>
    public const int WorkingCopyLength = 0x100;

    /// <summary>Key Configuration's screen class, the category list's fifth page, on the grid cursor
    /// (docs/findings/settings-screens-inventory-static.md).</summary>
    public const string KeyConfigClass = "ConfigMenuChoiceKeyConfig@ope@ui@app";

    /// <summary>Where the Debug log dumps Key Configuration's choice from (#564): its <c>vf41</c> (<c>0x7fb690</c>)
    /// branches on a byte at <c>+0x908</c> and a mode at <c>+0x914</c> (docs/findings/settings-screens-inventory-static.md).
    /// Which marks "waiting for a key" is <c>[NEEDS VERIFICATION]</c>; a session's dumps settle it.</summary>
    public const int KeyConfigStateOffset = 0x900;

    /// <summary>How many bytes the Debug log dumps from <see cref="KeyConfigStateOffset"/>: both fields and their
    /// neighbours.</summary>
    public const int KeyConfigStateLength = 0x20;

    /// <summary>Key Configuration's mode at <c>+0x914</c> in its choice (#589): <see cref="KeyConfigOnButtonMode"/> while
    /// the OK button has the focus, 0 on every cell, where the cursor flags' item bit isn't reliable
    /// (docs/raw-sources/key-config-actions-session-2026-11-21.md, game build 548ddc95…).</summary>
    public const int KeyConfigModeOffset = 0x914;

    /// <summary>The <see cref="KeyConfigModeOffset"/> value while the OK button has the focus.</summary>
    public const int KeyConfigOnButtonMode = 1;

    /// <summary>Whether Key Configuration's mode, as read (null when unreadable), says the OK button has the focus; an
    /// unread mode counts as a cell, so a misread says the cell, not the button.</summary>
    public static bool KeyConfigOnButton(int? mode) => mode == KeyConfigOnButtonMode;

    /// <summary>Whether <paramref name="screenClass"/> is one of the four pages.</summary>
    public static bool IsPage(string? screenClass) => screenClass is not null && Classes.Contains(screenClass);
}

/// <summary>A settings page's values (#96), decoded from its working copy (<see cref="SettingsPages.WorkingCopyOffset"/>):
/// one signed dword per setting ID from <c>+0x10</c>, and at <c>+0x8</c> the setting count, <see cref="Count"/> in every
/// dump on game build 548ddc95… (docs/raw-sources/settings-values-sessions-2026-11-21.md). That the dwords are the
/// values by ID is our reading of which dword moves as a row is changed, <c>[NEEDS VERIFICATION]</c> in the exe; a copy
/// whose count reads otherwise isn't read. Two copies with the same values are equal.</summary>
public sealed class SettingValues : IEquatable<SettingValues>
{
    /// <summary>The setting count the copy keeps at <c>+0x8</c> on this build.</summary>
    public const int Count = 51;

    private const int CountOffset = 0x8;
    private const int ValuesOffset = 0x10;

    private readonly int[] values;

    private SettingValues(int[] values) => this.values = values;

    /// <summary>The values in <paramref name="copy"/>, the bytes from the copy's start, or null when it is too short to
    /// hold every ID or its count isn't <see cref="Count"/>.</summary>
    public static SettingValues? Of(ReadOnlySpan<byte> copy)
    {
        if (copy.Length < ValuesOffset + (4 * Count)
            || BitConverter.ToInt32(copy.Slice(CountOffset, 4)) != Count)
        {
            return null;
        }
        var values = new int[Count];
        for (var id = 0; id < Count; id++)
        {
            values[id] = BitConverter.ToInt32(copy.Slice(ValuesOffset + (4 * id), 4));
        }
        return new SettingValues(values);
    }

    /// <summary>The value of setting <paramref name="id"/>, or null for an ID outside the copy.</summary>
    public int? Value(int id) => id >= 0 && id < values.Length ? values[id] : null;

    /// <summary>The IDs whose value differs between this copy and <paramref name="other"/>.</summary>
    public IEnumerable<int> ChangedIds(SettingValues other)
    {
        ArgumentNullException.ThrowIfNull(other);
        for (var id = 0; id < values.Length; id++)
        {
            if (values[id] != other.values[id])
            {
                yield return id;
            }
        }
    }

    public bool Equals(SettingValues? other) => other is not null && values.AsSpan().SequenceEqual(other.values);

    public override bool Equals(object? obj) => Equals(obj as SettingValues);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in values)
        {
            hash.Add(value);
        }
        return hash.ToHashCode();
    }
}

/// <summary>Gives each object's bytes as hex the first time and whenever they change, and null otherwise, so a log of
/// them holds only changes. Keeps at most 64 objects, then starts over. Safe to use from several threads.</summary>
public sealed class ChangedBytes
{
    private const int MaxObjects = 64;

    private readonly Dictionary<long, string> last = [];
    private readonly Lock gate = new();

    /// <summary>The bytes as lower-case hex when they are new for <paramref name="key"/> or differ from its last, else
    /// null.</summary>
    public string? Changed(long key, ReadOnlySpan<byte> bytes)
    {
        var hex = Convert.ToHexStringLower(bytes);
        lock (gate)
        {
            if (last.TryGetValue(key, out var before) && before == hex)
            {
                return null;
            }
            if (last.Count >= MaxObjects && !last.ContainsKey(key))
            {
                last.Clear();
            }
            last[key] = hex;
            return hex;
        }
    }
}
