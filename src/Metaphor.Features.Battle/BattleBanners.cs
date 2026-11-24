using System.Text.RegularExpressions;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>Says the battle's in-turn situation banner (#692): boss warnings, stat changes, waiting and fleeing, "Not
/// enough MP!", turn icons gained or lost. A banner of <see cref="SituationKind"/> is the message at its id's position
/// in <see cref="SituationFile"/>; it is said queued, after the hit lines already waiting, and the same banner shown
/// again within <see cref="RepeatMilliseconds"/> for the same subject is not said again. Other kinds say nothing. The
/// subject's name the banner fills in (<c>&lt;V 0&gt;</c>, #739) is said when <see cref="NamesOn"/> and the hook read
/// it from message variable slot 0, as a serial <see cref="SerialName"/> knows or as text; otherwise it is left out, as
/// is any other <c>&lt;V n&gt;</c>. Safe to call from several threads.</summary>
/// <remarks>
/// <para>On build 548ddc95… (Steam build 18330018) the banner component's two show functions map kind 2 to message-file
/// table entry 9, <c>message/system/jyokyo_help_Message.msg</c>, at position id
/// (docs/findings/battle-situation-banner-text-static.md).</para>
/// <para>The idea of reading the in-turn banner comes from Arcana, the Persona 3 Reload accessibility mod
/// (<c>ChipsAhoiMcCoy/arcana</c>, Zlib licence, release v0.5.0 at <c>eae8dbd</c>, <c>main</c> at <c>ed3b005f</c>,
/// <c>CHANGELOG.md</c> "0.5.0 / Battle", <c>src/screens/battle.cpp</c>). No code is copied.</para>
/// </remarks>
[FeatureSetting<BattleFeature>(BattleFeature.BattleBanners)]
public sealed partial class BattleBanners
{
    /// <summary>The banner kind that names a situation message by its position.</summary>
    public const int SituationKind = 2;

    /// <summary>The file the situation messages are in, by its path from the <c>message</c> folder on, the key
    /// <see cref="MessageFilePositions"/> uses.</summary>
    public const string SituationFile = "message/system/jyokyo_help_Message.msg";

    /// <summary>The situation file as game build 548ddc95… (Steam build 18330018) compiles it: 259 messages in the
    /// mod's message log of 2026-11-21 (docs/findings/battle-situation-banner-text-static.md). Another count is another
    /// build's file, whose positions say nothing.</summary>
    public static MessageFileShape Steam18330018 { get; } = new(SituationFile, 259);

    /// <summary>How long the same banner stays unsaid after it was last shown.</summary>
    public const long RepeatMilliseconds = 1000;

    // The shared cleaner would say "value" for <V n> (#739), so each is named or dropped before cleaning. Only <V 0> is
    // known to be the subject's name (docs/findings/battle-banner-name-variable-static.md).
    [GeneratedRegex(@"<V(?: [^>]*)?>")]
    private static partial Regex NameTag();

    [GeneratedRegex(@"<V 0>")]
    private static partial Regex SubjectTag();

    /// <summary>A name serial's text, or null when it isn't a name the mod knows. None, when not set.</summary>
    public Func<int, string?> SerialName { get; init; } = _ => null;

    /// <summary>Whether the subject's name is said (#739, testing). Off, when not set.</summary>
    public Func<bool> NamesOn { get; init; } = () => false;

    private readonly MessageFilePositions messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly Func<long> clock;
    private readonly Func<bool> enabled;
    private readonly Lock gate = new();
    private (int Kind, int Id, string? Name)? last;
    private long lastAt;

    /// <summary>Banners said through <paramref name="announcer"/> while <paramref name="enabled"/> (always, when not
    /// given) is true.</summary>
    public BattleBanners(
        MessageFilePositions messages, IAnnouncer announcer, TextCleaner cleaner, Func<long> clock, Func<bool>? enabled = null)
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

    /// <summary>The banner component showed a banner of <paramref name="kind"/> and <paramref name="id"/>, with
    /// <paramref name="subject"/> in message variable slot 0 when the hook read one. Returns what was said, or null.
    /// </summary>
    public string? Shown(int kind, int id, BannerName? subject = null)
    {
        if (kind != SituationKind || !enabled())
        {
            return null;
        }
        var name = subject is { } read && NamesOn()
            ? read.Text ?? (read.Serial is { } serial ? SerialName(serial) : null)
            : null;
        var now = clock();
        lock (gate)
        {
            var repeat = last == (kind, id, name) && now - lastAt < RepeatMilliseconds;
            last = (kind, id, name);
            lastAt = now;
            if (repeat)
            {
                return null;
            }
        }
        if (messages.Find(SituationFile, id) is not { } message)
        {
            return null;
        }
        var text = name is null
            ? message.Text
            : SubjectTag().Replace(message.Text, name.Replace("$", "$$", StringComparison.Ordinal));
        if (cleaner.Clean(NameTag().Replace(text, "")).Trim() is not { Length: > 0 } said)
        {
            return null;
        }
        announcer.Announce(new Announcement(said, Interrupt: false));
        return said;
    }
}
