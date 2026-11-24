using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>The camera-tilt warning's limits (#828), in degrees from level: past <paramref name="DownDegrees"/> looking
/// down or <paramref name="UpDegrees"/> looking up the camera counts as tilted, and it counts as level again only
/// <paramref name="MarginDegrees"/> back inside, so a camera resting on a limit doesn't chatter.</summary>
public readonly record struct TiltLimits(int DownDegrees, int UpDegrees, int MarginDegrees)
{
    /// <summary>The defaults, the mod's own guess until a by-ear session tunes them: the field camera's usual tilt isn't
    /// measured yet [NEEDS VERIFICATION].</summary>
    public static TiltLimits Default { get; } = new(40, 20, 5);

    /// <summary>The limits the settings file's <c>limits.field</c> object gives, each kept within 1 to 89 degrees (the
    /// margin 0 to 30), each missing one the default.</summary>
    public static TiltLimits From(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(
            Math.Clamp(settings.Limit("field", "cameraDownDegrees", Default.DownDegrees), 1, 89),
            Math.Clamp(settings.Limit("field", "cameraUpDegrees", Default.UpDegrees), 1, 89),
            Math.Clamp(settings.Limit("field", "cameraMarginDegrees", Default.MarginDegrees), 0, 30));
    }
}

/// <summary>Says when the field camera tilts past a limit (#828): "Camera looking down" or "Camera looking up" once per
/// crossing, "Camera level" once it comes back past the margin, so a blind player knows the screen-relative words and
/// sounds may be off. The tilt is <see cref="FieldPose.CameraPitch"/>, from the camera read of #667. The mod never
/// writes the camera (#823). The field controls call <see cref="Check"/> on every keyboard read; it reads the pose only
/// in free roam and at most once per <see cref="CheckIntervalMilliseconds"/>, and the callout queues behind what is
/// being said. <see cref="Line"/> is the on-demand tilt for "where am I".</summary>
[FeatureSetting<FieldFeature>(FieldFeature.CameraTilt)]
public sealed class TiltWarning
{
    /// <summary>The least time between two reads of the camera: ten a second.</summary>
    public const long CheckIntervalMilliseconds = 100;

    private readonly Lock gate = new();
    private readonly IFieldReads field;
    private readonly FreeRoamGate roam;
    private readonly IAnnouncer announcer;
    private readonly Func<long> milliseconds;
    private readonly Func<TiltLimits> limits;
    private readonly Func<InputDevice>? lastDevice;
    private Tilt state = Tilt.Level;
    private long checkedAt;
    private bool checkedOnce;

    /// <summary>A warning that reads <paramref name="field"/>, speaks through <paramref name="announcer"/>, keeps time by
    /// <paramref name="milliseconds"/> and reads <paramref name="limits"/> on each check, so a changed file applies at
    /// once.</summary>
    public TiltWarning(IFieldReads field, IAnnouncer announcer, Func<long> milliseconds, Func<TiltLimits> limits)
        : this(field, announcer, milliseconds, limits, null)
    {
    }

    /// <summary>As the other constructor, and a crossing line adds "press right stick to recenter" while
    /// <paramref name="lastDevice"/> says the last input was the controller (#828: R3 recenters the field camera,
    /// confirmed by ear 2026-11-23, build 4d936b2). The keyboard's equivalent isn't known, so nothing is added there.
    /// </summary>
    public TiltWarning(
        IFieldReads field, IAnnouncer announcer, Func<long> milliseconds, Func<TiltLimits> limits,
        Func<InputDevice>? lastDevice)
    {
        this.lastDevice = lastDevice;
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(milliseconds);
        ArgumentNullException.ThrowIfNull(limits);
        this.field = field;
        roam = new FreeRoamGate(field);
        this.announcer = announcer;
        this.milliseconds = milliseconds;
        this.limits = limits;
    }

    private enum Tilt
    {
        Level,
        Down,
        Up,
    }

    /// <summary>Reads the camera when a check is due and the player roams freely, and says a crossing. A read that
    /// throws says nothing, so it never reaches the game.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A failed read must never reach the game; it says nothing.")]
    public void Check()
    {
        string? text;
        lock (gate)
        {
            var now = milliseconds();
            if (checkedOnce && now - checkedAt < CheckIntervalMilliseconds)
            {
                return;
            }
            checkedOnce = true;
            checkedAt = now;
            try
            {
                text = roam.Open() && field.ReadPose() is { CameraPitch: { } pitch } ? Cross(Degrees(pitch)) : null;
            }
            catch (Exception)
            {
                return;
            }
        }
        if (text is not null)
        {
            announcer.Announce(new Announcement(text, Interrupt: false));
        }
    }

    /// <summary>The camera's tilt now, for "where am I": "Camera level, 12 degrees down", "Camera looking down, 45
    /// degrees", or "Camera tilt unknown" when it can't be read.</summary>
    public string Line()
    {
        if (field.ReadPose() is not { CameraPitch: { } pitch })
        {
            return "Camera tilt unknown";
        }
        var degrees = Degrees(pitch);
        var limit = limits();
        var size = (int)Math.Round(Math.Abs(degrees));
        if (degrees <= -limit.DownDegrees)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Camera looking down, {size} degrees");
        }
        if (degrees >= limit.UpDegrees)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Camera looking up, {size} degrees");
        }
        return size == 0 ? "Camera level" :
            string.Create(CultureInfo.InvariantCulture, $"Camera level, {size} degrees {(degrees < 0 ? "down" : "up")}");
    }

    private static double Degrees(double radians) => radians * 180 / Math.PI;

    // The new state for a tilt in degrees and what to say for it, or null when it hasn't crossed. Called under the gate.
    private string? Cross(double degrees)
    {
        var limit = limits();
        var next = degrees <= -limit.DownDegrees ? Tilt.Down
            : degrees >= limit.UpDegrees ? Tilt.Up
            : state == Tilt.Down && degrees <= -(limit.DownDegrees - limit.MarginDegrees) ? Tilt.Down
            : state == Tilt.Up && degrees >= limit.UpDegrees - limit.MarginDegrees ? Tilt.Up
            : Tilt.Level;
        if (next == state)
        {
            return null;
        }
        state = next;
        var recenter = lastDevice?.Invoke() == InputDevice.Controller ? ", press right stick to recenter" : "";
        return next switch
        {
            Tilt.Down => "Camera looking down" + recenter,
            Tilt.Up => "Camera looking up" + recenter,
            _ => "Camera level",
        };
    }
}
