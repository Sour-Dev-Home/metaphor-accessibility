using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;

namespace Metaphor.Features.Battle.Tests;

/// <summary>The HP and MP key (#692, the maintainer's decision of 2026-11-23): in a battle, M says each party member's
/// HP and MP and is hidden from the game; outside one it reaches the game. Made-up party and numbers only.</summary>
[Trait("Category", "Unit")]
public sealed class BattleHpMpTests
{
    private const int KeyM = 0x4D;

    private readonly RecordingAnnouncer announcer = new();
    private readonly Party party = new();
    private readonly Scene scene = new() { On = true };
    private readonly BattleHpMp key;

    public BattleHpMpTests()
    {
        var hits = new HitLines(new SilentAnnouncer(), new ManualTime(), unit => unit == 3 ? "Rowan" : null);
        key = new BattleHpMp(party, hits, scene, announcer, () => BattleBindings.Default);
    }

    [Fact]
    public void TheDefaultIsMClearOfTheGamesAndTheModsBattleKeys()
    {
        // The game's battle keys read aloud on 2026-11-22 (Q, H, F, E, Tab, N, T, 1 to 5), and the mod's H, K, J, L, R.
        int[] taken = [0x51, 0x48, 0x46, 0x45, 0x09, 0x4E, 0x54, 0x31, 0x32, 0x33, 0x34, 0x35, 0x4B, 0x4A, 0x4C, 0x52];

        Assert.Equal(KeyM, BattleBindings.Default.HpMpKey);
        Assert.DoesNotContain(BattleBindings.Default.HpMpKey, taken);
    }

    [Fact]
    public void InABattleMSaysEachMembersHpAndMpAndIsHidden()
    {
        party.Now = [new PartyMember(1, 7, 50, null, 12), new PartyMember(0, 3, 89, 120, 30)];

        var handed = key.Keys([KeyM]);

        Assert.Empty(handed);
        Assert.Equal("Rowan 89 of 120 HP, 30 MP. Second ally, 50 HP, 12 MP", announcer.Said.Single().Text);
        Assert.True(announcer.Said.Single().Interrupt);
    }

    [Fact]
    public void AnUnreadMpIsLeftOut()
    {
        party.Now = [new PartyMember(0, 3, 89, null)];

        key.Keys([KeyM]);

        Assert.Equal("Rowan 89 HP", announcer.Said.Single().Text);
    }

    [Fact]
    public void OutsideABattleMReachesTheGame()
    {
        scene.On = false;
        party.Now = [new PartyMember(0, 3, 89, 120, 30)];

        Assert.Equal([KeyM], key.Keys([KeyM]));
        Assert.Empty(announcer.Said);
    }

    private sealed class Party : IBattleParty
    {
        public IReadOnlyList<PartyMember> Now { get; set; } = [];

        public IReadOnlyList<PartyMember> Members() => Now;
    }

    private sealed class Scene : IBattleScene
    {
        public bool On { get; set; }

        public bool InBattle() => On;
    }
}
