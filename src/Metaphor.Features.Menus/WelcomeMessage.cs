using System.Diagnostics.CodeAnalysis;
using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>The first-launch welcome (#617): the first time the title screen opens with the mod, it says which version
/// is running and the three field keys, queued after the title screen's own speech and the load message before it.
/// Then the settings file's <c>welcomed</c> flag is set, so later launches say nothing. Its own setting turns it off
/// without setting the flag. Safe to use from several threads.</summary>
[FeatureSetting<MenusFeature>(MenusFeature.Welcome)]
public sealed class WelcomeMessage : IScreenObserver
{
    private readonly string text;
    private readonly Screen titleScreen;
    private readonly IAnnouncer announcer;
    private readonly Func<ModSettings> settings;
    private readonly Action markWelcomed;
    private int said;

    /// <param name="modVersion">The mod's own version, from its ModConfig.json.</param>
    /// <param name="titleScreen">The title screen, as the menus feature names it.</param>
    /// <param name="announcer">Where the welcome is said.</param>
    /// <param name="settings">The settings in force, read on the title screen's first visit.</param>
    /// <param name="markWelcomed">Sets the flag in the settings file; a failure only means it is said again next
    /// launch.</param>
    public WelcomeMessage(
        string modVersion, Screen titleScreen, IAnnouncer announcer, Func<ModSettings> settings, Action markWelcomed)
    {
        ArgumentNullException.ThrowIfNull(modVersion);
        ArgumentNullException.ThrowIfNull(titleScreen);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(markWelcomed);
        text = Text(modVersion);
        this.titleScreen = titleScreen;
        this.announcer = announcer;
        this.settings = settings;
        this.markWelcomed = markWelcomed;
    }

    /// <summary>The welcome for <paramref name="modVersion"/>, in the maintainer's words (#617, 2026-11-22).</summary>
    public static string Text(string modVersion) =>
        $"Metaphor accessibility {modVersion} is running. In the field, N says what's near you, L says where you are, G guides you.";

    [SuppressMessage("Design", "CA1031", Justification = "A flag that can't be written must never reach the game.")]
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen != titleScreen || change.Change != ScreenChange.Entered || Volatile.Read(ref said) != 0)
        {
            return;
        }
        var now = settings();
        if (now.Welcomed || !now.IsOn(MenusFeature.Welcome) || Interlocked.Exchange(ref said, 1) != 0)
        {
            return;
        }
        announcer.Announce(new Announcement(text, Interrupt: false));
        try
        {
            markWelcomed();
        }
        catch (Exception)
        {
            // Said again next launch; this launch remembers it said it.
        }
    }
}
