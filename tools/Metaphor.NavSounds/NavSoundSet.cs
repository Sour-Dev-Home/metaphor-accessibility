using Metaphor.Core.Cues;
using Metaphor.Features.Field;

namespace Metaphor.NavSounds;

/// <summary>One generated sound: its file name under <see cref="NavSoundSet.RelativeFolder"/> and its bytes.</summary>
public sealed record NavSound(string Name, byte[] Wav);

/// <summary>Every navigation sound the field kit uses (#839), generated from the field feature's own pitches, lengths,
/// pans and default volumes, so each file is what the game plays: the sonar's kinds (#827), the four wall-tone loops
/// (#830) and the route pings (#831). The cue player holds only sine tones, so the mod makes these sounds itself at run
/// time and doesn't load the files; they are for listening outside the game and are the files a sample player would
/// load. The three sight sounds stand in for the cues Arcana's <c>sight/</c> set covered and nothing plays them yet.
/// <para>The idea of generating the kit with a script, and the sight sounds' descriptions ("dense steady sizzle" and
/// "dense irregular impulses, 3 ms bursts"), come from Grim Dark's <c>tools/gen_hazard_cues.py</c> (Austin Hicks,
/// https://github.com/ahicks92/grimdark, Zlib, commit <c>1cbe1732e9</c>), as read in
/// docs/raw-sources/arcana-audio-origins-2026-11-23.md; idea only, no code copied, the script never run
/// (CREDITS.md).</para></summary>
public static class NavSoundSet
{
    /// <summary>Where the files go, from the repository root: the mod's data folder.</summary>
    public const string RelativeFolder = "data/sounds";

    /// <summary>How to bring the files up to date.</summary>
    public const string Regenerate =
        "run `dotnet run --project tools/Metaphor.NavSounds -c Release -v q` from the repository root and commit " +
        RelativeFolder;

    /// <summary>The seed of the sight sounds' noise; the same seed gives the same bytes.</summary>
    public const uint Seed = 839;

    /// <summary>Every sound, in name order.</summary>
    public static IReadOnlyList<NavSound> Render()
    {
        var sounds = new List<NavSound>();
        foreach (var kind in Enum.GetValues<SonarKind>())
        {
            var note = new ToneStep(Sonar.Hertz(kind), (int)Sonar.PingMilliseconds);
            sounds.Add(new($"sonar-{Lower(kind)}.wav", Synth.Wav(Synth.Notes([note], Sonar.NearVolume), 0)));
        }
        foreach (var direction in Enum.GetValues<WallDirection>())
        {
            var loop = Synth.Loop((int)WallTones.Hertz(direction), 1, WallLimits.Default.Volume);
            var pan = CameraFrame.Pan(WallTones.Bearing(direction));
            sounds.Add(new($"wall-{Lower(direction)}.wav", Synth.Wav(loop, Math.Round(pan, 12))));
        }
        foreach (var kind in Enum.GetValues<RoutePingKind>())
        {
            var notes = Synth.Notes(RoutePings.Steps(kind), RoutePingLimits.Default.Volume);
            sounds.Add(new($"route-{Lower(kind)}.wav", Synth.Wav(notes, 0)));
        }
        // A steady sizzle pitched at 260 Hz, 80 bursts a second, on either side; and irregular bursts, about 35 a
        // second, in the centre.
        var hidden = Synth.Bursts(Seed, 3, 1000.0 / 80, 0, 260, 60);
        sounds.Add(new("sight-hidden-left.wav", Synth.Wav(hidden, -1)));
        sounds.Add(new("sight-hidden-right.wav", Synth.Wav(hidden, 1)));
        sounds.Add(new("sight-zone.wav", Synth.Wav(Synth.Bursts(Seed + 1, 3, 1000.0 / 35, 0.5, 0, 60), 0)));
        return [.. sounds.OrderBy(sound => sound.Name, StringComparer.Ordinal)];
    }

    private static string Lower(Enum value) => value.ToString().ToLowerInvariant();
}
