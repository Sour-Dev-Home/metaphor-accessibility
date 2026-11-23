using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>The name entry's read-back (#585): the menus' details binding, R3 and F by default (maintainer's decision,
/// 2026-11-21), pressed while the name entry screen is up, says the name typed so far, privately, and is hidden from
/// the game. Elsewhere the binding reaches the game unchanged. While the game takes typed text the input frame keeps
/// the letter keys from every control (#807), so F types its letter there and only a non-letter details key reads the
/// name back on the keyboard. A press is an up-to-down edge on any pad or the keyboard, as for the menu controls. Any
/// of the game's threads may call it.</summary>
[FeatureSetting<MenusFeature>(MenusFeature.NameReadBack)]
public sealed class NameReadBack : IInputControls
{
    private const int KeyboardSource = -1;

    private readonly Func<string?> name;
    private readonly IAnnouncer announcer;
    private readonly Func<MenuBindings> bindings;
    private readonly Lock gate = new();
    private readonly HashSet<int> holding = [];

    /// <param name="name">The name to say, or null when the screen isn't up (<see cref="NameEntrySpeech.ReadBack"/>).
    /// </param>
    /// <param name="announcer">Where the name is said.</param>
    /// <param name="bindings">The menus' bindings in force, read on each input read.</param>
    public NameReadBack(Func<string?> name, IAnnouncer announcer, Func<MenuBindings> bindings)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(bindings);
        this.name = name;
        this.announcer = announcer;
        this.bindings = bindings;
    }

    public int Pad(int pad, int buttons, bool made)
    {
        if (!made)
        {
            return buttons;
        }
        var binding = bindings().DetailsButtons;
        var held = binding != 0 && (buttons & binding) == binding;
        return Read(pad, held) ? buttons & ~binding : buttons;
    }

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var key = bindings().DetailsKey;
        return Read(KeyboardSource, key != 0 && keys.Contains(key)) ? [.. keys.Where(held => held != key)] : keys;
    }

    // Records what the source holds, says the name on a press while the screen is up, and says whether to hide the
    // binding: held while the screen is up.
    private bool Read(int source, bool held)
    {
        bool pressed;
        lock (gate)
        {
            var before = holding.Count > 0;
            if (held)
            {
                holding.Add(source);
            }
            else
            {
                holding.Remove(source);
            }
            pressed = !before && holding.Count > 0;
        }
        if (!held)
        {
            return false;
        }
        if (name() is not { } said)
        {
            return false;
        }
        if (pressed)
        {
            announcer.Announce(new Announcement(said, Interrupt: true, Private: true));
        }
        return true;
    }
}
