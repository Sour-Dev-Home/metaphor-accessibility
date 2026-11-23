using System.Diagnostics.CodeAnalysis;
using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Updates;

/// <summary>Says once on the title screen that a newer release exists (#576, ADR 0018): "A newer version of the mod is
/// available: 0.0.1-unstable.3." <see cref="CheckAsync"/> asks the release source once per launch, off the game
/// thread, unless the settings turn the check off; the newest tag by <see cref="ReleaseVersion"/> order, pre-releases
/// included, is compared with the running version. The notice is queued after what the title screen says, on its
/// first visit once the answer is in, or at once when the answer comes while it is open. A failure says nothing and
/// goes to the log. Safe to use from several threads.</summary>
[FeatureSetting<UpdatesFeature>(UpdatesFeature.UpdateNotice)]
public sealed class UpdateNotice : IScreenObserver
{
    /// <summary>The longest tag taken; a longer one is skipped rather than read out in full.</summary>
    public const int MaxTagLength = 64;

    private readonly string runningVersion;
    private readonly Screen titleScreen;
    private readonly IAnnouncer announcer;
    private readonly Func<bool> enabled;
    private readonly Action<string> log;
    private readonly Lock gate = new();
    private bool checkStarted;
    private bool onTitle;
    private bool said;
    private ReleaseVersion? newer;

    /// <param name="runningVersion">The mod's own version, from its ModConfig.json.</param>
    /// <param name="titleScreen">The title screen, as the menus feature names it.</param>
    /// <param name="announcer">Where the notice is said.</param>
    /// <param name="enabled">Whether the settings allow the check, asked before the request and before speaking.</param>
    /// <param name="log">Where the outcome goes (the Debug log).</param>
    public UpdateNotice(
        string runningVersion, Screen titleScreen, IAnnouncer announcer, Func<bool> enabled, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(runningVersion);
        ArgumentNullException.ThrowIfNull(titleScreen);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(enabled);
        ArgumentNullException.ThrowIfNull(log);
        this.runningVersion = runningVersion;
        this.titleScreen = titleScreen;
        this.announcer = announcer;
        this.enabled = enabled;
        this.log = log;
    }

    /// <summary>What is said when <paramref name="version"/> is newer than the running one.</summary>
    public static string Notice(ReleaseVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return $"A newer version of the mod is available: {version.Text}.";
    }

    /// <summary>Asks <paramref name="source"/> for the releases, the first time only and when the settings allow it.
    /// Never throws.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "The check runs inside the game and fails silently.")]
    public async Task CheckAsync(IReleaseSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock (gate)
        {
            if (checkStarted)
            {
                return;
            }
            checkStarted = true;
        }
        if (!enabled())
        {
            log("update check: off in the settings");
            return;
        }
        if (ReleaseVersion.Parse(runningVersion) is not { } running)
        {
            log($"update check: the mod's version \"{runningVersion}\" is not a semantic version; not checking");
            return;
        }
        IReadOnlyList<string> tags;
        try
        {
            tags = await source.ReleaseTagsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            log($"update check failed: {e.GetType().Name}: {e.Message}");
            return;
        }
        var newest = tags.Where(tag => tag.Length <= MaxTagLength).Select(ReleaseVersion.Parse).OfType<ReleaseVersion>()
            .Aggregate((ReleaseVersion?)null, (best, next) => best is null || next.IsNewerThan(best) ? next : best);
        log($"update check: running {running.Text}, newest release {newest?.Text ?? "none"}");
        if (newest is null || !newest.IsNewerThan(running))
        {
            return;
        }
        bool speak;
        lock (gate)
        {
            newer = newest;
            speak = TakeNotice();
        }
        Speak(speak, newest);
    }

    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen != titleScreen)
        {
            return;
        }
        bool speak;
        ReleaseVersion? version;
        lock (gate)
        {
            onTitle = change.Change != ScreenChange.Left;
            speak = TakeNotice();
            version = newer;
        }
        Speak(speak, version);
    }

    // Under the gate: whether to say the notice now, marking it said.
    private bool TakeNotice()
    {
        if (said || newer is null || !onTitle || !enabled())
        {
            return false;
        }
        said = true;
        return true;
    }

    private void Speak(bool speak, ReleaseVersion? version)
    {
        if (speak && version is not null)
        {
            announcer.Announce(new Announcement(Notice(version), Interrupt: false));
        }
    }
}
