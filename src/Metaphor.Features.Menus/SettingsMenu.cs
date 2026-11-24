using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>The mod's own spoken settings menu (#754, the maintainer's decisions of 2026-11-23): F2, or the chord, opens
/// it on any screen and says its name, the section and the focused row (ADR 0023); Up and Down move, interrupting;
/// Left and Right change the value by one rule for every kind (#959): left is off, the first choice or less, right is
/// on, the last choice or more, stopping at either end; a number moves by its step (Shift: 5 steps); Enter or A turns
/// a toggle over or steps a choice round; and each change is saved to the settings file at once and said, then "saved"; F or R3 says what the row
/// does; Page Down and Page Up, or RB and LB, jump a section; Escape, B or F2 again closes. While it is open the game
/// gets nothing (<see cref="IInputBlock"/>), and after it closes the keys or buttons that closed it stay kept until they
/// are let go, so the game never sees half a press. Opened while a dialogue line is up, its first line waits behind the
/// one being read instead of cutting it off. Safe to call from any of the game's threads.</summary>
[FeatureSetting<MenusFeature>(MenusFeature.SettingsMenu)]
public sealed class SettingsMenu : IWindowInputControls, IInputBlock
{
    /// <summary>What the menu calls itself as it opens.</summary>
    public const string Name = "Mod settings";

    // Virtual-key codes (VK_* in WinUser.h, Windows SDK 10.0.26100.0). The game's keyboard read lists each when held:
    // it skips only codes in two bit tables, below 32 (0xF7E71000, which leaves Enter 0x0D and Escape 0x1B) and from
    // 0xA6, so the arrows, Page Up and Down and either Shift (0xA0, 0xA1) are listed
    // (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md, build 548ddc95).
    private const int Enter = 0x0D;
    private const int Escape = 0x1B;
    private const int PageUp = 0x21;
    private const int PageDown = 0x22;
    private const int Left = 0x25;
    private const int Up = 0x26;
    private const int Right = 0x27;
    private const int Down = 0x28;
    private const int LeftShift = 0xA0;
    private const int RightShift = 0xA1;

    // The game's pad mask on build 548ddc95 (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md).
    private const int PadUp = 0x0010;
    private const int PadDown = 0x0040;
    private const int PadLeft = 0x0080;
    private const int PadRight = 0x0020;
    private const int PadA = 0x4000;
    private const int PadB = 0x2000;
    private const int PadLb = 0x0400;
    private const int PadRb = 0x0800;

    /// <summary>How many steps Shift moves a number by (#754).</summary>
    public const int ShiftSteps = 5;

    private readonly Lock gate = new();
    private readonly Func<IReadOnlyList<PlayerSetting>> rows;
    private readonly Func<ModSettings> settings;
    private readonly Func<IReadOnlyList<string>, string, bool> save;
    private readonly IAnnouncer speech;
    private readonly Func<SettingsMenuBindings> bindings;
    private readonly Func<MenuBindings> details;
    private readonly Func<bool> on;
    private readonly Func<bool> positions;
    private readonly Dictionary<int, int> padsBefore = [];
    private readonly HashSet<int> keysBefore = [];
    private readonly HashSet<int> keysKept = [];
    private IReadOnlyList<PlayerSetting>? listed;
    private int padKept;
    private bool open;
    private int index;
    private long? window;

    /// <param name="rows">Every row, asked at the first opening, when every module has listed its keys.</param>
    /// <param name="settings">The settings in force, read for each row's value.</param>
    /// <param name="save">Sets a path in the settings file to JSON text and puts it in force; false when it couldn't
    /// (<see cref="SettingsEditor.Set"/>).</param>
    /// <param name="speech">Where the menu speaks: straight to speech, not the reader switch, behind its setting.
    /// </param>
    /// <param name="bindings">The open key and chord, read on each read.</param>
    /// <param name="details">The menus' details binding, which reads a row's description here too.</param>
    /// <param name="on">Whether the feature is on: off, it closes and hands every read on.</param>
    /// <param name="positions">Whether the "n of total" is said (<see cref="SpokenDetail.Positions"/>).</param>
    public SettingsMenu(
        Func<IReadOnlyList<PlayerSetting>> rows, Func<ModSettings> settings, Func<IReadOnlyList<string>, string, bool> save,
        IAnnouncer speech, Func<SettingsMenuBindings> bindings, Func<MenuBindings> details, Func<bool> on,
        Func<bool> positions)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(speech);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(on);
        ArgumentNullException.ThrowIfNull(positions);
        this.rows = rows;
        this.settings = settings;
        this.save = save;
        this.speech = speech;
        this.bindings = bindings;
        this.details = details;
        this.on = on;
        this.positions = positions;
    }

    /// <summary>Whether the menu is open.</summary>
    public bool IsOpen
    {
        get
        {
            lock (gate)
            {
                return open;
            }
        }
    }

    public bool BlocksPad
    {
        get
        {
            lock (gate)
            {
                return on() && (open || padKept != 0);
            }
        }
    }

    public bool BlocksKeys
    {
        get
        {
            lock (gate)
            {
                return on() && (open || keysKept.Count > 0);
            }
        }
    }

    public void WindowRead(long? currentMessage)
    {
        lock (gate)
        {
            window = currentMessage;
        }
    }

    public int Pad(int pad, int buttons, bool made)
    {
        if (!made)
        {
            return buttons;
        }
        lock (gate)
        {
            var down = buttons & ~padsBefore.GetValueOrDefault(pad);
            padsBefore[pad] = buttons;
            padKept &= buttons;
            if (!open)
            {
                var now = bindings();
                if (now.ChordButtons != 0 && now.SettingsButton != 0 &&
                    (buttons & now.ChordButtons) == now.ChordButtons && (down & now.SettingsButton) != 0)
                {
                    Open();
                    return 0;
                }
                return buttons;
            }
            var said = details().DetailsButtons;
            if ((down & PadB) != 0)
            {
                Close();
                padKept = buttons;
            }
            else if ((down & PadUp) != 0)
            {
                Move(-1);
            }
            else if ((down & PadDown) != 0)
            {
                Move(1);
            }
            else if ((down & PadLeft) != 0)
            {
                Change(-1);
            }
            else if ((down & PadRight) != 0)
            {
                Change(1);
            }
            else if ((down & PadA) != 0)
            {
                Activate();
            }
            else if ((down & PadLb) != 0)
            {
                Section(-1);
            }
            else if ((down & PadRb) != 0)
            {
                Section(1);
            }
            else if (said != 0 && (buttons & said) == said && (down & said) != 0)
            {
                ReadMore();
            }
            return 0;
        }
    }

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        lock (gate)
        {
            var down = keys.Where(key => !keysBefore.Contains(key)).ToList();
            keysBefore.Clear();
            keysBefore.UnionWith(keys);
            keysKept.IntersectWith(keys);
            var openKey = bindings().SettingsKey;
            if (!open)
            {
                if (openKey != 0 && down.Contains(openKey))
                {
                    Open();
                    return [];
                }
                return keysKept.Count == 0 ? keys : [.. keys.Where(key => !keysKept.Contains(key))];
            }
            var shift = keys.Contains(LeftShift) || keys.Contains(RightShift);
            var steps = shift ? ShiftSteps : 1;
            var more = details().DetailsKey;
            foreach (var key in down)
            {
                if (key == Escape || (openKey != 0 && key == openKey))
                {
                    Close();
                    keysKept.UnionWith(keys);
                    break;
                }
                switch (key)
                {
                    case Up:
                        Move(-1);
                        break;
                    case Down:
                        Move(1);
                        break;
                    case Left:
                        Change(-steps);
                        break;
                    case Right:
                        Change(steps);
                        break;
                    case Enter:
                        Activate();
                        break;
                    case PageUp:
                        Section(-1);
                        break;
                    case PageDown:
                        Section(1);
                        break;
                    default:
                        if (more != 0 && key == more)
                        {
                            ReadMore();
                        }
                        break;
                }
            }
            return [];
        }
    }

    /// <summary>The key list's rows (#754): each listed key once, said with its bindings, read-only. A fixed key (the
    /// menu's own arrows) and a Debug-only one aren't settings, so aren't listed.</summary>
    public static IEnumerable<PlayerSetting> KeyRows(IEnumerable<KeyEntry> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return keys.Select(entry => entry.Key)
            .Where(key => key.Fixed is null && !key.Hidden)
            .DistinctBy(key => (key.Does, key.Key, key.Button, key.Hold))
            .Select(key => new KeySetting(PlayerSettings.Keys, key.Does, now => key.Keys(now) ?? "none"));
    }

    private IReadOnlyList<PlayerSetting> Rows => listed ??= rows();

    private void Open()
    {
        open = true;
        index = Math.Clamp(index, 0, Math.Max(0, Rows.Count - 1));
        // A dialogue line being read is let finish: the menu's first line queues behind it.
        var reading = window is { } message && message != 0;
        var intro = SayIntro() ? $" {Intro()}" : "";
        Say($"{Name}.{intro} {Rows[index].Section}. {Line(index)}", interrupt: !reading);
    }

    /// <summary>Whether opening says the layout first (#927), read at each opening; off by default.</summary>
    public Func<bool> SayIntro { get; init; } = () => false;

    // "6 sections: General, Speech, ... Page Down and Page Up, or RB and LB, move between them. ..." (#927).
    private string Intro()
    {
        var sections = Rows.Select(row => row.Section).Distinct().ToList();
        const string Keys = "Page Down and Page Up, or RB and LB, move between them. Up and Down move, Left is off " +
            "or less, Right is on or more, Escape or B closes.";
        return string.Create(
            CultureInfo.InvariantCulture, $"{sections.Count} sections: {string.Join(", ", sections)}. {Keys}");
    }

    private void Close()
    {
        open = false;
        Say("Settings closed.");
    }

    private void Move(int by)
    {
        var count = Rows.Count;
        var next = (((index + by) % count) + count) % count;
        var crossed = Rows[next].Section != Rows[index].Section;
        index = next;
        Say(crossed ? $"{Rows[index].Section}. {Line(index)}" : Line(index));
    }

    private void Section(int by)
    {
        var sections = Rows.Select(row => row.Section).Distinct().ToList();
        var at = sections.IndexOf(Rows[index].Section);
        var next = sections[(((at + by) % sections.Count) + sections.Count) % sections.Count];
        index = Rows.ToList().FindIndex(row => row.Section == next);
        Say($"{next}. {Line(index)}");
    }

    private void Change(int steps) => Apply(row => row.Change(settings(), steps));

    private void Activate() => Apply(row => row.Activate(settings()));

    private void Apply(Func<PlayerSetting, SettingChange?> changeOf)
    {
        var row = Rows[index];
        if (row is KeySetting)
        {
            Say("To change a key, edit settings.json.");
            return;
        }
        if (changeOf(row) is not { } change)
        {
            Say(row.Value(settings()));
            return;
        }
        Say(save(change.Path, change.Json) ? $"{row.Value(settings())}, saved." : "Not saved.");
    }

    private void ReadMore()
    {
        var row = Rows[index];
        var text = row.Description.Replace("`", "", StringComparison.Ordinal);
        Say(row.Testing ? $"{text} Still being tested." : text);
    }

    // "Positions, on, 1 of 42", the position within the row's section.
    private string Line(int at)
    {
        var row = Rows[at];
        var section = Rows.Select((each, i) => (each, i)).Where(pair => pair.each.Section == row.Section).ToList();
        var value = row.Testing ? $"testing, {row.Value(settings())}" : row.Value(settings());
        var line = $"{row.Spoken}, {value}";
        if (!positions())
        {
            return line;
        }
        var position = section.FindIndex(pair => pair.i == at) + 1;
        return string.Create(CultureInfo.InvariantCulture, $"{line}, {position} of {section.Count}");
    }

    private void Say(string text, bool interrupt = true) => speech.Announce(new Announcement(text, interrupt));
}
