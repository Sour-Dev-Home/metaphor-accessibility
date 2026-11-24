using Metaphor.Core.Cues;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>Where the navigation kit's positional cues sound from (#823): the pan from <see cref="CameraFrame"/>, narrowed
/// by height when <see cref="FieldFeature.HeightPan"/> is on (#650), and the behind-muffle when
/// <see cref="FieldFeature.BehindMuffle"/> is on (#903): a cue whose source lies past <see cref="MuffleDegrees"/> from
/// the camera's facing is low-passed (<see cref="CueWave.MuffleHertz"/>) and a little quieter, the same in both ears, so
/// front and back differ on speakers too. Both testing, from docs/contributors/explanation/elevation-and-front-back-cues.md
/// steps 2 and 5. The idea of muffling what is behind you comes from Coswars (coswars.com, MERLIN), a blind-accessible
/// shooter, from its public blog pages; idea only, no code (CREDITS.md).</summary>
public sealed class CuePlacement
{
    /// <summary>A muffled cue's volume, as a share of its own: 85 is about 3 dB quieter on the volume's square curve.
    /// The mod's own choice until a by-ear check tunes it.</summary>
    public const int MuffledShare = 85;

    private readonly Func<bool> muffle;
    private readonly Func<int> muffleDegrees;
    private readonly Func<bool> heightPan;

    /// <summary>Placement that muffles while <paramref name="muffle"/> says so, past the bearing
    /// <paramref name="muffleDegrees"/> gives, and narrows the pan by height while <paramref name="heightPan"/> says so;
    /// each read on every cue.</summary>
    public CuePlacement(Func<bool> muffle, Func<int> muffleDegrees, Func<bool> heightPan)
    {
        ArgumentNullException.ThrowIfNull(muffle);
        ArgumentNullException.ThrowIfNull(muffleDegrees);
        ArgumentNullException.ThrowIfNull(heightPan);
        this.muffle = muffle;
        this.muffleDegrees = muffleDegrees;
        this.heightPan = heightPan;
    }

    /// <summary>The plain frame: the pan from the bearing alone, nothing muffled.</summary>
    public static CuePlacement Plain { get; } = new(() => false, () => 180, () => false);

    /// <summary>The settings file's <c>limits.field.behindMuffleDegrees</c>: how far from straight ahead, either way, a
    /// source must lie to be muffled. Past 90 is behind the camera's side line; the default leaves a margin so a source
    /// at the side doesn't flicker in and out.</summary>
    public static SliderLimit MuffleDegrees { get; } = new("field", "behindMuffleDegrees", 100, 90, 170, 5)
    {
        Spoken = "Behind muffle angle",
        Description = "How far round from straight ahead, in degrees either way, a navigation sound must come from to " +
            "be muffled as behind you (`behindMuffle`).",
        Order = 120,
    };

    /// <summary>Whether a source at <paramref name="bearing"/> (radians, as <see cref="CameraFrame.Bearing"/>) lies past
    /// <paramref name="degrees"/> from straight ahead.</summary>
    public static bool Behind(double bearing, int degrees) =>
        Math.Abs(CameraFrame.Normalised(bearing)) > degrees * Math.PI / 180;

    /// <summary><paramref name="tone"/> placed at <paramref name="bearing"/> and <paramref name="elevation"/>: its pan,
    /// and when the muffle is on and the source behind, muffled at <see cref="MuffledShare"/> of its volume.</summary>
    public HeldTone Place(HeldTone tone, double bearing, double elevation = 0)
    {
        var placed = tone with { Pan = heightPan() ? CameraFrame.Pan(bearing, elevation) : CameraFrame.Pan(bearing) };
        return muffle() && Behind(bearing, muffleDegrees())
            ? placed with { Muffled = true, Volume = tone.Volume * MuffledShare / 100 }
            : placed;
    }

    /// <summary><paramref name="tone"/> placed toward the point (<paramref name="x"/>, <paramref name="y"/>,
    /// <paramref name="z"/>) from the listener at <paramref name="pose"/>.</summary>
    public HeldTone Toward(HeldTone tone, FieldPose pose, double x, double y, double z) =>
        Place(tone, CameraFrame.Bearing(pose, x, z), CameraFrame.Elevation(pose, x, y, z));
}
