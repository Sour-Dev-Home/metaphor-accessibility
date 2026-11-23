using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The first-launch welcome (#617): said once, queued after the title screen's own speech, on the first launch
/// only, and remembered by the settings file's flag.</summary>
[Trait("Category", "Unit")]
public sealed class WelcomeMessageTests
{
    private const string Expected =
        "Metaphor accessibility 0.1.0 is running. In the field, N says what's near you, L says where you are, " +
        "G guides you.";

    private static readonly Screen Other = new("Main menu", "RootMenuChoice@ope@ui@app");

    private readonly RecordingAnnouncer announcer = new();
    private ModSettings settings = ModSettings.Defaults;
    private int marked;

    private WelcomeMessage Welcome() => new("0.1.0", TitleMenuFocus.Screen, announcer, () => settings, () => marked++);

    private static ScreenEvent On(Screen screen, ScreenChange change = ScreenChange.Entered) =>
        new(change, screen, screen.ScreenClass, Index: 0);

    [Fact]
    public void TheFirstLaunchSaysTheWelcomeOnTheTitleScreenAfterWhatItSays()
    {
        Welcome().Observe(On(TitleMenuFocus.Screen));

        Assert.Equal([new Announcement(Expected, Interrupt: false)], announcer.Said);
        Assert.Equal(1, marked);
    }

    [Fact]
    public void ItIsSaidOnceALaunchHoweverOftenTheTitleScreenComesBack()
    {
        var welcome = Welcome();

        welcome.Observe(On(TitleMenuFocus.Screen));
        welcome.Observe(On(TitleMenuFocus.Screen, ScreenChange.Left));
        welcome.Observe(On(TitleMenuFocus.Screen));

        Assert.Single(announcer.Said);
        Assert.Equal(1, marked);
    }

    [Fact]
    public void ALaterLaunchWhoseFileHasTheFlagSaysNothing()
    {
        settings = ModSettings.Parse("""{ "welcomed": true }""", SettingCatalogue.Of<MenusFeature>(), _ => { });

        Welcome().Observe(On(TitleMenuFocus.Screen));

        Assert.Empty(announcer.Said);
        Assert.Equal(0, marked);
    }

    [Fact]
    public void OtherScreensAndFocusMovesSayNothing()
    {
        var welcome = Welcome();

        welcome.Observe(On(Other));
        welcome.Observe(On(TitleMenuFocus.Screen, ScreenChange.FocusMoved));
        welcome.Observe(On(TitleMenuFocus.Screen, ScreenChange.Left));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void TurnedOffItSaysNothingAndLeavesTheFlagForWhenItIsBackOn()
    {
        settings = ModSettings.Parse(
            """{ "speech": { "welcome": false } }""", SettingCatalogue.Of<MenusFeature>(), _ => { });

        Welcome().Observe(On(TitleMenuFocus.Screen));

        Assert.Empty(announcer.Said);
        Assert.Equal(0, marked);
    }

    [Fact]
    public void AFlagThatCannotBeWrittenStillSaysItOnlyOnceThisLaunch()
    {
        var welcome = new WelcomeMessage(
            "0.1.0", TitleMenuFocus.Screen, announcer, () => settings, () => throw new IOException("locked"));

        welcome.Observe(On(TitleMenuFocus.Screen));
        welcome.Observe(On(TitleMenuFocus.Screen));

        Assert.Single(announcer.Said);
    }
}
