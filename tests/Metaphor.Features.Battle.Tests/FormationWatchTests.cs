using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;

namespace Metaphor.Features.Battle.Tests;

/// <summary>Formation in battle (#259): a party member's Formation bit flipping between reads is said once, as "row
/// changed", since which value is the back row isn't known yet. Made-up flags only.</summary>
[Trait("Category", "Unit")]
public sealed class FormationWatchTests
{
    private const int Back = PartyMember.BackRowBit;

    private readonly RecordingAnnouncer announcer = new();
    private readonly ManualTime time = new();
    private readonly Party party = new();
    private readonly Scene scene = new() { On = true };
    private readonly List<string> logged = [];
    private readonly FormationWatch watch;

    public FormationWatchTests() => watch = new FormationWatch(
        party, scene, announcer, time, (slot, unit, rest) => $"Member {slot} of {unit}, {rest}")
    {
        Log = logged.Add,
    };

    private List<string> Said() => [.. announcer.Said.Select(said => said.Text)];

    private static PartyMember Member(int slot, int unit, int flags) => new(slot, unit, 100, null, 10, flags);

    // Input reads, one each 50 ms frame, over the given time.
    private void Frames(long milliseconds, FormationWatch? other = null)
    {
        var on = other ?? watch;
        for (var at = 0L; at < milliseconds; at += 50)
        {
            time.Advance(50);
            on.Keys([]);
            on.Pad(0, 0, made: true);
        }
    }

    [Fact]
    public void AFlippedBitIsSaidOnceAndTheFirstReadSaysNothing()
    {
        party.Now = [Member(0, 1, 0x10), Member(1, 5, Back | 0x10)];
        Frames(500);
        party.Now = [Member(0, 1, Back | 0x10), Member(1, 5, Back | 0x10)];
        Frames(500);

        Assert.Equal(["Member 0 of 1, row changed"], Said());
        Assert.True(Assert.Single(announcer.Said).Interrupt);
        Assert.Contains("slot=0 unit=1 bit False -> True", logged);
    }

    [Fact]
    public void ASwapSaysWhoCameInForWhomAndOtherFlagsDontCount()
    {
        var named = new FormationWatch(
            party, scene, announcer, time, (slot, unit, rest) => $"Member {slot} of {unit}, {rest}")
        {
            Log = logged.Add,
            NameOf = unit => unit == 5 ? "Hulkenberg" : null,
        };
        party.Now = [Member(0, 1, 0), Member(1, 5, 0)];
        Frames(500, named);
        party.Now = [Member(0, 1, 0x1), Member(1, 3, Back)];
        Frames(500, named);

        Assert.Equal(["Member 1 of 3, in for Hulkenberg"], Said());
        Assert.Contains("slot=1 unit 5 -> 3 bit=True", logged);
    }

    [Fact]
    public void ASwapWithNoNameForTheMemberTakenOutSaysSwappedIn()
    {
        party.Now = [Member(0, 1, 0)];
        Frames(500);
        party.Now = [Member(0, 4, 0)];
        Frames(500);

        Assert.Equal(["Member 0 of 4, swapped in"], Said());
    }

    [Fact]
    public void OutsideABattleNothingIsReadAndANewBattleStartsAfresh()
    {
        scene.On = false;
        party.Now = [Member(0, 1, 0)];
        Frames(1_500);
        Assert.Equal(0, party.Reads);

        scene.On = true;
        Frames(1_500);
        scene.On = false;
        Frames(1_500);
        // Between battles the row changed, as in the camp menu: the next battle's first read says nothing.
        party.Now = [Member(0, 1, Back)];
        scene.On = true;
        Frames(1_500);

        Assert.Empty(Said());
    }

    [Fact]
    public void ThePartyIsReadAtMostEveryReadInterval()
    {
        party.Now = [Member(0, 1, 0)];
        Frames(1_000);

        Assert.InRange(party.Reads, 1, (int)(1_000 / FormationWatch.ReadEveryMilliseconds) + 1);
    }

    private sealed class Party : IBattleParty
    {
        public IReadOnlyList<PartyMember> Now { get; set; } = [];

        public int Reads { get; private set; }

        public IReadOnlyList<PartyMember> Members()
        {
            Reads++;
            return Now;
        }
    }

    private sealed class Scene : IBattleScene
    {
        public bool On { get; set; }

        public bool InBattle() => On;
    }
}
