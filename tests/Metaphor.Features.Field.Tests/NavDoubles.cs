using Metaphor.Core.Announcements;
using Metaphor.Core.Cues;

namespace Metaphor.Features.Field.Tests;

/// <summary>A stand-in field for the navigation sounds' tests (#830, #831): a pose, units and a map, all set by the test.
/// </summary>
internal sealed class NavField : IFieldReads
{
    public bool Roaming { get; set; } = true;

    public FieldPose? Pose { get; set; } = new(0, 0, 0, 0, CameraHeading: 0);

    public IReadOnlyList<FieldUnit> Units { get; set; } = [];

    public int? Map { get; set; } = 1;

    public bool InFreeRoam() => Roaming;

    public FieldView? Read() => Pose is { } pose
        ? new(pose.X, pose.Y, pose.Z, pose.Heading, Units, MapId: Map, CameraHeading: pose.CameraHeading)
        : null;

    public int? MapId() => Map;

    public FieldPose? ReadPose() => Pose;
}

/// <summary>Records every held tone and silence, by channel.</summary>
internal sealed class ChannelCues : ICuePlayer
{
    public List<(ToneChannel Channel, HeldTone? Tone)> Calls { get; } = [];

    /// <summary>What each channel holds now: its last tone, or null once silenced.</summary>
    public HeldTone? Now(ToneChannel channel) => Calls.LastOrDefault(call => call.Channel == channel).Tone;

    public IEnumerable<HeldTone> Held(ToneChannel channel) =>
        Calls.Where(call => call.Channel == channel && call.Tone is not null).Select(call => call.Tone!.Value);

    public void Play(Cue cue)
    {
    }

    public void Play(Cue cue, double pan)
    {
    }

    public void Hold(ToneChannel channel, HeldTone tone) => Calls.Add((channel, tone));

    public void Silence(ToneChannel channel) => Calls.Add((channel, null));
}

/// <summary>Records what is said.</summary>
internal sealed class SaidList : IAnnouncer
{
    public List<Announcement> Said { get; } = [];

    public void Announce(Announcement announcement) => Said.Add(announcement);
}
