using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus.Tests;

/// <summary>Opt-in controller speech stop (#1036): normal pad presses interrupt ongoing speech before downstream
/// controls speak what the press brings up, triggers and held chord modifiers do not stop speech, and the off state
/// changes nothing.</summary>
[Trait("Category", "Unit")]
public sealed class PadSpeechStopTests
{
    private const int PadA = 0x4000;
    private const int PadX = 0x8000;
    private const int PadDown = 0x0040;
    private const int PadLb = 0x0400;
    private const int PadRb = 0x0800;
    private const int PadLt = 0x0100;
    private const int PadRt = 0x0200;
    private const int PadLs = 0x0002;

    [Fact]
    public void NormalButtonPressInterruptsOngoingSpeechWhileDownstreamAnnouncementStillPlays()
    {
        var events = new List<string>();
        var inner = new CallbackAnnouncer(a => events.Add($"say:{a.Text}"));
        var stoppable = new StoppableAnnouncer(inner, () => events.Add("stop"));
        var settings = new SettingsReloader(new SilentAnnouncer(), SettingCatalogue.Of<MenusFeature>(), _ => { });
        settings.Load("""{ "speech": { "padSpeechStop": true } }""");

        var padStop = settings.Input(MenusFeature.PadSpeechStop, new PadSpeechStop(stoppable.StopAll));
        var downstream = new DownstreamControl(PadX, () => stoppable.Announce(new Announcement("Repeated line", Interrupt: true)));
        var chain = new ChainedControls(padStop, downstream);

        stoppable.Announce(new Announcement("Long earlier line", Interrupt: true));
        events.Clear();

        var handed = chain.Pad(0, PadX, made: true);

        // Stopped first, then downstream spoke, and holding X on the next frame does not stop the new speech.
        Assert.Equal(0, handed);
        Assert.Equal(["stop", "say:Repeated line"], events);

        events.Clear();
        chain.Pad(0, PadX, made: true);
        Assert.Empty(events);
    }

    [Theory]
    [InlineData(PadLt)]
    [InlineData(PadRt)]
    [InlineData(PadLt | PadRt)]
    [InlineData(PadLb)]
    [InlineData(PadRb)]
    [InlineData(PadLb | PadRb)]
    [InlineData(PadLs)]
    public void TriggersAndHeldChordModifiersDoNotStopSpeech(int exemptPress)
    {
        var stops = 0;
        var padStop = new PadSpeechStop(() => stops++, () => ShoulderTaps.Shoulders | PadLs);

        Assert.Equal(exemptPress, padStop.Pad(0, exemptPress, made: true));
        Assert.Equal(0, stops);

        // Pressing a non-exempt button while a chord modifier or trigger is held still stops speech once.
        Assert.Equal(exemptPress | PadDown, padStop.Pad(0, exemptPress | PadDown, made: true));
        Assert.Equal(1, stops);
    }

    [Fact]
    public void WhenOffOrReadNotMadeItNeverStopsSpeech()
    {
        var stops = 0;
        var settings = new SettingsReloader(new SilentAnnouncer(), SettingCatalogue.Of<MenusFeature>(), _ => { });
        settings.Load("{}");
        var gated = settings.Input(MenusFeature.PadSpeechStop, new PadSpeechStop(() => stops++));

        Assert.False(settings.Current.IsOn(MenusFeature.PadSpeechStop));
        Assert.Equal(PadA, gated.Pad(0, PadA, made: true));
        Assert.Equal(0, stops);

        var active = new PadSpeechStop(() => stops++);
        Assert.Equal(PadA, active.Pad(0, PadA, made: false));
        Assert.Equal(0, stops);
    }

    [Fact]
    public void ClosingSettingsMenuWithBKeepsHeldBFromStoppingClosingSpeech()
    {
        const int lt = 0x0100;
        const int rt = 0x0200;
        const int view = 0x0001;
        const int padB = 0x2000;
        var stops = 0;
        var menuSpeech = new RecordingAnnouncer();
        var settings = new SettingsReloader(new SilentAnnouncer(), SettingCatalogue.Of<MenusFeature>(), _ => { });
        var menu = new SettingsMenu(
            () => PlayerSettings.GeneralSettings, () => settings.Current, (_, _) => true, menuSpeech,
            () => SettingsMenuBindings.Default, () => MenuBindings.Default, () => true, () => true);
        var chain = new ChainedControls(menu, new PadSpeechStop(() => stops++));

        chain.Pad(0, lt | rt, made: true);
        chain.Pad(0, lt | rt | view, made: true);
        chain.Pad(0, 0, made: true);
        stops = 0;

        // Press B to close, then keep holding B on the next frame: B stays kept until released.
        Assert.Equal(0, chain.Pad(0, padB, made: true));
        Assert.Equal(0, chain.Pad(0, padB, made: true));
        Assert.Equal(0, stops);
        Assert.Equal("Settings closed.", menuSpeech.Said[^1].Text);
    }

    private sealed class CallbackAnnouncer(Action<Announcement> onAnnounce) : IAnnouncer
    {
        public void Announce(Announcement announcement) => onAnnounce(announcement);
    }

    private sealed class DownstreamControl(int trigger, Action onPress) : IInputControls
    {
        private int before;

        public int Pad(int pad, int buttons, bool made)
        {
            var down = buttons & ~before;
            before = buttons;
            if ((down & trigger) != 0)
            {
                onPress();
                return buttons & ~trigger;
            }
            return buttons;
        }

        public IReadOnlyList<int> Keys(IReadOnlyList<int> keys) => keys;
    }
}
