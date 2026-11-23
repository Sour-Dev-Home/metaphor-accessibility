using Metaphor.Core;
using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The silent screen list (#597): each screen entered or focus moved that the mod said nothing for goes to the
/// report log, up to a limit. The list hears each screen event after every speaking feature, so speech for the event
/// has already passed through it.</summary>
[Trait("Category", "Unit")]
public sealed class SilentScreensTests
{
    private const string Items = "ItemMenuList@ope@ui@app";

    private readonly RecordingAnnouncer spoken = new();
    private readonly List<ScreenEvent> silent = [];
    private readonly List<int> full = [];
    private ModSettings settings = ModSettings.Defaults;

    private SilentScreens List(int limit = SilentScreens.MaxEntries) =>
        new(spoken, new Report(silent, full), () => settings, limit);

    private static ScreenEvent Moved(int index) => new(ScreenChange.FocusMoved, null, Items, index, Count: 9);

    [Fact]
    public void AFocusMoveWithNoSpeechIsListed()
    {
        var list = List();

        list.Observe(Moved(3));

        Assert.Equal([Moved(3)], silent);
    }

    [Fact]
    public void AFocusMoveThatSpokeIsNotListedAndSpeechStillReachesTheScreenReader()
    {
        var list = List();

        list.Announce(new Announcement("Item, 4 of 9", Interrupt: true));
        list.Observe(Moved(3));

        Assert.Empty(silent);
        Assert.Single(spoken.Said);
    }

    [Fact]
    public void SpeechCountsOnlyForTheEventItCameWith()
    {
        var list = List();

        list.Announce(new Announcement("Item, 4 of 9", Interrupt: true));
        list.Observe(Moved(3));
        list.Observe(Moved(4));

        Assert.Equal([Moved(4)], silent);
    }

    [Fact]
    public void ASilentScreenEntryIsListedAndLeavingIsNot()
    {
        var list = List();
        var entered = new ScreenEvent(ScreenChange.Entered, null, Items, 0);

        list.Observe(entered);
        list.Observe(new ScreenEvent(ScreenChange.Left, null, Items, 0));

        Assert.Equal([entered], silent);
    }

    [Fact]
    public void TheListStopsAtItsLimitAndSaysSoOnce()
    {
        var list = List(limit: 2);

        for (var i = 0; i < 5; i++)
        {
            list.Observe(Moved(i));
        }

        Assert.Equal([Moved(0), Moved(1)], silent);
        Assert.Equal([2], full);
    }

    [Fact]
    public void TurnedOffNothingIsListed()
    {
        settings = ModSettings.Parse(
            """{ "report": { "silentScreens": false } }""", SettingCatalogue.Of<MenusFeature>(), _ => { });

        List().Observe(Moved(3));

        Assert.Empty(silent);
    }

    private sealed class Report(List<ScreenEvent> silent, List<int> full) : ISessionReport
    {
        public void Mark(int number)
        {
        }

        public void Silent(ScreenEvent change) => silent.Add(change);

        public void SilentListFull(int limit) => full.Add(limit);
    }
}
