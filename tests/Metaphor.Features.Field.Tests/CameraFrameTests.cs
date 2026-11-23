namespace Metaphor.Features.Field.Tests;

/// <summary>The camera-relative frame (#686, #823): bearings from the camera's ground heading, the facing when the camera
/// isn't read, and the stereo pan of a bearing. Headings grow counter-clockwise seen from above, so +x is on the left
/// looking along +z (docs/raw-sources/field-position-camera-session6-2026-11-17.md).</summary>
[Trait("Category", "Unit")]
public sealed class CameraFrameTests
{
    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(100, 0, Math.PI / 2)]
    [InlineData(-100, 0, -Math.PI / 2)]
    [InlineData(0, -100, Math.PI)]
    public void BearingsAreFromTheCameraLeftPositive(double x, double z, double bearing)
    {
        // The character faces the camera (heading π); only the camera's heading counts.
        var pose = new FieldPose(0, 0, 0, Math.PI, CameraHeading: 0);

        Assert.Equal(bearing, CameraFrame.Bearing(pose, x, z), 6);
    }

    [Fact]
    public void BearingsFallBackToTheFacingWhenTheCameraIsUnread()
    {
        var pose = new FieldPose(0, 0, 0, Math.PI);

        Assert.Equal(-Math.PI / 2, CameraFrame.Bearing(pose, 100, 0), 6);
        Assert.Equal(Math.PI, CameraFrame.Ahead(pose), 6);
    }

    [Fact]
    public void BearingsAreFromTheListenersPosition()
    {
        var pose = new FieldPose(500, 0, 500, 0, CameraHeading: Math.PI / 2);

        // Straight along +x from the listener is ahead of a camera looking along +x.
        Assert.Equal(0, CameraFrame.Bearing(pose, 900, 500), 6);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(Math.PI / 2, -1)]
    [InlineData(-Math.PI / 2, 1)]
    [InlineData(Math.PI, 0)]
    public void PanIsLeftNegative(double bearing, double pan) => Assert.Equal(pan, CameraFrame.Pan(bearing), 6);

    [Fact]
    public void TheCamerasHeadingAndPitchComeFromItsEyeToItsLookAt()
    {
        // Eye 100 above and 100 behind the look-at point along +z: looking along +z, 45° down.
        var (heading, pitch) = CameraFrame.Camera((0, 250, -100), (0, 150, 0));

        Assert.Equal(0, heading!.Value, 6);
        Assert.Equal(-Math.PI / 4, pitch!.Value, 6);
    }

    [Fact]
    public void ACameraLookingStraightDownHasAPitchButNoHeading()
    {
        var (heading, pitch) = CameraFrame.Camera((10, 500, 10), (10, 100, 10));

        Assert.Null(heading);
        Assert.Equal(-Math.PI / 2, pitch!.Value, 6);
    }

    [Fact]
    public void ACameraWhoseEyeIsItsLookAtIsUnread() =>
        Assert.Equal((null, null), CameraFrame.Camera((1, 2, 3), (1, 2, 3)));

    [Fact]
    public void AViewsPoseCarriesTheCamera()
    {
        var view = new FieldView(1, 2, 3, 0.5, [], CameraHeading: 1.5, CameraPitch: -0.25);

        Assert.Equal(new FieldPose(1, 2, 3, 0.5, 1.5, -0.25), view.Pose);
    }
}
