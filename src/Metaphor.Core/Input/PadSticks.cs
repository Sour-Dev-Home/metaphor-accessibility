namespace Metaphor.Core.Input;

/// <summary>One read of a pad's two sticks as the game keeps them, after its button mask (#701, #184): each axis a
/// 16-bit value centred on <see cref="Centre"/>, 0 to 255, x growing to the right and y growing <b>down</b> (pushed up is
/// below the centre), and exactly the centre inside the game's dead zone, on game build 548ddc95… (Steam build 18330018)
/// (docs/findings/pad-sticks-in-pad-state-static.md).</summary>
/// <param name="RightX">The right stick's x, at <c>+2</c>.</param>
/// <param name="RightY">The right stick's y, at <c>+4</c>.</param>
/// <param name="LeftX">The left stick's x, at <c>+6</c>.</param>
/// <param name="LeftY">The left stick's y, at <c>+8</c>.</param>
public readonly record struct PadSticks(int RightX, int RightY, int LeftX, int LeftY)
{
    /// <summary>An axis at rest.</summary>
    public const int Centre = 0x80;

    /// <summary>How far an axis goes from the centre at a full push.</summary>
    public const int Reach = 127;

    /// <summary>Both sticks at rest.</summary>
    public static PadSticks Rest { get; } = new(Centre, Centre, Centre, Centre);

    /// <summary>Whether either stick is away from rest: the player is pushing one.</summary>
    public bool Moved => this != Rest;

    /// <summary>The right stick as (x, y), each −1 to 1, right and up positive.</summary>
    public (double X, double Y) Right => (Unit(RightX), -Unit(RightY));

    /// <summary>The left stick as (x, y), each −1 to 1, right and up positive.</summary>
    public (double X, double Y) Left => (Unit(LeftX), -Unit(LeftY));

    /// <summary>These sticks with the right one at rest.</summary>
    public PadSticks RightAtRest() => this with { RightX = Centre, RightY = Centre };

    /// <summary>These sticks with the left one pushed to (<paramref name="x"/>, <paramref name="y"/>), each −1 to 1, right
    /// and up positive, clamped to the stick's reach.</summary>
    public PadSticks WithLeft(double x, double y) => this with { LeftX = Axis(x), LeftY = Axis(-y) };

    private static double Unit(int value) => Math.Clamp((value - Centre) / (double)Reach, -1, 1);

    private static int Axis(double value) => Centre + (int)Math.Round(Math.Clamp(value, -1, 1) * Reach);
}

/// <summary>Controls that take each pad read's sticks too (#701, #184), after the buttons, and say what to hand the game.
/// </summary>
public interface IStickControls
{
    /// <summary>Takes one read of <paramref name="pad"/>'s <paramref name="sticks"/>, with its
    /// <paramref name="buttons"/> as the game read them, and returns the sticks to hand on.</summary>
    PadSticks Sticks(int pad, int buttons, PadSticks sticks);
}
