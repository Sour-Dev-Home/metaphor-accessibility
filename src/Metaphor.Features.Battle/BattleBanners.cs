using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Battle;

/// <summary>Says the battle's in-turn situation banner (#692): boss warnings, stat changes, waiting and fleeing, "Not
/// enough MP!", turn icons gained or lost. A banner of <see cref="SituationKind"/> is the message at its id's position
/// in <see cref="SituationFile"/>; it is said queued, after the hit lines already waiting, and the same banner shown
/// again within <see cref="RepeatMilliseconds"/> is not said again. Other kinds say nothing. A name the banner fills in
/// (<c>&lt;V 0&gt;</c>) is left out, as no read of it is known. Safe to call from several threads.</summary>
/// <remarks>
/// <para>On build 548ddc95… (Steam build 18330018) the banner component's two show functions map kind 2 to message-file
/// table entry 9, <c>message/system/jyokyo_help_Message.msg</c>, at position id
/// (docs/findings/battle-situation-banner-text-static.md).</para>
/// <para>The idea of reading the in-turn banner comes from Arcana, the Persona 3 Reload accessibility mod
/// (<c>ChipsAhoiMcCoy/arcana</c>, Zlib licence, release v0.5.0 at <c>eae8dbd</c>, <c>main</c> at <c>ed3b005f</c>,
/// <c>CHANGELOG.md</c> "0.5.0 / Battle", <c>src/screens/battle.cpp</c>). No code is copied.</para>
/// </remarks>
public sealed class BattleBanners
{
    /// <summary>The banner kind that names a situation message by its position.</summary>
    public const int SituationKind = 2;

    /// <summary>The file the situation messages are in.</summary>
    public const string SituationFile = "jyokyo_help_Message.msg";

    /// <summary>How long the same banner stays unsaid after it was last shown.</summary>
    public const long RepeatMilliseconds = 1000;

    private readonly IMessageInFile messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly Func<long> clock;
    private readonly Func<bool> enabled;
    private readonly Lock gate = new();
    private (int Kind, int Id)? last;
    private long lastAt;

    /// <summary>Banners said through <paramref name="announcer"/> while <paramref name="enabled"/> (always, when not
    /// given) is true.</summary>
    public BattleBanners(
        IMessageInFile messages, IAnnouncer announcer, TextCleaner cleaner, Func<long> clock, Func<bool>? enabled = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(clock);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.clock = clock;
        this.enabled = enabled ?? (() => true);
    }

    /// <summary>The banner component showed a banner of <paramref name="kind"/> and <paramref name="id"/>. Returns what
    /// was said, or null.</summary>
    public string? Shown(int kind, int id)
    {
        if (kind != SituationKind || !enabled())
        {
            return null;
        }
        var now = clock();
        lock (gate)
        {
            var repeat = last == (kind, id) && now - lastAt < RepeatMilliseconds;
            last = (kind, id);
            lastAt = now;
            if (repeat)
            {
                return null;
            }
        }
        if (messages.FindInFile(SituationFile, id) is not { } message ||
            cleaner.Clean(message.Text) is not { Length: > 0 } said)
        {
            return null;
        }
        announcer.Announce(new Announcement(said, Interrupt: false));
        return said;
    }
}
