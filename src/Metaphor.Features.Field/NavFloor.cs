namespace Metaphor.Features.Field;

/// <summary>Who has the floor among the field's navigation sounds (#823), so the sonar, the wall tones, the route pings
/// and the guidance beep don't all sound at once. Three rules, kept simple until a by-ear session tunes them:
/// <list type="number">
/// <item>While the guidance beep guides, the sonar holds its sweeps: the beep is what the player follows.</item>
/// <item>A route ping takes the floor for <see cref="PingHoldMilliseconds"/>: the sonar drops its sweep and waits, so
/// the ping is heard alone.</item>
/// <item>The wall tones keep playing under both, at <see cref="DuckedPercent"/> of their volume while the beep guides or
/// a ping holds the floor.</item>
/// </list>
/// Thread-safe: the pad's and the keyboard's reads both ask it.</summary>
public sealed class NavFloor
{
    /// <summary>How long a route ping keeps the sonar quiet.</summary>
    public const long PingHoldMilliseconds = 800;

    /// <summary>The share of their volume the wall tones keep while another sound has the floor.</summary>
    public const int DuckedPercent = 50;

    private readonly Lock gate = new();
    private readonly Func<long> milliseconds;
    private readonly Func<bool> guiding;
    private long heldUntil;
    private bool held;

    /// <summary>A floor that keeps time by <paramref name="milliseconds"/> and asks <paramref name="guiding"/> whether
    /// the guidance beep is on.</summary>
    public NavFloor(Func<long> milliseconds, Func<bool> guiding)
    {
        ArgumentNullException.ThrowIfNull(milliseconds);
        ArgumentNullException.ThrowIfNull(guiding);
        this.milliseconds = milliseconds;
        this.guiding = guiding;
    }

    /// <summary>A route ping takes the floor from now.</summary>
    public void Hold()
    {
        lock (gate)
        {
            heldUntil = milliseconds() + PingHoldMilliseconds;
            held = true;
        }
    }

    /// <summary>Whether the sonar may ping now: no guidance beep, and no route ping holding the floor.</summary>
    public bool SonarMayPing() => !guiding() && !Held();

    /// <summary>The share of their volume, in percent, the wall tones play at now.</summary>
    public int WallPercent() => guiding() || Held() ? DuckedPercent : 100;

    private bool Held()
    {
        lock (gate)
        {
            return held && milliseconds() < heldUntil;
        }
    }
}
