using Metaphor.Core.Announcements;
using Metaphor.Core.Cues;
using Metaphor.Core.Features;
using Metaphor.Core.Settings;
using Metaphor.Core.Tests.Settings;

namespace Metaphor.Core.Tests.Features;

/// <summary>The speech factory (#723): each output it hands a feature says and plays nothing while the feature is off,
/// read at each line and cue, and a held tone can always be silenced.</summary>
[Trait("Category", "Unit")]
public sealed class FeatureOutputsTests
{
    private readonly RecordingAnnouncer now = new();
    private readonly RecordingAnnouncer reader = new();
    private readonly RecordingCues cues = new();
    private readonly SettingsReloader settings;
    private readonly FeatureOutputs outputs;

    public FeatureOutputsTests()
    {
        settings = new SettingsReloader(new SilentAnnouncer(), SampleSettings.Catalogue, _ => { });
        settings.Load("{}");
        outputs = new FeatureOutputs(settings, now, reader, cues);
    }

    private static Announcement Line(string text) => new(text, Interrupt: false);

    [Fact]
    public void EachOutputReachesItsRouteWhileTheFeatureIsOn()
    {
        var menus = outputs.For(SampleFeature.Menus);

        menus.Now.Announce(Line("now"));
        menus.Reader.Announce(Line("reader"));
        menus.Cues.Play(Cue.AllyHit);
        menus.Cues.Play(Cue.AllyHit, 0.5);
        menus.Cues.Hold(ToneChannel.Guidance, new HeldTone(440, 0, 100));

        Assert.True(menus.IsOn);
        Assert.Equal(["now"], now.Said.Select(said => said.Text));
        Assert.Equal(["reader"], reader.Said.Select(said => said.Text));
        Assert.Equal(["play", "play pan", "hold"], cues.Calls);
    }

    [Fact]
    public void TurnedOffNothingIsSaidOrPlayedButSilenceStillPasses()
    {
        var menus = outputs.For(SampleFeature.Menus);
        var through = new RecordingAnnouncer();
        var routed = menus.Through(through);
        settings.Reload("""{ "speech": { "menus": false } }""");

        menus.Now.Announce(Line("now"));
        menus.Reader.Announce(Line("reader"));
        routed.Announce(Line("routed"));
        menus.Cues.Play(Cue.AllyHit);
        menus.Cues.Play(Cue.AllyHit, 0.5);
        menus.Cues.Hold(ToneChannel.Guidance, new HeldTone(440, 0, 100));
        menus.Cues.Silence(ToneChannel.Guidance);

        Assert.False(menus.IsOn);
        Assert.Empty(now.Said);
        Assert.Empty(reader.Said);
        Assert.Empty(through.Said);
        Assert.Equal(["silence"], cues.Calls);
    }

    [Fact]
    public void TheSettingIsReadAtEachLineSoAChangedFileAppliesAtOnce()
    {
        var menus = outputs.For(SampleFeature.Menus);

        menus.Now.Announce(Line("Items"));
        settings.Reload("""{ "speech": { "menus": false } }""");
        menus.Now.Announce(Line("Skills"));
        settings.Reload("{}");
        menus.Now.Announce(Line("Equip"));

        Assert.Equal(["Items", "Equip"], now.Said.Select(said => said.Text));
    }

    [Fact]
    public void OutputsForSeveralFeaturesStayOnWhileAnyIsOn()
    {
        var both = outputs.For(SampleFeature.Menus, SampleFeature.Dialogue);

        settings.Reload("""{ "speech": { "menus": false } }""");
        both.Now.Announce(Line("one on"));
        settings.Reload("""{ "speech": { "menus": false, "dialogue": false } }""");
        both.Now.Announce(Line("both off"));

        Assert.Equal(["one on"], now.Said.Select(said => said.Text));
    }

    [Fact]
    public void AnotherFeatureTurnedOffLeavesThisOneSpeaking()
    {
        var menus = outputs.For(SampleFeature.Menus);
        settings.Reload("""{ "speech": { "dialogue": false } }""");

        menus.Reader.Announce(Line("Items"));

        Assert.Single(reader.Said);
    }

    [Fact]
    public void OutputsNeedAFeature() => Assert.Throws<ArgumentException>(() => outputs.For());

    private sealed class RecordingCues : ICuePlayer
    {
        public List<string> Calls { get; } = [];

        public void Play(Cue cue) => Calls.Add("play");

        public void Play(Cue cue, double pan) => Calls.Add("play pan");

        public void Hold(ToneChannel channel, HeldTone tone) => Calls.Add("hold");

        public void Silence(ToneChannel channel) => Calls.Add("silence");
    }
}
