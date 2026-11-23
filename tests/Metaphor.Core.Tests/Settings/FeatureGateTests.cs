using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Core.Tests.Settings;

/// <summary>The gates the wiring puts in front of each feature (#661): they read the feature's setting at each
/// announcement and each input read, so a changed file applies without a restart.</summary>
[Trait("Category", "Unit")]
public sealed class FeatureGateTests
{
    private readonly RecordingAnnouncer announcer = new();
    private readonly RecordingAnnouncer underneath = new();
    private readonly SettingsReloader settings;

    public FeatureGateTests()
    {
        settings = new SettingsReloader(announcer, SampleSettings.Catalogue, _ => { });
        settings.Load("{}");
    }

    [Fact]
    public void SpeechPassesWhileTheFeatureIsOnAndStopsAtOnceWhenItIsTurnedOff()
    {
        var menus = settings.Speech(SampleFeature.Menus, underneath);

        menus.Announce(new Announcement("Items", Interrupt: true));
        settings.Reload("""{ "speech": { "menus": false } }""");
        menus.Announce(new Announcement("Skills", Interrupt: true));
        settings.Reload("{}");
        menus.Announce(new Announcement("Equip", Interrupt: true));

        Assert.Equal(["Items", "Equip"], underneath.Said.Select(said => said.Text));
    }

    [Fact]
    public void AnotherFeatureTurnedOffLeavesThisOneSpeaking()
    {
        var menus = settings.Speech(SampleFeature.Menus, underneath);
        settings.Reload("""{ "speech": { "dialogue": false } }""");

        menus.Announce(new Announcement("Items", Interrupt: true));

        Assert.Single(underneath.Said);
    }

    [Fact]
    public void InputReachesTheGameUntouchedWhileTheFeatureIsOff()
    {
        var controls = new HidingControls();
        var gated = settings.Input(SampleFeature.Dialogue, controls);
        settings.Reload("""{ "speech": { "dialogue": false } }""");

        Assert.Equal(0xFF, gated.Pad(0, 0xFF, made: true));
        Assert.Equal([1, 2], gated.Keys([1, 2]));
        gated.WindowRead(5);

        Assert.Equal(0, controls.Pads);
        Assert.Equal(0, controls.KeyReads);
        Assert.Equal([5L], controls.Windows);
    }

    [Fact]
    public void InputGoesToTheControlsWhileTheFeatureIsOn()
    {
        var gated = settings.Input(SampleFeature.Dialogue, new HidingControls());

        Assert.Equal(0, gated.Pad(0, 0xFF, made: true));
        Assert.Empty(gated.Keys([1, 2]));
    }

    [Fact]
    public void ALiveValueIsWorkedOutOncePerFileAndAgainAfterAChange()
    {
        var made = 0;
        var key = settings.Live(now => { made++; return now.Binding("dialogue", "repeatKey", 0x52); });

        Assert.Equal(0x52, key());
        Assert.Equal(0x52, key());
        settings.Reload("""{ "keys": { "dialogue": { "repeatKey": "Q" } } }""");

        Assert.Equal(0x51, key());
        Assert.Equal(2, made);
    }

    [Theory]
    [InlineData("""{ "speech": { "dialogue": false } }""", "Dialogue: off.")]
    [InlineData("""{ "field": { "nearby": false } }""", "Nearby list: off.")]
    [InlineData("""{ "sounds": { "allyHit": { "on": false } } }""", "Ally hit sound: off.")]
    [InlineData("""{ "sounds": { "enemyHit": { "volume": 40 } } }""", "Enemy hit sound volume: 40.")]
    [InlineData("""{ "keys": { "field": { "nearbyKey": "M" } } }""", "Keys changed.")]
    public void AChangedToggleIsSaid(string json, string said)
    {
        settings.Reload(json);

        Assert.Equal([new Announcement(said, Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void TurningAFeatureBackOnIsSaid()
    {
        settings.Load("""{ "speech": { "menus": false } }""");

        settings.Reload("{}");

        Assert.Equal([new Announcement("Menus: on.", Interrupt: true)], announcer.Said);
    }

    /// <summary>Controls that hide every bound input, and count what they're handed.</summary>
    private sealed class HidingControls : IWindowInputControls
    {
        public int Pads { get; private set; }

        public int KeyReads { get; private set; }

        public List<long?> Windows { get; } = [];

        public int Pad(int pad, int buttons, bool made)
        {
            Pads++;
            return 0;
        }

        public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
        {
            KeyReads++;
            return [];
        }

        public void WindowRead(long? currentMessage) => Windows.Add(currentMessage);
    }
}
