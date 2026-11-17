namespace Metaphor.Core.Field;

/// <summary>A point or direction in the field, as the game's units keep it: x, y, z, y read as up
/// (docs/raw-sources/field-position-2026-11-16.md, <c>[NEEDS VERIFICATION]</c>).</summary>
public readonly record struct FieldVector(float X, float Y, float Z)
{
    public override string ToString() => string.Create(
        System.Globalization.CultureInfo.InvariantCulture, $"{X:0.0},{Y:0.0},{Z:0.0}");
}

/// <summary>The angles the #177 spike derives from what it reads: a unit's facing from its rotation quaternion, and a
/// horizontal heading from a difference of positions. Both in degrees, <c>atan2(x, z)</c> as the game's own yaw helper
/// computes it (<c>0x587AB0</c> in docs/raw-sources/field-position-2026-11-16.md), so 0 faces +z. Which way is clockwise
/// on screen is what the session checks.</summary>
public static class FieldGeometry
{
    /// <summary>The heading of the vector (0, 0, 1) rotated by the quaternion (<paramref name="x"/>, <paramref name="y"/>,
    /// <paramref name="z"/>, <paramref name="w"/>), in degrees from -180 to 180.</summary>
    public static double YawDegrees(float x, float y, float z, float w)
    {
        // q v q⁻¹ for v = (0, 0, 1): only the x and z of the result are needed.
        var forwardX = 2.0 * ((x * z) + (w * y));
        var forwardZ = 1.0 - (2.0 * ((x * x) + (y * y)));
        return HeadingDegrees(forwardX, forwardZ);
    }

    /// <summary>The heading of the horizontal direction (<paramref name="dx"/>, <paramref name="dz"/>), in degrees from
    /// -180 to 180; 0 when both are 0.</summary>
    public static double HeadingDegrees(double dx, double dz) => Math.Atan2(dx, dz) * 180.0 / Math.PI;

    /// <summary>The horizontal distance from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static double GroundDistance(FieldVector from, FieldVector to)
    {
        var dx = (double)to.X - from.X;
        var dz = (double)to.Z - from.Z;
        return Math.Sqrt((dx * dx) + (dz * dz));
    }

    /// <summary>The heading from <paramref name="from"/> to <paramref name="to"/>, as <see cref="HeadingDegrees"/>.</summary>
    public static double HeadingTo(FieldVector from, FieldVector to) =>
        HeadingDegrees((double)to.X - from.X, (double)to.Z - from.Z);
}
