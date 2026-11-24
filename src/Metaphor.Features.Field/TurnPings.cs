using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>When a one-shot navigation cue sounds again because the camera turned (#827, #831), testing as
/// <see cref="FieldFeature.TurnPings"/>: turning moves a sound ahead one way and a sound behind the other, so a ping
/// heard again mid-turn tells front from back in plain stereo
/// (docs/contributors/explanation/elevation-and-front-back-cues.md, step 4).</summary>
public static class TurnPings
{
    /// <summary>How long after a route ping a turn may still sound it again; the sonar re-pings only the thing it pinged
    /// last, until its next ping.</summary>
    public const long RouteWindowMilliseconds = 10000;

    /// <summary>The settings file's <c>limits.field.turnPingDegrees</c>: how far the camera must turn, either way, since
    /// a ping for it to sound again.</summary>
    public static SliderLimit Degrees { get; } = new("field", "turnPingDegrees", 30, 10, 90, 5)
    {
        Spoken = "Turn ping angle",
        Description = "How far the camera must turn, in degrees, for the sonar's or a route ping's last sound to play " +
            "again (`turnPings`).",
        Order = 130,
    };

    /// <summary>Whether the camera has turned from <paramref name="from"/> to <paramref name="to"/> (ground headings in
    /// radians) by at least <paramref name="degrees"/>, either way, the short way round.</summary>
    public static bool Turned(double from, double to, int degrees) =>
        Math.Abs(CameraFrame.Normalised(to - from)) >= degrees * Math.PI / 180;
}
