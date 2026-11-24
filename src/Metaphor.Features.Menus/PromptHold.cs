using Metaphor.Core.Announcements;

namespace Metaphor.Features.Menus;

/// <summary>Keeps a menu prompt from being cut off by the row line that follows it (#923). The menus speak through
/// this in place of the reader: after a prompt is said (through <see cref="Prompts"/>), the first interrupting line
/// within <see cref="Window"/> is queued behind it instead, so "Which item?" is heard whole. Any later line interrupts
/// as usual, so the next cursor move is never held back.</summary>
public sealed class PromptHold : IReaderSwitch
{
    /// <summary>How long after a prompt the next interrupting line is queued behind it, in milliseconds.</summary>
    public const long Window = 2500;

    private readonly IReaderSwitch inner;
    private readonly Func<long> clock;
    private readonly object gate = new();
    private long holdUntil = long.MinValue;

    public PromptHold(IReaderSwitch inner, Func<long> clock)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(clock);
        this.inner = inner;
        this.clock = clock;
        Prompts = new PromptSide(this);
    }

    /// <summary>Where prompts are announced: queued as given, and the hold starts.</summary>
    public IAnnouncer Prompts { get; }

    public bool IsOn => inner.IsOn;

    public void Toggle() => inner.Toggle();

    public void Announce(Announcement announcement)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        if (announcement.Interrupt)
        {
            lock (gate)
            {
                if (clock() < holdUntil)
                {
                    holdUntil = long.MinValue;
                    announcement = announcement with { Interrupt = false };
                }
            }
        }
        inner.Announce(announcement);
    }

    private void Said(Announcement prompt)
    {
        inner.Announce(prompt);
        lock (gate)
        {
            holdUntil = clock() + Window;
        }
    }

    private sealed class PromptSide(PromptHold owner) : IAnnouncer
    {
        public void Announce(Announcement announcement)
        {
            ArgumentNullException.ThrowIfNull(announcement);
            owner.Said(announcement);
        }
    }
}
