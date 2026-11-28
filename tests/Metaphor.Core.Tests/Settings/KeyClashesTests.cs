using Metaphor.Core.Announcements;
using Metaphor.Core.Settings;

namespace Metaphor.Core.Tests.Settings;

/// <summary>Catching two actions bound to the same key or pad chord (#755): at start and on reload, a clash is spoken
/// once and logged once naming both actions and the binding, the earlier action keeps the binding, and the later one is
/// left unbound until fixed.</summary>
[Trait("Category", "Unit")]
public sealed class KeyClashesTests
{
    private static readonly BoundKey Hold = new("field", "holdButtons", 0x0400);
    private static readonly BoundKey NextKey = new("field", "nextKey", 0x4B);
    private static readonly BoundKey GuideKey = new("field", "guideKey", 0x47);
    private static readonly BoundKey NextButton = new("field", "nextButton", 0x0040);
    private static readonly BoundKey GuideButton = new("field", "guideButton", 0x0020);
    private static readonly BoundKey BattleKey = new("battle", "statusKey", 0x42);

    private static readonly IReadOnlyList<KeyEntry> SampleKeys =
    [
        new(KeyPlaces.Field, SampleFeature.Menus, new("Next nearby", NextKey, NextButton, Hold)),
        new(KeyPlaces.Field, SampleFeature.Dialogue, new("Guidance", GuideKey, GuideButton, Hold)),
        new(KeyPlaces.Battle, SampleFeature.Menus, new("Battle status", BattleKey)),
    ];

    private readonly RecordingAnnouncer announcer = new();
    private readonly List<string> logged = [];

    private SettingsReloader Reloader(IReadOnlyList<KeyEntry>? keys = null) =>
        new(announcer, SampleSettings.Catalogue, logged.Add, keys: keys ?? SampleKeys);

    [Fact]
    public void AKeyClashAtStartSpeaksOnceLogsAndLeavesTheLaterActionUnbound()
    {
        var reloader = Reloader();

        reloader.Load("""{ "keys": { "field": { "nextKey": "F", "guideKey": "F" } } }""");

        Assert.Equal(0x46, NextKey.From(reloader.Current));
        Assert.Equal(0, GuideKey.From(reloader.Current));
        var said = Assert.Single(announcer.Said);
        Assert.Equal("Next nearby and Guidance are both bound to F. Guidance is unbound.", said.Text);
        Assert.False(said.Interrupt);
        var line = Assert.Single(logged);
        Assert.Contains("Next nearby", line, StringComparison.Ordinal);
        Assert.Contains("Guidance", line, StringComparison.Ordinal);
        Assert.Contains("bound to F", line, StringComparison.Ordinal);
    }

    [Fact]
    public void APadChordClashOnReloadSpeaksOnceLogsAndKeepsTheEarlierChordWorking()
    {
        var reloader = Reloader();
        reloader.Load("{}");

        reloader.Reload("""{ "keys": { "field": { "guideButton": "DpadDown" } } }""");

        Assert.Equal(0x0400, Hold.From(reloader.Current));
        Assert.Equal(0x0040, NextButton.From(reloader.Current));
        Assert.Equal(0, GuideButton.From(reloader.Current));
        Assert.Equal(
            [
                new Announcement("Keys changed.", Interrupt: true),
                new Announcement("Next nearby and Guidance are both bound to LB with D-pad down. Guidance is unbound.", Interrupt: false),
            ],
            announcer.Said);
        var line = Assert.Single(logged);
        Assert.Contains("LB with D-pad down", line, StringComparison.Ordinal);

        // A duplicate watcher event on the same file does not repeat the clash.
        reloader.Reload("""{ "keys": { "field": { "guideButton": "DpadDown" } } }""");
        Assert.Equal(2, announcer.Said.Count);
        Assert.Single(logged);

        // Fixing the clash restores the binding and announces Keys changed with no clash line.
        reloader.Reload("{}");
        Assert.Equal(0x0020, GuideButton.From(reloader.Current));
        Assert.Equal("Keys changed.", announcer.Said[^1].Text);
    }

    [Fact]
    public void NoClashInDefaultsDifferentScopesOrTurnedOffFeaturesSaysAndLogsNothing()
    {
        var reloader = Reloader();

        reloader.Load("""{ "keys": { "battle": { "statusKey": "K" } } }""");
        Assert.Equal(0x4B, NextKey.From(reloader.Current));
        Assert.Equal(0x4B, BattleKey.From(reloader.Current));
        Assert.Empty(announcer.Said);
        Assert.Empty(logged);

        reloader.Reload("""{ "speech": { "dialogue": false }, "keys": { "field": { "guideKey": "K", "battle": "K" } } }""");
        Assert.Equal(0x4B, NextKey.From(reloader.Current));
        Assert.DoesNotContain(announcer.Said, item => item.Text.Contains("both bound", StringComparison.Ordinal));
    }

    [Fact]
    public void KeysWatchedAfterLoadAreCheckedOnceWhenLiveBindingsReadThem()
    {
        var reloader = new SettingsReloader(announcer, SampleSettings.Catalogue, logged.Add);
        reloader.Load("""{ "keys": { "field": { "guideKey": "K" } } }""");
        var list = new List<KeyEntry>();
        reloader.WatchKeys(list);
        list.AddRange(SampleKeys);
        var liveGuide = reloader.Live(GuideKey.From);
        var liveNext = reloader.Live(NextKey.From);

        Assert.Equal(0, liveGuide());
        Assert.Equal(0x4B, liveNext());
        Assert.Equal(0, liveGuide());
        Assert.Single(announcer.Said);
        Assert.Single(logged);
    }
}
