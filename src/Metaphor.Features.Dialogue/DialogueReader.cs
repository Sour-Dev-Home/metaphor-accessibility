using Metaphor.Core.Announcements;

namespace Metaphor.Features.Dialogue;

/// <summary>The reader on/off switch (#58): spoken dialogue goes through it, and is dropped while it's off. It starts on.
/// Toggling announces the new state through the announcer underneath, so the player hears it either way. Safe to call
/// from any of the game's threads.
/// The idea of a key that turns the reader on and off comes from Persona-4-Golden-Access by AquaRose7 (GPL-3.0,
/// <c>M/Components/Speech.cs</c> and <c>Dialogue.cs</c> at <c>dcff255</c>;
/// docs/raw-sources/persona-4-golden-access-2026-11-16.md). No code is copied.</summary>
public sealed class DialogueReader : IReaderSwitch
{
    private readonly IAnnouncer announcer;
    private volatile bool on = true;

    public DialogueReader(IAnnouncer announcer)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        this.announcer = announcer;
    }

    /// <summary>Whether dialogue is spoken.</summary>
    public bool On => on;

    bool IReaderSwitch.IsOn => on;

    public void Announce(Announcement announcement)
    {
        if (on)
        {
            announcer.Announce(announcement);
        }
    }

    /// <summary>Turns the reader off if it's on, or on if it's off, and says which.</summary>
    public void Toggle()
    {
        on = !on;
        announcer.Announce(new Announcement(on ? "Dialogue reader on" : "Dialogue reader off", Interrupt: true));
    }
}
