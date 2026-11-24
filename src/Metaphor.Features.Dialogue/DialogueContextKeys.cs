using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue;

/// <summary>The line the dialogue window shows, as the mod cleaned it: its speaker (empty when none) and its text
/// (#879). Recorded for every line, spoken or not, so the speaker and line keys work with the reader off. Safe from any
/// of the game's threads.</summary>
public sealed class ShownLine
{
    private readonly Lock gate = new();
    private (string Speaker, string Text)? current;

    /// <summary>The line on screen, or null before any line or once its window closed.</summary>
    public (string Speaker, string Text)? Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }

    /// <summary>A line was shown, with its cleaned speaker (empty when none) and text.</summary>
    public void Record(string speaker, string text)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        ArgumentNullException.ThrowIfNull(text);
        lock (gate)
        {
            current = (speaker, text);
        }
    }

    /// <summary>The window closed: no line is on screen.</summary>
    public void Clear()
    {
        lock (gate)
        {
            current = null;
        }
    }
}

/// <summary>Which keys and buttons the dialogue's context keys (#879) use. Keys are Windows virtual-key codes, buttons
/// bits of the game's pad mask as in <see cref="DialogueBindings"/>; 0 leaves one unbound.</summary>
/// <param name="StopKey">Stops the speech.</param>
/// <param name="SpeakerKey">Says who is speaking.</param>
/// <param name="LineKey">Says the line on screen, without its speaker.</param>
/// <param name="StopButtons">The stop key on the pad.</param>
/// <param name="SpeakerButtons">The speaker key on the pad.</param>
/// <param name="LineButtons">The line key on the pad.</param>
public sealed record DialogueKeyBindings(
    int StopKey, int SpeakerKey, int LineKey, int StopButtons, int SpeakerButtons, int LineButtons)
{
    /// <summary><c>P</c>, <c>O</c> and <c>U</c> (<c>VK_P</c> 0x50, <c>VK_O</c> 0x4F, <c>VK_U</c> 0x55), keys no other
    /// mod control uses; that the game binds none of them in dialogue is <c>[NEEDS VERIFICATION]</c> (no source records
    /// its keyboard defaults). No pad button: in dialogue the game uses the face buttons and shoulders, and the mod
    /// <c>X</c> and <c>LB</c> + <c>RB</c> (ADR 0023: never a button the game uses on that screen).</summary>
    public static DialogueKeyBindings Default { get; } = new(0x50, 0x4F, 0x55, 0, 0, 0);

    /// <summary>The bindings the settings file's <c>keys.dialogue</c> object names (#661), each missing or unusable
    /// one the default.</summary>
    public static DialogueKeyBindings From(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(
            settings.Binding("dialogue", "stopKey", Default.StopKey),
            settings.Binding("dialogue", "speakerKey", Default.SpeakerKey),
            settings.Binding("dialogue", "lineKey", Default.LineKey),
            settings.Binding("dialogue", "stopButtons", Default.StopButtons),
            settings.Binding("dialogue", "speakerButtons", Default.SpeakerButtons),
            settings.Binding("dialogue", "lineButtons", Default.LineButtons));
    }
}

/// <summary>The dialogue's context keys (#879, ADR 0023): stop the speech, say who is speaking, and say the line on
/// screen. They act only while the dialogue window holds a message, and are hidden from the game then; elsewhere they
/// do nothing and the game keeps them. Who and the line are said outside the reader switch, so they work with the reader
/// off. A press is an up-to-down edge on any pad or the keyboard, as for <see cref="DialogueControls"/>, and a key
/// already held when the window opens doesn't fire. The bindings are read on each input read (#661). Idea only from
/// Unseen Desire, a Persona 5 Royal accessibility mod (heavenay.com/unseenDesire); no code read (CREDITS.md).</summary>
[FeatureSetting<DialogueFeature>(DialogueFeature.DialogueKeys)]
public sealed class DialogueContextKeys : IWindowInputControls
{
    private const int KeyboardSource = -1;

    private readonly Lock gate = new();
    private readonly Func<DialogueKeyBindings> liveBindings;
    private readonly ShownLine line;
    private readonly IAnnouncer announcer;
    private readonly Action stop;
    private readonly HeldBinding stopHeld = new();
    private readonly HeldBinding speakerHeld = new();
    private readonly HeldBinding lineHeld = new();
    private bool open;

    /// <param name="bindings">The bindings in force, read on each input read.</param>
    /// <param name="line">The line on screen.</param>
    /// <param name="announcer">Where who and the line are said, reader on or off.</param>
    /// <param name="stop">Stops all speech.</param>
    public DialogueContextKeys(
        Func<DialogueKeyBindings> bindings, ShownLine line, IAnnouncer announcer, Action stop)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(stop);
        liveBindings = bindings;
        this.line = line;
        this.announcer = announcer;
        this.stop = stop;
    }

    /// <summary>One read of the message the dialogue window holds, as <see cref="DialogueControls.WindowRead"/>: a
    /// close leaves no line on screen.</summary>
    public void WindowRead(long? currentMessage)
    {
        var nowOpen = currentMessage is { } message && message != 0;
        lock (gate)
        {
            if (open && !nowOpen)
            {
                line.Clear();
            }
            open = nowOpen;
        }
    }

    public int Pad(int pad, int buttons, bool made)
    {
        if (!made)
        {
            return buttons;
        }
        var bindings = liveBindings();
        var hidden = Read(
            pad, Holds(buttons, bindings.StopButtons), Holds(buttons, bindings.SpeakerButtons),
            Holds(buttons, bindings.LineButtons));
        return hidden ? buttons & ~(bindings.StopButtons | bindings.SpeakerButtons | bindings.LineButtons) : buttons;
    }

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var bindings = liveBindings();
        var hidden = Read(
            KeyboardSource, Holds(keys, bindings.StopKey), Holds(keys, bindings.SpeakerKey),
            Holds(keys, bindings.LineKey));
        return hidden && (Holds(keys, bindings.StopKey) || Holds(keys, bindings.SpeakerKey) || Holds(keys, bindings.LineKey))
            ? [.. keys.Where(key => key != bindings.StopKey && key != bindings.SpeakerKey && key != bindings.LineKey)]
            : keys;
    }

    private static bool Holds(int buttons, int binding) => binding != 0 && (buttons & binding) == binding;

    private static bool Holds(IReadOnlyList<int> keys, int binding) => binding != 0 && keys.Contains(binding);

    /// <summary>Records what <paramref name="source"/> holds, acts on each press, and says whether the bindings are
    /// hidden from the game: while the window is open.</summary>
    private bool Read(int source, bool stopNow, bool speakerNow, bool lineNow)
    {
        bool stopPressed;
        bool speakerPressed;
        bool linePressed;
        bool inDialogue;
        lock (gate)
        {
            inDialogue = open;
            stopPressed = stopHeld.Update(source, stopNow) && open;
            speakerPressed = speakerHeld.Update(source, speakerNow) && open;
            linePressed = lineHeld.Update(source, lineNow) && open;
        }
        // Outside the lock: the announcer may take its time.
        if (stopPressed)
        {
            stop();
        }
        if (speakerPressed || linePressed)
        {
            var shown = line.Current;
            if (speakerPressed)
            {
                Say(shown is not { } who ? "No line on screen" : who.Speaker.Length == 0 ? "No speaker" : who.Speaker);
            }
            if (linePressed)
            {
                Say(shown is { } on ? on.Text : "No line on screen");
            }
        }
        return inDialogue;
    }

    private void Say(string text) => announcer.Announce(new Announcement(text, Interrupt: true));
}
