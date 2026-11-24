using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle.Tests;

/// <summary>The enemy HP keys in a battle (#767, the maintainer's decision of 2026-11-23): K and J step to the next and
/// previous enemy and say its name and HP, L says them all, and the pad's LB held with D-pad Down, Up and Left does the
/// same. Each is hidden from the game in a battle only; F is left to the game. Made-up numbers only.</summary>
[Trait("Category", "Unit")]
public sealed class BattleEnemyHpTests
{
    private const int Number = 1;
    private const int KeyF = 0x46;
    private const int KeyJ = 0x4A;
    private const int KeyK = 0x4B;
    private const int KeyL = 0x4C;
    private const int A = 0x4000;
    private const int LB = 0x0400;
    private const int RS = 0x0004;
    private const int DpadUp = 0x0010;
    private const int DpadDown = 0x0040;
    private const int DpadLeft = 0x0080;

    private readonly RecordingAnnouncer announcer = new();
    private readonly ManualTime time = new();
    private readonly Scene scene = new() { On = true };
    private readonly List<string> logged = [];
    private readonly HitLines hits;
    private readonly BattleEnemyHp enemies;

    public BattleEnemyHpTests()
    {
        hits = new HitLines(new SilentAnnouncer(), time, _ => null);
        enemies = new BattleEnemyHp(() => BattleBindings.Default, hits, scene, announcer, logged.Add);
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    // Each press is a key going down: let go between presses.
    private void Press(int key)
    {
        enemies.Keyboard.Keys([key]);
        enemies.Keyboard.Keys([]);
    }

    private void Chord(int button)
    {
        enemies.Pad.Pad(0, LB | button, made: true);
        enemies.Pad.Pad(0, LB, made: true);
    }

    private void TwoEnemiesHit()
    {
        hits.Observe(new BattleHit(0xa0, -360, 840, Number, BattleSide.Enemy, HpMax: 1200));
        hits.Observe(new BattleHit(0xb0, -10, 50, Number, BattleSide.Enemy));
    }

    [Fact]
    public void TheDefaultsAreKJAndLAndLbWithTheDpadClearOfTheGamesAndTheModsBattleKeys()
    {
        var keys = BattleBindings.Default;

        Assert.Equal((KeyK, KeyJ, KeyL), (keys.EnemyNextKey, keys.EnemyPreviousKey, keys.EnemyHpKey));
        Assert.Equal(
            (LB, DpadDown, DpadUp, DpadLeft),
            (keys.EnemyHoldButtons, keys.EnemyNextButton, keys.EnemyPreviousButton, keys.EnemyHpButton));
        // The game's battle keys read aloud on 2026-11-22 (Q, H, F, E, Tab, N, T, 1 to 5), the status key H, the
        // menus' details key F, the reader key T and the repeat key R.
        int[] taken = [0x51, 0x48, KeyF, 0x45, 0x09, 0x4E, 0x54, 0x31, 0x32, 0x33, 0x34, 0x35, 0x52];
        Assert.DoesNotContain(keys.EnemyNextKey, taken);
        Assert.DoesNotContain(keys.EnemyPreviousKey, taken);
        Assert.DoesNotContain(keys.EnemyHpKey, taken);
    }

    // Neither is heard by ear yet, so both ship off: a normal install leaves K, J, L and LB with the D-pad to the game.
    [Fact]
    public void TheKeysAreOnAndThePadChordsAreStillTesting()
    {
        Assert.False(SettingKey.Of(BattleFeature.BattleEnemyKeys).Testing);
        Assert.True(SettingKey.Of(BattleFeature.BattleEnemyPad).Testing);
        Assert.True(ModSettings.Defaults.IsOn(BattleFeature.BattleEnemyKeys));
        Assert.False(ModSettings.Defaults.IsOn(BattleFeature.BattleEnemyPad));
    }

    [Fact]
    public void TheFileNamesEveryEnemyBinding()
    {
        var settings = ModSettings.Parse(
            """
            { "keys": { "battle": { "enemyNextKey": "F2", "enemyPreviousKey": "F3", "enemyHpKey": "none",
              "enemyHoldButtons": "RB", "enemyNextButton": "Y", "enemyPreviousButton": "none",
              "enemyHpButton": "DpadRight" } } }
            """, SettingCatalogue.Empty, _ => { });

        Assert.Equal(
            BattleBindings.Default with
            {
                EnemyNextKey = 0x71,
                EnemyPreviousKey = 0x72,
                EnemyHpKey = 0,
                EnemyHoldButtons = 0x0800,
                EnemyNextButton = 0x1000,
                EnemyPreviousButton = 0,
                EnemyHpButton = 0x0020,
            },
            BattleBindings.From(settings));
        Assert.Equal(BattleBindings.Default, BattleBindings.From(ModSettings.Defaults));
    }

    // #854: the "of max HP" is a setting; off says the current HP alone, as for an enemy whose maximum isn't known.
    [Fact]
    public void WithTheMaxHpTurnedOffALineSaysTheCurrentHpAlone()
    {
        var say = true;
        var quiet = new BattleEnemyHp(() => BattleBindings.Default, hits, scene, announcer) { SayMaxHp = () => say };
        TwoEnemiesHit();
        quiet.Keyboard.Keys([]);

        say = false;
        quiet.Keyboard.Keys([KeyK]);
        quiet.Keyboard.Keys([]);
        say = true;
        quiet.Keyboard.Keys([KeyK]);

        Assert.Equal(["First enemy, 840 left", "Second enemy, 50 left"], Said());
    }

    [Fact]
    public void KStepsToEachEnemyInTurnSayingItsHpAndIsHidden()
    {
        TwoEnemiesHit();

        Assert.Empty(enemies.Keyboard.Keys([KeyK]));
        enemies.Keyboard.Keys([]);
        Press(KeyK);
        Press(KeyK);

        Assert.Equal(["First enemy, 840 of 1200", "Second enemy, 50 left", "First enemy, 840 of 1200"], Said());
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }

    [Fact]
    public void JStepsBackFromTheLastEnemy()
    {
        TwoEnemiesHit();

        Press(KeyJ);
        Press(KeyJ);
        Press(KeyK);

        Assert.Equal(["Second enemy, 50 left", "First enemy, 840 of 1200", "Second enemy, 50 left"], Said());
    }

    [Fact]
    public void LSaysEveryEnemyInOneLine()
    {
        TwoEnemiesHit();

        Assert.Empty(enemies.Keyboard.Keys([KeyL]));

        Assert.Equal(["First enemy, 840 of 1200. Second enemy, 50 left"], Said());
    }

    [Fact]
    public void AStepKeepsToTheSameEnemyWhenAnotherIsHit()
    {
        hits.Observe(new BattleHit(0xa0, -5, 55, Number, BattleSide.Enemy));
        Press(KeyK);
        hits.Observe(new BattleHit(0xb0, -10, 50, Number, BattleSide.Enemy));

        Press(KeyK);

        Assert.Equal(["Enemy, 55 left", "Second enemy, 50 left"], Said());
    }

    [Fact]
    public void ANewBattleStartsTheStepsAgainAtTheFirstEnemy()
    {
        TwoEnemiesHit();
        Press(KeyK);
        hits.BattleOver();
        time.Advance(HitLines.RepeatMilliseconds + 1);
        TwoEnemiesHit();

        Press(KeyK);

        Assert.Equal("First enemy, 840 of 1200", Said().Last());
    }

    [Fact]
    public void WithNoEnemyHitEachKeySaysSo()
    {
        hits.Observe(new BattleHit(0x1000, -5, 55, Number, BattleSide.Ally, 0, Unit: 3));

        Press(KeyK);
        Press(KeyJ);
        Press(KeyL);

        Assert.Equal(["No enemy hit yet", "No enemy hit yet", "No enemy hit yet"], Said());
    }

    [Fact]
    public void FAndRsReachTheGameInABattle()
    {
        TwoEnemiesHit();

        Assert.Equal([KeyF], enemies.Keyboard.Keys([KeyF]));
        Assert.Equal(RS, enemies.Pad.Pad(0, RS, made: true));
        Assert.Empty(Said());
    }

    [Fact]
    public void LbWithTheDpadStepsAndSumsUpHidingOnlyTheDpad()
    {
        TwoEnemiesHit();

        Assert.Equal(LB, enemies.Pad.Pad(0, LB, made: true));
        Assert.Equal(LB, enemies.Pad.Pad(0, LB | DpadDown, made: true));
        Assert.Equal(LB, enemies.Pad.Pad(0, LB, made: true));
        Chord(DpadUp);
        Chord(DpadLeft);

        Assert.Equal(
            ["First enemy, 840 of 1200", "Second enemy, 50 left", "First enemy, 840 of 1200. Second enemy, 50 left"],
            Said());
    }

    [Fact]
    public void TheDpadWithoutLbReachesTheGame()
    {
        TwoEnemiesHit();

        Assert.Equal(DpadDown, enemies.Pad.Pad(0, DpadDown, made: true));
        Assert.Empty(Said());
    }

    [Fact]
    public void TheKeyboardKeysAndThePadChordsAreSeparateControls()
    {
        TwoEnemiesHit();

        Assert.Equal([KeyK], enemies.Pad.Keys([KeyK]));
        Assert.Equal(LB | DpadDown, enemies.Keyboard.Pad(0, LB | DpadDown, made: true));
        Assert.Empty(Said());
    }

    [Fact]
    public void OutsideABattleTheKeysReachTheGameAndSayNothing()
    {
        scene.On = false;

        Assert.Equal([KeyK], enemies.Keyboard.Keys([KeyK]));
        Assert.Equal([KeyL], enemies.Keyboard.Keys([KeyL]));
        Assert.Equal(LB | DpadDown, enemies.Pad.Pad(0, LB | DpadDown, made: true));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void AChordWithAKeyReachesTheGameAndSaysNothing()
    {
        Assert.Equal([KeyK, 0x41], enemies.Keyboard.Keys([KeyK, 0x41]));
        Assert.Equal(LB | DpadDown | A, enemies.Pad.Pad(0, LB | DpadDown | A, made: true));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void AHeldKeyOrASecondPadMirroringItSpeaksOnce()
    {
        TwoEnemiesHit();

        enemies.Pad.Pad(0, LB | DpadDown, made: true);
        enemies.Pad.Pad(0, LB | DpadDown, made: true);
        enemies.Pad.Pad(1, LB | DpadDown, made: true);
        Assert.Single(announcer.Said);

        enemies.Keyboard.Keys([KeyK]);
        enemies.Keyboard.Keys([KeyK]);
        Assert.Equal(2, announcer.Said.Count);
    }

    [Fact]
    public void AKeyReboundInTheFileAppliesAtTheNextRead()
    {
        var bindings = BattleBindings.Default;
        var rebound = new BattleEnemyHp(() => bindings, hits, scene, announcer);
        TwoEnemiesHit();
        bindings = bindings with { EnemyNextKey = 0x71 };

        Assert.Equal([KeyK], rebound.Keyboard.Keys([KeyK]));
        Assert.Empty(rebound.Keyboard.Keys([0x71]));
        Assert.Equal(["First enemy, 840 of 1200"], Said());
    }

    // For the next session's target read: each summary logs every known enemy's record, to match against the target.
    [Fact]
    public void EachSummaryLogsTheKnownEnemiesRecords()
    {
        hits.Observe(new BattleHit(0xa0, -360, 840, Number, BattleSide.Enemy, HpMax: 1200));

        Press(KeyL);

        Assert.Equal(["enemies=1 Enemy record=0xa0 hp=840 max=1200"], logged);
    }

    // #811: LB opens Tactics as it goes down, so in a battle the pad chords hold LB back until it's let go; held with the
    // D-pad it's the chord and the game never sees it, tapped alone it reaches the game on release.
    private int ChordThroughTaps(ShoulderTaps taps, int buttons) =>
        taps.Pad(0, buttons, enemies.Pad.Pad(0, buttons, made: true), made: true);

    [Fact]
    public void InABattleLbWithTheDpadIsTheChordAndNeverReachesTheGame()
    {
        var taps = new ShoulderTaps([enemies.HeldShoulders], () => 0);
        TwoEnemiesHit();

        var handed = new[]
        {
            ChordThroughTaps(taps, LB), ChordThroughTaps(taps, LB | DpadDown), ChordThroughTaps(taps, LB),
            ChordThroughTaps(taps, 0), ChordThroughTaps(taps, 0), ChordThroughTaps(taps, 0),
        };

        Assert.Equal([0, 0, 0, 0, 0, 0], handed);
        Assert.Equal(["First enemy, 840 of 1200"], Said());
    }

    [Fact]
    public void InABattleALoneLbTapReachesTheGameOnRelease()
    {
        var taps = new ShoulderTaps([enemies.HeldShoulders], () => 0);

        Assert.Equal(0, ChordThroughTaps(taps, LB));
        Assert.Equal(0, ChordThroughTaps(taps, LB));
        Assert.Equal(LB, ChordThroughTaps(taps, 0));
        Assert.Empty(Said());
    }

    // #835, the maintainer's decision of 2026-11-23: LB held alone is never handed to the game, however long, so it
    // can stay down while the D-pad steps the enemies; let go with no chord, it is a tap.
    [Fact]
    public void InABattleLbHeldAloneForAnyTimeIsATapOnRelease()
    {
        long now = 0;
        var taps = new ShoulderTaps([enemies.HeldShoulders], () => now);

        Assert.Equal(0, ChordThroughTaps(taps, LB));
        now = 5000;
        Assert.Equal(0, ChordThroughTaps(taps, LB));
        now = 5016;
        Assert.Equal(LB, ChordThroughTaps(taps, 0));
        Assert.Empty(Said());
    }

    [Fact]
    public void OutsideABattleOrWithTheChordsUnboundLbIsNotHeld()
    {
        var unbound = new BattleEnemyHp(
            () => BattleBindings.Default with { EnemyHpButton = 0, EnemyNextButton = 0, EnemyPreviousButton = 0 },
            hits, scene, announcer);

        Assert.Equal(LB, enemies.HeldShoulders() & LB);
        Assert.Equal(0, unbound.HeldShoulders());
        scene.On = false;
        Assert.Equal(0, enemies.HeldShoulders());
    }

    private sealed class Scene : IBattleScene
    {
        public bool On { get; set; }

        public bool InBattle() => On;
    }
}
