using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the travel map's destination panel (#545): each time the panel shows a destination, its name and its
/// detail line, each cutting off the last. A part whose message isn't known is left out; with neither known, nothing is
/// said. The same destination shown again within <see cref="RepeatMilliseconds"/> of the last showing is not said
/// again, so a panel redrawn in place stays quiet. Safe to call from several threads.</summary>
/// <remarks>On build 548ddc95 the panel (<c>sortie::map::Detail</c>) is handed each destination's name and detail as
/// message references (docs/findings/travel-map-destination-text-static.md).</remarks>
[FeatureSetting(Feature.TravelMap)]
public sealed class TravelMapDestination
{
    /// <summary>How long the same destination stays unsaid after it was last shown.</summary>
    public const long RepeatMilliseconds = 2000;

    private readonly IMessageByReference messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly Func<long> clock;
    private readonly Lock gate = new();
    private (MessageReference? Name, MessageReference? Detail)? last;
    private long lastAt;

    public TravelMapDestination(IMessageByReference messages, IAnnouncer announcer, TextCleaner cleaner, Func<long> clock)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(clock);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.clock = clock;
    }

    /// <summary>The panel showed a destination named <paramref name="name"/> with the line <paramref name="detail"/>;
    /// either is null when the panel's reference couldn't be read. Returns what was said, or null.</summary>
    public string? Shown(MessageReference? name, MessageReference? detail)
    {
        var now = clock();
        lock (gate)
        {
            var repeat = last == (name, detail) && now - lastAt < RepeatMilliseconds;
            last = (name, detail);
            lastAt = now;
            if (repeat)
            {
                return null;
            }
        }
        var parts = new[] { TextOf(name), TextOf(detail) }.OfType<string>().ToArray();
        if (parts.Length == 0)
        {
            return null;
        }
        var said = string.Join(". ", parts);
        announcer.Announce(new Announcement(said, Interrupt: true));
        return said;
    }

    private string? TextOf(MessageReference? reference) =>
        reference is not null && messages.Find(reference) is { } message &&
        cleaner.Clean(message.Text) is { Length: > 0 } text
            ? text
            : null;
}
