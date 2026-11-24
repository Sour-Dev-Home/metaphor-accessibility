using Metaphor.Core.Cues;

namespace Metaphor.Features.Field.Tests;

/// <summary>The enemy awareness cues (#649): the notice and give-up edges of a field enemy's AI state, each cue from the
/// enemy's side, the line said once, and nothing when off, out of free roam, or with no state read (a Release build).
/// </summary>
[Trait("Category", "Unit")]
public sealed class EnemyAwarenessTests
{
    private const int Patrol = 8;

    private readonly NavField field = new();
    private readonly PlayedCues cues = new();
    private readonly SaidList said = new();
    private readonly List<string> logged = [];
    private bool on = true;
    private long now = 1000;
    private readonly EnemyAwareness awareness;

    public EnemyAwarenessTests() =>
        awareness = new EnemyAwareness(field, cues, said, () => now, () => on, logged.Add);

    // With the camera looking along +z, +x is on the screen's left (CameraFrame).
    private void Read(params FieldUnit[] enemies)
    {
        field.Units = enemies;
        now += EnemyAwareness.CheckIntervalMilliseconds;
        awareness.Check();
    }

    private static FieldUnit Enemy(float x, int? state, int? requested = null) =>
        new(FieldKind.Enemy, x, 0, 0, AiState: state, AiRequested: requested);

    [Fact]
    public void AnEnemyNoticingYouPlaysTheAlertFromItsSideAndSaysItOnce()
    {
        Read(Enemy(500, Patrol));
        Read(Enemy(480, EnemyAwareness.NoticedState));
        Read(Enemy(450, EnemyAwareness.NoticedState));

        var (cue, pan) = Assert.Single(cues.Played);
        Assert.Equal(Cue.EnemyNoticed, cue);
        Assert.Equal(-1, pan, 6);
        Assert.Equal([EnemyAwareness.NoticedText], said.Said.Select(a => a.Text));
    }

    [Fact]
    public void ARequestForTheNoticedStateCountsAsNoticing()
    {
        Read(Enemy(500, Patrol));
        Read(Enemy(500, Patrol, requested: EnemyAwareness.NoticedState));

        Assert.Equal([Cue.EnemyNoticed], cues.Played.Select(p => p.Cue));
    }

    [Fact]
    public void BackInItsPatrolItGaveUpWithASoundAndNoLine()
    {
        Read(Enemy(-500, Patrol));
        Read(Enemy(-400, EnemyAwareness.NoticedState));
        Read(Enemy(-300, 3));
        Read(Enemy(-350, Patrol, requested: Patrol));

        Assert.Equal([Cue.EnemyNoticed, Cue.EnemyGaveUp], cues.Played.Select(p => p.Cue));
        Assert.Equal(1, cues.Played[1].Pan, 6);
        Assert.Single(said.Said.Select(a => a.Text));
    }

    [Fact]
    public void ChasingThroughOtherStatesIsNotGivingUp()
    {
        Read(Enemy(500, Patrol));
        Read(Enemy(500, EnemyAwareness.NoticedState));
        Read(Enemy(400, 3));
        Read(Enemy(300, 5));
        Read(Enemy(250, 6));

        Assert.Equal([Cue.EnemyNoticed], cues.Played.Select(p => p.Cue));
    }

    [Fact]
    public void TwoEnemiesAreFollowedApartByWhereTheyStand()
    {
        Read(Enemy(500, Patrol), Enemy(-500, Patrol));
        Read(Enemy(-480, EnemyAwareness.NoticedState), Enemy(520, Patrol));
        Read(Enemy(-460, Patrol), Enemy(540, Patrol));

        Assert.Equal([(Cue.EnemyNoticed, 1.0), (Cue.EnemyGaveUp, 1.0)],
            cues.Played.Select(p => (p.Cue, Math.Round(p.Pan, 6))));
    }

    [Fact]
    public void ReadsAreThrottledAndEachStateChangeIsLogged()
    {
        Read(Enemy(500, Patrol));
        field.Units = [Enemy(500, EnemyAwareness.NoticedState)];
        now += EnemyAwareness.CheckIntervalMilliseconds - 1;
        awareness.Check();
        Assert.Empty(cues.Played);
        now += 1;
        awareness.Check();
        Read(Enemy(500, EnemyAwareness.NoticedState));

        Assert.Single(cues.Played);
        Assert.Equal(2, logged.Count);
        Assert.StartsWith("enemy ai n=1 state=-->8 requested=- calm=- noticed=0 ", logged[0], StringComparison.Ordinal);
        Assert.StartsWith("enemy ai n=1 state=8->30 requested=- calm=8 noticed=0 ", logged[1], StringComparison.Ordinal);
    }

    [Fact]
    public void NothingWhenOffOutOfFreeRoamOrWithNoStateRead()
    {
        on = false;
        Read(Enemy(500, Patrol));
        Read(Enemy(500, EnemyAwareness.NoticedState));
        on = true;
        field.Roaming = false;
        Read(Enemy(500, Patrol));
        Read(Enemy(500, EnemyAwareness.NoticedState));
        field.Roaming = true;
        Read(Enemy(500, null));
        Read(Enemy(500, null));

        Assert.Empty(cues.Played);
        Assert.Empty(said.Said.Select(a => a.Text));
    }

    [Fact]
    public void TheKeysAndButtonsPassThroughAndTheKeysCheck()
    {
        field.Units = [Enemy(500, EnemyAwareness.NoticedState)];
        now += EnemyAwareness.CheckIntervalMilliseconds;

        Assert.Equal([0x4e], awareness.Keys([0x4e]));
        Assert.Equal(5, awareness.Pad(0, 5, made: true));
        Assert.Single(cues.Played);
    }

    private sealed class PlayedCues : ICuePlayer
    {
        public List<(Cue Cue, double Pan)> Played { get; } = [];

        public void Play(Cue cue) => Played.Add((cue, 0));

        public void Play(Cue cue, double pan) => Played.Add((cue, pan));

        public void Hold(ToneChannel channel, HeldTone tone)
        {
        }

        public void Silence(ToneChannel channel)
        {
        }
    }
}
