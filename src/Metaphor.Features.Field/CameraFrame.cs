namespace Metaphor.Features.Field;

/// <summary>The navigation kit's one frame (#823, decided 2026-11-23): the listener stands at the player's position and
/// faces the camera's horizontal angle, pitch ignored, so "left" is the screen's left. The nearby list's words (#686),
/// the guidance beep (#183), the sonar (#827) and later cues all take their bearings and pans from here. When the camera
/// isn't read the player's facing stands in. Headings grow counter-clockwise seen from above, so a bearing is left
/// positive (docs/raw-sources/field-position-camera-session6-2026-11-17.md).</summary>
public static class CameraFrame
{
    /// <summary>The ground heading the listener faces: the camera's, or the player's facing when it isn't read.</summary>
    public static double Ahead(FieldPose pose) => pose.CameraHeading ?? pose.Heading;

    /// <summary>The bearing of the ground point (<paramref name="x"/>, <paramref name="z"/>) from the listener at
    /// <paramref name="pose"/>, in radians, left positive, in (−π, π].</summary>
    public static double Bearing(FieldPose pose, double x, double z) =>
        Normalised(Math.Atan2(x - pose.X, z - pose.Z) - Ahead(pose));

    /// <summary>The stereo pan of a bearing, from −1 (left) to +1 (right): full at the side, centred ahead and behind.
    /// </summary>
    public static double Pan(double bearing) => -Math.Sin(bearing);

    /// <summary>The stereo pan of a source at <paramref name="bearing"/> and <paramref name="elevation"/> (#650): the
    /// side component of its direction, <c>-sin(bearing)·cos(elevation)</c>, so a source far above or below sits nearer
    /// the centre. Geometric; docs/contributors/explanation/elevation-and-front-back-cues.md, step 5.</summary>
    public static double Pan(double bearing, double elevation) => -Math.Sin(bearing) * Math.Cos(elevation);

    /// <summary>The elevation of the point (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) seen from
    /// the listener at <paramref name="pose"/>, in radians, up positive, in [−π/2, π/2]: its height above the player
    /// against its ground distance. Y is up (docs/raw-sources/field-position-camera-session6-2026-11-17.md).</summary>
    public static double Elevation(FieldPose pose, double x, double y, double z)
    {
        var dx = x - pose.X;
        var dz = z - pose.Z;
        return Math.Atan2(y - pose.Y, Math.Sqrt((dx * dx) + (dz * dz)));
    }

    /// <summary>The camera's ground heading and pitch from its <paramref name="eye"/> to its <paramref name="lookAt"/>
    /// point (docs/raw-sources/field-position-camera-session6-2026-11-17.md): the heading as
    /// <see cref="FieldPose.CameraHeading"/>, null when it looks straight up or down; the pitch as
    /// <see cref="FieldPose.CameraPitch"/>, null when the two points are the same.</summary>
    public static (double? Heading, double? Pitch) Camera(
        (float X, float Y, float Z) eye, (float X, float Y, float Z) lookAt)
    {
        double dx = lookAt.X - eye.X;
        double dy = lookAt.Y - eye.Y;
        double dz = lookAt.Z - eye.Z;
        var ground = Math.Sqrt(dx * dx + dz * dz);
        if (ground == 0 && dy == 0)
        {
            return (null, null);
        }
        return (ground == 0 ? null : Math.Atan2(dx, dz), Math.Atan2(dy, ground));
    }

    /// <summary><paramref name="angle"/> wrapped into (−π, π].</summary>
    public static double Normalised(double angle)
    {
        var turned = Math.IEEERemainder(angle, 2 * Math.PI);
        return turned <= -Math.PI ? turned + 2 * Math.PI : turned;
    }
}
