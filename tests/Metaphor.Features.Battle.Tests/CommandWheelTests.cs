using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;

namespace Metaphor.Features.Battle.Tests;

/// <summary>The command wheel read aloud (#252, the maintainer's decision of 2026-11-24): C on the keyboard says the
/// wheel's keyboard keys, LB held with D-pad Up its pad buttons, each hidden from the game in a battle only.</summary>
[Trait("Category", "Unit")]
public sealed class CommandWheelTests
{
    private const int KeyC = 0x43;
    private const int Lb = 0x0400;
    private const int Up = 0x0010;
    private const int Down = 0x0040;

    private readonly RecordingAnnouncer announcer = new();
    private readonly Scene scene = new() { On = true };
    private readonly CommandWheel wheel;

    public CommandWheelTests() => wheel = new CommandWheel(scene, announcer, () => BattleBindings.Default);

    [Fact]
    public void TheDefaultsAreCAndLbWithUpClearOfTheGamesAndTheModsBattleKeys()
    {
        // The game's battle keys read aloud on 2026-11-22 (Q, H, F, E, Tab, N, T, 1 to 5), and the mod's H, K, J, L, M, I.
        int[] taken = [0x51, 0x48, 0x46, 0x45, 0x09, 0x4E, 0x54, 0x31, 0x32, 0x33, 0x34, 0x35, 0x4B, 0x4A, 0x4C, 0x4D, 0x49];
        var defaults = BattleBindings.Default;

        Assert.Equal(KeyC, defaults.CommandWheelKey);
        Assert.DoesNotContain(defaults.CommandWheelKey, taken);
        Assert.Equal(Up, defaults.CommandWheelButton);
        Assert.Equal(Lb, defaults.EnemyHoldButtons);
        Assert.DoesNotContain(defaults.CommandWheelButton, new[] { defaults.EnemyNextButton, defaults.EnemyHpButton });
        // Up was the enemy before's chord; the wheel took it, and the enemy before moved to Left (#1007).
        Assert.Equal(0x0080, defaults.EnemyPreviousButton);
        Assert.DoesNotContain(defaults.CommandWheelButton, new[] { defaults.EnemyPreviousButton });
    }

    [Fact]
    public void InABattleCSaysTheKeyboardsKeysAndIsHidden()
    {
        var handed = wheel.Keys([KeyC]);

        Assert.Empty(handed);
        var said = announcer.Said.Single();
        Assert.True(said.Interrupt);
        Assert.Equal(CommandWheel.KeyboardLine, said.Text);
        Assert.StartsWith("Archetype E, Melee F, Guard H, Synthesis Q, Item 4, Pass N, Retry 1", said.Text);
    }

    [Fact]
    public void InABattleLbWithUpSaysThePadsButtonsAndHidesOnlyUp()
    {
        Assert.Equal(Lb, wheel.Pad(0, Lb, made: true));
        Assert.Empty(announcer.Said);

        var handed = wheel.Pad(0, Lb | Up, made: true);

        Assert.Equal(Lb, handed);
        Assert.Equal(CommandWheel.PadLine, announcer.Said.Single().Text);
        Assert.StartsWith("Archetype Y, Melee X, Guard B, Synthesis A", announcer.Said.Single().Text);
    }

    [Fact]
    public void HeldDownItSpeaksOnce()
    {
        wheel.Pad(0, Lb | Up, made: true);
        wheel.Pad(0, Lb | Up, made: true);
        wheel.Keys([KeyC]);
        wheel.Keys([KeyC]);

        Assert.Equal(2, announcer.Said.Count);
    }

    [Fact]
    public void OutsideABattleBothReachTheGame()
    {
        scene.On = false;

        Assert.Equal([KeyC], wheel.Keys([KeyC]));
        Assert.Equal(Lb | Up, wheel.Pad(0, Lb | Up, made: true));
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void UpAloneOrWithDownIsNotTheChord()
    {
        Assert.Equal(Up, wheel.Pad(0, Up, made: true));
        Assert.Equal(Lb | Up | Down, wheel.Pad(0, Lb | Up | Down, made: true));
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ItsHoldKeepsLbFromTheGameInABattleOnly()
    {
        Assert.Equal(Lb, wheel.HeldShoulders());

        scene.On = false;

        Assert.Equal(0, wheel.HeldShoulders());
    }

    [Fact]
    public void AnUnboundChordHoldsNothing()
    {
        var unbound = new CommandWheel(
            scene, announcer, () => BattleBindings.Default with { CommandWheelButton = 0 });

        Assert.Equal(0, unbound.HeldShoulders());
        Assert.Equal(Lb | Up, unbound.Pad(0, Lb | Up, made: true));
        Assert.Empty(announcer.Said);
    }

    private sealed class Scene : IBattleScene
    {
        public bool On { get; set; }

        public bool InBattle() => On;
    }
}
