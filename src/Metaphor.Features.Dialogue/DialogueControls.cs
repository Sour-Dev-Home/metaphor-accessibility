using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue;

/// <summary>Which buttons and keys the dialogue controls (#58) use. Buttons are bits of the game's own 16-bit pad mask
/// and a binding of several bits means all of them held together; keys are Windows virtual-key codes.</summary>
/// <param name="RepeatButtons">The buttons that repeat the last dialogue line.</param>
/// <param name="ReaderButtons">The buttons that turn the dialogue reader on or off.</param>
/// <param name="RepeatKey">The key that repeats the last dialogue line.</param>
/// <param name="ReaderKey">The key that turns the dialogue reader on or off.</param>
public sealed record DialogueBindings(int RepeatButtons, int ReaderButtons, int RepeatKey, int ReaderKey)
{
    /// <summary>X and LB + RB, R and T (#58's design comment). The game's mask puts XInput's X at <c>0x8000</c>, LB at
    /// <c>0x400</c> and RB at <c>0x800</c> on game build 548ddc95… (Steam build 18330018):
    /// docs/raw-sources/pad-and-keyboard-state-2026-11-16.md. R and T are <c>VK_R</c> (0x52) and <c>VK_T</c> (0x54).
    /// The settings file can rebind each (#661, <see cref="From"/>).</summary>
    public static DialogueBindings Default { get; } = new(0x8000, 0x400 | 0x800, 0x52, 0x54);

    /// <summary>The bindings the settings file's <c>keys.dialogue</c> object names (#661), each missing or unusable
    /// one the default.</summary>
    public static DialogueBindings From(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(
            settings.Binding("dialogue", "repeatButtons", Default.RepeatButtons),
            settings.Binding("dialogue", "readerButtons", Default.ReaderButtons),
            settings.Binding("dialogue", "repeatKey", Default.RepeatKey),
            settings.Binding("dialogue", "readerKey", Default.ReaderKey));
    }
}

/// <summary>The dialogue controls (#58): the repeat binding re-speaks the last line in full and the reader binding turns
/// spoken dialogue on or off. They act only while the last <see cref="WindowRead"/> found the dialogue window holding a
/// message, and are hidden from the game then. While it's closed, before any read, or when it couldn't be read, they do
/// nothing and the game keeps them (#206, #232). A press is an up-to-down edge of a binding held on any pad or the
/// keyboard, so a second pad mirroring the first (Steam Input's virtual pad) or one whose sticks drift can't fire it
/// again, and a binding already held when the window opens doesn't fire. The input frame (#301) hands it each pad and
/// keyboard read and each read of the window, from any of the game's threads. The bindings are read on each input
/// read, so a rebinding in the settings file applies at once (#661).</summary>
[FeatureSetting(Feature.Dialogue)]
public sealed class DialogueControls : IWindowInputControls
{
    private const int KeyboardSource = -1;

    private readonly Lock gate = new();
    private readonly Func<DialogueBindings> liveBindings;
    private readonly DialogueHistory history;
    private readonly DialogueReader reader;
    private readonly IAnnouncer announcer;
    private readonly Action? onClose;
    private readonly Binding repeat = new();
    private readonly Binding toggle = new();
    private bool open;
    // The history's count when the window last closed: repeat speaks only lines recorded after it (#526).
    private long closedAt;

    public DialogueControls(DialogueBindings bindings, DialogueHistory history, DialogueReader reader, IAnnouncer announcer, Action? onClose = null)
        : this(() => bindings, history, reader, announcer, onClose)
    {
        ArgumentNullException.ThrowIfNull(bindings);
    }

    /// <summary>Dialogue controls on the bindings in force, read on each input read (#661).</summary>
    public DialogueControls(Func<DialogueBindings> bindings, DialogueHistory history, DialogueReader reader, IAnnouncer announcer, Action? onClose = null)
    {
        this.onClose = onClose;
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(announcer);
        liveBindings = bindings;
        this.history = history;
        this.reader = reader;
        this.announcer = announcer;
    }

    /// <summary>One read of the message the dialogue window holds (<c>[W+0x60]</c>): non-zero while a line or a choice
    /// is on screen, 0 once the window closes, and null when it couldn't be read, which counts as closed. Game build
    /// 548ddc95… (Steam build 18330018): docs/raw-sources/dialogue-window-flag-session5-2026-11-17.md. A close makes
    /// every line so far stale, so repeat in the next window never replays the last window's line (#526).</summary>
    public void WindowRead(long? currentMessage)
    {
        lock (gate)
        {
            var nowOpen = currentMessage is { } message && message != 0;
            if (open && !nowOpen)
            {
                closedAt = history.Recorded;
                // A closed window ends the conversation: the next line says its speaker again (#539).
                onClose?.Invoke();
            }
            open = nowOpen;
        }
    }

    /// <summary>Takes one read of <paramref name="pad"/>'s buttons, acts on a press, and returns the buttons to hand
    /// the game: without the bound ones while the window is open.</summary>
    public int Pad(int pad, int buttons) => Pad(pad, buttons, made: true);

    /// <summary>Takes one read of <paramref name="pad"/>'s buttons that the game reports as made or not (#225). A read
    /// not made is ignored, so leftover bits in it can't fire a binding, and its buttons are handed back
    /// unchanged.</summary>
    public int Pad(int pad, int buttons, bool made)
    {
        if (!made)
        {
            return buttons;
        }
        var bindings = liveBindings();
        var repeatHeld = Holds(buttons, bindings.RepeatButtons);
        var toggleHeld = Holds(buttons, bindings.ReaderButtons);
        var hidden = Read(pad, repeatHeld, toggleHeld);
        return hidden ? buttons & ~(bindings.RepeatButtons | bindings.ReaderButtons) : buttons;
    }

    /// <summary>Takes one read of the keys held down, acts on a press, and returns the keys to hand the game: without the
    /// bound ones while the window is open.</summary>
    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var bindings = liveBindings();
        var hidden = Read(KeyboardSource, keys.Contains(bindings.RepeatKey), keys.Contains(bindings.ReaderKey));
        return hidden ? [.. keys.Where(key => key != bindings.RepeatKey && key != bindings.ReaderKey)] : keys;
    }

    private static bool Holds(int buttons, int binding) => binding != 0 && (buttons & binding) == binding;

    /// <summary>Records what <paramref name="source"/> holds, acts on each press, and says whether the bound input is
    /// hidden from the game.</summary>
    private bool Read(int source, bool repeatHeld, bool toggleHeld)
    {
        bool repeatPressed;
        bool togglePressed;
        bool hidden;
        long since;
        lock (gate)
        {
            hidden = open;
            since = closedAt;
            repeatPressed = repeat.Update(source, repeatHeld) && open;
            togglePressed = toggle.Update(source, toggleHeld) && open;
        }
        // Spoken outside the lock: the announcer may take its time.
        if (repeatPressed)
        {
            announcer.Announce(new Announcement(history.LatestSince(since) ?? "No dialogue to repeat", Interrupt: true));
        }
        if (togglePressed)
        {
            reader.Toggle();
        }
        return hidden;
    }

    /// <summary>One binding's state across sources: which hold it now.</summary>
    private sealed class Binding
    {
        private readonly HashSet<int> holding = [];

        /// <summary>Records whether <paramref name="source"/> holds the binding, and says whether that made it go from
        /// held nowhere to held somewhere.</summary>
        public bool Update(int source, bool held)
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
            return !before && holding.Count > 0;
        }
    }
}
