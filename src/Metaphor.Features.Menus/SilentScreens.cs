using Metaphor.Core;
using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>The silent screen list (#597): every screen entered and every focus move the mod said nothing for goes to
/// the report log with its screen class, index and count, so a session's gaps are found from the log, not by ear. It
/// sits in front of the announcer every feature speaks through, counting what passes, and hears each screen event after
/// the features that speak for it: an event with nothing said since the one before is silent. Speech that a feature
/// says later, off the event's call, isn't seen, so such a screen may be listed though it spoke. At most
/// <see cref="MaxEntries"/> entries a session, so the log stays small. Safe to use from several threads.</summary>
[FeatureSetting<MenusFeature>(MenusFeature.SilentScreens)]
public sealed class SilentScreens : IAnnouncer, IScreenObserver
{
    /// <summary>The most entries listed in one session.</summary>
    public const int MaxEntries = 200;

    private readonly IAnnouncer inner;
    private readonly ISessionReport report;
    private readonly Func<ModSettings> settings;
    private readonly int limit;
    private readonly Lock gate = new();
    private long announced;
    private long seen;
    private int listed;

    /// <param name="inner">Where every announcement goes on to, unchanged.</param>
    /// <param name="report">Where each silent event is written.</param>
    /// <param name="settings">The settings in force, read on each event: the feature's toggle.</param>
    /// <param name="limit">The most entries listed.</param>
    public SilentScreens(IAnnouncer inner, ISessionReport report, Func<ModSettings> settings, int limit = MaxEntries)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(settings);
        this.inner = inner;
        this.report = report;
        this.settings = settings;
        this.limit = limit;
    }

    public void Announce(Announcement announcement)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        if (announcement.Text.Length > 0)
        {
            Interlocked.Increment(ref announced);
        }
        inner.Announce(announcement);
    }

    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Change == ScreenChange.Left)
        {
            return;
        }
        bool write;
        bool nowFull;
        lock (gate)
        {
            var now = Interlocked.Read(ref announced);
            var quiet = now == seen;
            seen = now;
            write = quiet && listed < limit && settings().IsOn(MenusFeature.SilentScreens);
            if (write)
            {
                listed++;
            }
            nowFull = write && listed == limit;
        }
        if (write)
        {
            report.Silent(change);
        }
        if (nowFull)
        {
            report.SilentListFull(limit);
        }
    }
}
