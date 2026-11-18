using System.Diagnostics.CodeAnalysis;

namespace Metaphor.Core.Battle;

// Issue #251, the tracer bullet of spec #247: the events every battle feature subscribes to, decoded from the battle
// package's per-frame update and a battle-state reader. Battle started and ended are emitted; turn started, target
// focused and action resolved are declared for the slices that find their state (#162). Elements, offsets and phases:
// docs/raw-sources/battle-state-session6-2026-11-17.md, build 548ddc95 (Steam build 18330018).

/// <summary>A battle phase, by the class the battle package runs it as (<c>Load@phase@btl</c> and its siblings). IDs and
/// names: docs/raw-sources/battle-state-leads-2026-11-17.md; the ones a battle passes through:
/// docs/raw-sources/battle-state-session6-2026-11-17.md. <see cref="Unknown"/> for any other class or an unreadable
/// one.</summary>
public enum BattlePhase
{
    Unknown,
    Launcher,
    Load,
    Beginning,
    Fight,
    AdvantageousTransition,
    PinchTransition,
    AllyEscape,
    Victory,
    Annihilation,
    Interruption,
    Rewind,
    Finalize,
}

/// <summary>Names a phase from its class.</summary>
public static class BattlePhases
{
    private const string Namespace = "@phase@btl";

    /// <summary>The phase whose class is <paramref name="className"/>, as <see cref="RttiName"/> gives it (such as
    /// <c>Victory@phase@btl</c>); <see cref="BattlePhase.Unknown"/> for a class outside <c>phase@btl</c> or one this
    /// module doesn't use.</summary>
    public static BattlePhase FromClassName(string? className) =>
        className is not null && className.EndsWith(Namespace, StringComparison.Ordinal) &&
        Enum.TryParse<BattlePhase>(className[..^Namespace.Length], ignoreCase: false, out var phase) &&
        Enum.IsDefined(phase)
            ? phase
            : BattlePhase.Unknown;
}

/// <summary>What the battle events read from the battle the package update hook reports (#247): its real
/// implementation reads the package in the hook layer; tests fake it. Each read is null (or
/// <see cref="BattlePhase.Unknown"/>) when it can't be made.</summary>
public interface IBattleState
{
    /// <summary>The phase the battle package at <paramref name="package"/> runs now.</summary>
    BattlePhase Phase(long package);

    /// <summary>How many enemies are still standing (package <c>+0x140</c>).</summary>
    int? EnemyCount(long package);

    /// <summary>The money the battle has paid out so far (package <c>+0x3E4</c>; confirmed once, at a victory).</summary>
    int? MoneyWon(long package);
}

/// <summary>How a battle ended. <see cref="Lost"/> is the <c>Annihilation</c> phase, not yet seen in a session.
/// <see cref="Unknown"/>: the battle reached <c>Interruption</c> or <c>Finalize</c> without a phase that names the end.
/// </summary>
public enum BattleOutcome
{
    Unknown,
    Won,
    Escaped,
    Lost,
}

/// <summary>A battle began against <paramref name="EnemyCount"/> enemies. Which enemies, and an advantage or ambush,
/// are [NEEDS VERIFICATION] (docs/raw-sources/battle-state-session6-2026-11-17.md).</summary>
public sealed record BattleStarted(int EnemyCount);

/// <summary>A party member's turn began. Declared for #163; not emitted yet: the acting member is
/// [NEEDS VERIFICATION].</summary>
public sealed record TurnStarted(string Member, int Hp, int MaxHp);

/// <summary>The target cursor moved to an enemy. Declared for #163; not emitted yet: the targeted enemy is
/// [NEEDS VERIFICATION].</summary>
public sealed record TargetFocused(string Enemy, int Position);

/// <summary>An action finished. Declared for #163; not emitted yet: attacker, action and results are
/// [NEEDS VERIFICATION].</summary>
public sealed record ActionResolved(string Attacker, string Action);

/// <summary>A battle ended with <paramref name="Outcome"/>, having paid <paramref name="Money"/> when that was read (a
/// victory).</summary>
public sealed record BattleEnded(BattleOutcome Outcome, int? Money);

/// <summary>Told of the battle events it cares about; each method it doesn't implement does nothing.</summary>
public interface IBattleObserver
{
    void Started(BattleStarted battle)
    {
    }

    void TurnStarted(TurnStarted turn)
    {
    }

    void TargetFocused(TargetFocused target)
    {
    }

    void ActionResolved(ActionResolved action)
    {
    }

    void Ended(BattleEnded battle)
    {
    }
}

/// <summary>Turns the battle package's frames into battle events (#247), as <see cref="Menus.CursorEvents"/> does for
/// cursor inputs. A new package is a new battle, and so is a package that starts loading again after its battle ended;
/// the battle starts on its first frame with a plausible enemy count and ends on the first phase that names the end, or
/// at <c>Interruption</c> or <c>Finalize</c>. Each frame reads the phase and, until the battle has started, the enemy
/// count; nothing more. Observers hear each event in the order they were added, and one that throws never stops the
/// rest. Add observers before the hooks go live; frames are safe from any game thread.</summary>
public sealed class BattleEvents
{
    // A guard against a misread, not the game's limit (unknown): a session showed 1 to 3 (#162).
    private const int MostEnemies = 32;

    private readonly IBattleState state;
    private readonly Lock gate = new();
    private IBattleObserver[] observers = [];
    private long package;
    private bool started;
    private bool ended;

    public BattleEvents(IBattleState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        this.state = state;
    }

    /// <summary>Adds an observer of battle events, after those already added.</summary>
    public void Observe(IBattleObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (gate)
        {
            observers = [.. observers, observer];
        }
    }

    /// <summary>One frame of the battle whose package is at <paramref name="current"/>; 0 is ignored.</summary>
    public void Frame(long current)
    {
        if (current == 0)
        {
            return;
        }
        BattleStarted? start = null;
        BattleEnded? end = null;
        lock (gate)
        {
            var phase = state.Phase(current);
            if (current != package || (ended && phase is BattlePhase.Launcher or BattlePhase.Load))
            {
                package = current;
                started = false;
                ended = false;
            }
            if (!started)
            {
                if (EndOf(phase) is null && state.EnemyCount(current) is { } count and > 0 and <= MostEnemies)
                {
                    started = true;
                    start = new BattleStarted(count);
                }
            }
            else if (!ended && EndOf(phase) is { } outcome)
            {
                ended = true;
                end = new BattleEnded(outcome, outcome == BattleOutcome.Won ? state.MoneyWon(current) : null);
            }
        }
        var listeners = Volatile.Read(ref observers);
        if (start is not null)
        {
            TellEach(listeners, observer => observer.Started(start));
        }
        if (end is not null)
        {
            TellEach(listeners, observer => observer.Ended(end));
        }
    }

    // The outcome a phase names, or null for a phase inside the battle.
    private static BattleOutcome? EndOf(BattlePhase phase) => phase switch
    {
        BattlePhase.Victory => BattleOutcome.Won,
        BattlePhase.AllyEscape => BattleOutcome.Escaped,
        BattlePhase.Annihilation => BattleOutcome.Lost,
        BattlePhase.Interruption or BattlePhase.Finalize => BattleOutcome.Unknown,
        _ => null,
    };

    [SuppressMessage("Design", "CA1031", Justification = "One observer's failure must not keep the event from the others, or reach the game.")]
    private static void TellEach(IBattleObserver[] observers, Action<IBattleObserver> tell)
    {
        foreach (var observer in observers)
        {
            try
            {
                tell(observer);
            }
            catch (Exception)
            {
            }
        }
    }
}
