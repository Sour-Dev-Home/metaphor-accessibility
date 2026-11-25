using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Input;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle.Tests;

/// <summary>The status key (#631): in a battle, B pressed alone, or L3 held alone (#925), says the party's HP, then the
/// enemies this battle has hit, and is hidden from the game; an L3 tap reaches the game (Retry), and anything else, and
/// the key outside a battle, reaches it. The pad reads go through an input frame with the shoulder taps, as in the
/// game. Made-up party and numbers only.</summary>
[Trait("Category", "Unit")]
public sealed class BattleStatusTests
{
    private const int A = 0x4000;
    private const int L3 = BattleStatus.LeftThumb;
    private const int Hold = BattleStatus.HoldMilliseconds;

    private readonly RecordingAnnouncer announcer = new();
    private readonly ManualTime time = new();
    private readonly Party party = new();
    private readonly Scene scene = new() { On = true };
    private readonly HitLines hits;
    private readonly BattleStatus status;
    private readonly InputFrame frame;

    public BattleStatusTests()
    {
        hits = new HitLines(new SilentAnnouncer(), time, unit => unit == 3 ? "Rowan" : null);
        status = new BattleStatus(party, hits, scene, announcer, () => BattleBindings.Default, time);
        var taps = new ShoulderTaps([status.HeldButtons], () => time.GetUtcNow().ToUnixTimeMilliseconds());
        frame = new InputFrame(new NoGame(), new MenuScreens([]), [status], null, null, null, taps: taps);
    }

    private int Game(int buttons, int pad = 0) => frame.Pad(pad, buttons, made: true).Buttons;

    private List<string> Said() => [.. announcer.Said.Select(said => said.Text)];

    [Fact]
    public void OutsideABattleL3AndBReachTheGameAtOnceAndSayNothing()
    {
        scene.On = false;
        party.Now = [new PartyMember(0, 3, 89, 120)];

        Assert.Equal(L3, Game(L3));
        time.Advance(Hold);
        Assert.Equal(L3, Game(L3));
        Assert.Equal([BattleStatus.KeyB], status.Keys([BattleStatus.KeyB]));

        Assert.Empty(announcer.Said);
    }

    // #925: the game's Retry is on L3, so a tap reaches it, as the shoulders' taps do (#811).
    [Fact]
    public void InABattleAnL3TapReachesTheGameOnReleaseAndSaysNothing()
    {
        Assert.Equal(0, Game(L3));
        time.Advance(16);
        Assert.Equal(0, Game(L3));
        time.Advance(16);

        Assert.Equal(L3, Game(0));
        Assert.Empty(Said());
    }

    [Fact]
    public void InABattleL3HeldAloneSaysEachMemberThenTheEnemiesAndNeverReachesTheGame()
    {
        party.Now = [new PartyMember(0, 3, 89, 120), new PartyMember(1, 7, 40, null)];
        hits.Observe(new BattleHit(0xa0, -5, 55, 1, BattleSide.Enemy));

        Assert.Equal(0, Game(L3));
        time.Advance(Hold - 1);
        Assert.Equal(0, Game(L3));
        Assert.Empty(Said());
        time.Advance(1);
        Assert.Equal(0, Game(L3));
        Assert.Equal(["Rowan 89 of 120, Second ally, 40 HP. Enemy, 55"], Said());

        time.Advance(16);
        var released = new[] { Game(0), Game(0), Game(0) };
        time.Advance(200);
        Assert.Equal([0, 0, 0, 0], [.. released, Game(0)]);
        Assert.Single(announcer.Said);
    }

    [Fact]
    public void AnL3LetGoJustBeforeTheHoldTimeIsATap()
    {
        Assert.Equal(0, Game(L3));
        time.Advance(Hold - 1);

        Assert.Equal(L3, Game(0));
        Assert.Empty(Said());
    }

    // #941: the hold time is a setting; a changed value moves the tap/hold boundary.
    [Fact]
    public void ALongerHoldTimeMakesTheOldHoldATap()
    {
        var holdTime = Hold;
        var timed = new BattleStatus(party, hits, scene, announcer, () => BattleBindings.Default, time)
        {
            HoldTime = () => holdTime,
        };
        var taps = new ShoulderTaps([timed.HeldButtons], () => time.GetUtcNow().ToUnixTimeMilliseconds());
        var timedFrame = new InputFrame(new NoGame(), new MenuScreens([]), [timed], null, null, null, taps: taps);
        int Press(int buttons) => timedFrame.Pad(0, buttons, made: true).Buttons;
        holdTime = 600;

        Press(L3);
        time.Advance(Hold);
        Press(L3);
        Assert.Empty(Said());
        time.Advance(600 - Hold);
        Press(L3);

        Assert.Single(announcer.Said);
    }

    [Fact]
    public void TheHoldTimeLimitDefaultsToTheOldFixedValueAndStaysInRange()
    {
        Assert.Equal(Hold, BattleStatus.HoldLimit.From(ModSettings.Defaults));
        Assert.Equal(("battle", 300), (BattleStatus.HoldLimit.Group, BattleStatus.HoldLimit.Default));
        Assert.True(BattleStatus.HoldLimit.Min > 0 && BattleStatus.HoldLimit.Max >= 1000);
    }

    [Fact]
    public void AfterAHoldTheNextTapReachesTheGame()
    {
        Game(L3);
        time.Advance(Hold);
        Game(L3);
        Game(0);
        time.Advance(500);
        Game(0);

        Game(L3);
        time.Advance(16);
        Assert.Equal(L3, Game(0));
        Assert.Single(announcer.Said);
    }

    [Fact]
    public void APressKeepsWhereItStartedUntilReleased()
    {
        scene.On = false;
        Assert.Equal(L3, Game(L3));
        scene.On = true;
        time.Advance(Hold);

        Assert.Equal(L3, Game(L3));
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ABattleSceneIsTheFieldsCommandBattleScene()
    {
        Assert.True(BattleScene.IsBattle("fldScene_CmdBattle@fld@app"));
        Assert.False(BattleScene.IsBattle("fldScene_Idle@fld@app"));
        Assert.False(BattleScene.IsBattle("fldScene_LargeMap@fld@app"));
        Assert.False(BattleScene.IsBattle(null));
    }

    [Fact]
    public void BAloneSaysItAtOnce()
    {
        party.Now = [new PartyMember(0, 3, 89, 120)];

        Assert.Empty(status.Keys([BattleStatus.KeyB]));

        Assert.Equal(["Rowan 89 of 120"], Said());
    }

    // #1007: H is the game's Raise Defences (Guard), so in a battle it reaches the game on the default bindings, and
    // no default battle key takes it.
    [Fact]
    public void InABattleHReachesTheGameOnTheDefaultsAndSaysNothing()
    {
        const int keyH = 0x48;
        party.Now = [new PartyMember(0, 3, 89, 120)];
        var defaults = BattleBindings.Default;

        Assert.Equal([keyH], status.Keys([keyH]));
        Assert.Empty(announcer.Said);
        Assert.DoesNotContain(
            keyH,
            new[]
            {
                defaults.StatusKey, defaults.EnemyHpKey, defaults.EnemyNextKey, defaults.EnemyPreviousKey,
                defaults.HpMpKey, defaults.EnemyLookKey, defaults.CommandWheelKey,
            });
    }

    // The status key is clear of the game's battle keys read aloud on 2026-11-22 (Q, H, F, E, Tab, N, T, 1 to 5) and
    // of the mod's other battle keys (K, J, L, M, I, C), the menus' details key F, the reader key T and the repeat R.
    [Fact]
    public void TheDefaultStatusKeyIsBClearOfTheGamesAndTheModsBattleKeys()
    {
        int[] taken = [0x51, 0x48, 0x46, 0x45, 0x09, 0x4E, 0x54, 0x31, 0x32, 0x33, 0x34, 0x35, 0x4B, 0x4A, 0x4C, 0x4D,
            0x49, 0x43, 0x52];

        Assert.Equal(0x42, BattleBindings.Default.StatusKey);
        Assert.DoesNotContain(BattleBindings.Default.StatusKey, taken);
    }

    [Fact]
    public void AChordWithTheKeySaysNothing()
    {
        Game(L3 | A);
        time.Advance(Hold);
        Game(L3 | A);
        Assert.Equal([BattleStatus.KeyB, 0x41], status.Keys([BattleStatus.KeyB, 0x41]));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void AHeldKeyOrASecondPadMirroringItSpeaksOnceAndNeitherTaps()
    {
        Game(L3);
        Game(L3, pad: 1);
        time.Advance(Hold);
        Game(L3);
        Game(L3, pad: 1);
        time.Advance(16);
        Game(L3);
        Game(L3, pad: 1);

        Assert.Equal([0, 0], new[] { Game(0), Game(0, pad: 1) });
        Assert.Single(announcer.Said);
    }

    // A status button the taps can't hold back (not L3, a shoulder or a trigger) speaks as it goes down, as before.
    [Fact]
    public void AStatusButtonTheTapsCannotHoldSpeaksOnThePress()
    {
        const int back = 0x0020;
        var rebound = new BattleStatus(
            party, hits, scene, announcer, () => BattleBindings.Default with { StatusButtons = back }, time);

        Assert.Equal(0, rebound.HeldButtons());
        Assert.Equal(0, rebound.Pad(0, back, made: true));
        Assert.Single(announcer.Said);
    }

    [Fact]
    public void WithNothingReadItSaysSo()
    {
        status.Keys([BattleStatus.KeyB]);

        Assert.Equal(["No party status"], Said());
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

    private sealed class NoGame : IGameInputState
    {
        public long? DialogueWindow() => null;

        public bool MenusOpen() => false;
    }
}
