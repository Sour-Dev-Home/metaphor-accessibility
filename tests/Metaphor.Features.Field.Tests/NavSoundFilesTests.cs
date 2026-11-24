using System.Buffers.Binary;
using Metaphor.NavSounds;

namespace Metaphor.Features.Field.Tests;

/// <summary>The generated navigation sounds (#839): the committed files in <c>data/sounds/</c> are exactly what the
/// generator writes, so a hand edit, a stray file or a changed pitch without new files fails the checks; and the
/// generator gives the same bytes every time.</summary>
[Trait("Category", "Unit")]
public sealed class NavSoundFilesTests
{
    [Fact]
    public void TheCommittedFilesAreWhatTheGeneratorWrites()
    {
        var folder = Path.Combine(RepositoryRoot(), NavSoundSet.RelativeFolder);
        var sounds = NavSoundSet.Render();

        Assert.Equal(
            sounds.Select(sound => sound.Name),
            Directory.GetFiles(folder).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.All(sounds, sound => Assert.True(
            File.ReadAllBytes(Path.Combine(folder, sound.Name)).AsSpan().SequenceEqual(sound.Wav),
            $"{sound.Name} is stale: {NavSoundSet.Regenerate}"));
    }

    [Fact]
    public void TheSameParametersGiveTheSameBytes()
    {
        var first = NavSoundSet.Render();
        var second = NavSoundSet.Render();

        Assert.Equal(first.Select(sound => sound.Name), second.Select(sound => sound.Name));
        Assert.All(first.Zip(second), pair => Assert.Equal(pair.First.Wav, pair.Second.Wav));
    }

    [Fact]
    public void EveryKindTheKitUsesHasAFile()
    {
        var names = NavSoundSet.Render().Select(sound => sound.Name).ToHashSet();

        Assert.All(Enum.GetValues<SonarKind>(), kind => Assert.Contains($"sonar-{kind.ToString().ToLowerInvariant()}.wav", names));
        Assert.All(Enum.GetValues<WallDirection>(), direction => Assert.Contains($"wall-{direction.ToString().ToLowerInvariant()}.wav", names));
        Assert.All(Enum.GetValues<RoutePingKind>(), kind => Assert.Contains($"route-{kind.ToString().ToLowerInvariant()}.wav", names));
        Assert.Contains("sight-zone.wav", names);
    }

    [Fact]
    public void EachFileIsSixteenBitStereoAtTheCuePlayersRate()
    {
        Assert.All(NavSoundSet.Render(), sound =>
        {
            var wav = sound.Wav.AsSpan();
            Assert.True(wav[..4].SequenceEqual("RIFF"u8) && wav[8..16].SequenceEqual("WAVEfmt "u8), sound.Name);
            Assert.Equal(2, BinaryPrimitives.ReadInt16LittleEndian(wav[22..]));
            Assert.Equal(Synth.SampleRate, BinaryPrimitives.ReadInt32LittleEndian(wav[24..]));
            Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(wav[34..]));
            Assert.Equal(wav.Length - 44, BinaryPrimitives.ReadInt32LittleEndian(wav[40..]));
        });
    }

    [Fact]
    public void ASonarFileLastsAsLongAsTheGamesPing()
    {
        var people = NavSoundSet.Render().Single(sound => sound.Name == "sonar-people.wav");

        Assert.Equal(Sonar.PingMilliseconds * Synth.SampleRate / 1000 * 4, people.Wav.Length - 44);
    }

    [Fact]
    public void AWallFileIsPannedToItsSide()
    {
        var left = NavSoundSet.Render().Single(sound => sound.Name == "wall-left.wav").Wav;

        // A quarter of the way into the first cycle the left channel is loud and the right silent.
        var frame = 44 + (Synth.SampleRate / (int)WallTones.Hertz(WallDirection.Left) / 4 * 4);
        Assert.True(BinaryPrimitives.ReadInt16LittleEndian(left.AsSpan(frame)) > 1000);
        Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(left.AsSpan(frame + 2)));
    }

    [Fact]
    public void TheKitsSoundsReachTwelveKilohertzEachFamilyInItsOwnColour()
    {
        // #839: a pure sine has nothing above its pitch for the ear or the behind-muffle (#903) to work on.
        var sounds = NavSoundSet.Render().ToDictionary(sound => sound.Name, sound => Left(sound.Wav));
        var people = sounds["sonar-people.wav"];
        var wall = sounds["wall-ahead.wav"];
        var peopleHertz = Sonar.Hertz(SonarKind.People);
        var wallHertz = WallTones.Hertz(WallDirection.Ahead);

        // The sonar is bright: every harmonic, the 15th (near 10 kHz) at about 1/15.
        Assert.InRange(Amplitude(people, peopleHertz * 15) / Amplitude(people, peopleHertz), 0.04, 0.1);
        Assert.InRange(Amplitude(people, peopleHertz * 2) / Amplitude(people, peopleHertz), 0.4, 0.6);
        // The walls are hollow: odd harmonics only, the 45th (near 11.8 kHz) still there.
        Assert.InRange(Amplitude(wall, wallHertz * 45) / Amplitude(wall, wallHertz), 0.015, 0.03);
        Assert.InRange(Amplitude(wall, wallHertz * 2) / Amplitude(wall, wallHertz), 0, 0.005);
        Assert.All(sounds.Values, samples => Assert.DoesNotContain(samples, s => Math.Abs((int)s) >= short.MaxValue));
    }

    [Fact]
    public void ARoutePingIsStruckANoiseBurstAtEachNotesStartOnly()
    {
        var steps = RoutePings.Steps(RoutePingKind.Straight);
        var plain = Synth.Notes(steps, 90, Timbre.Bright);
        var struck = Synth.Notes(steps, 90, Timbre.Bright, struck: true, seed: 7);
        var strike = 12 * Synth.SampleRate / 1000;
        var starts = new List<int>();
        var at = 0;
        foreach (var step in steps)
        {
            if (step.Hertz > 0)
            {
                starts.Add(at);
            }
            at += step.Milliseconds * Synth.SampleRate / 1000;
        }

        var changed = Enumerable.Range(0, plain.Length).Where(i => plain[i] != struck[i]).ToList();
        Assert.All(changed, i => Assert.Contains(starts, start => i >= start && i < start + strike));
        Assert.All(starts, start => Assert.Contains(changed, i => i >= start && i < start + strike));
        Assert.Equal(struck, Synth.Notes(steps, 90, Timbre.Bright, struck: true, seed: 7));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.1)]
    [InlineData(0.25)]
    [InlineData(0.4)]
    [InlineData(0.75)]
    [InlineData(123.456)]
    [InlineData(-2.3)]
    public void TheGeneratorsSineIsTheSine(double turns) =>
        Assert.Equal(Math.Sin(2 * Math.PI * turns), Synth.Sin(turns), 10);

    // A file's left channel.
    private static short[] Left(byte[] wav) =>
        [.. Enumerable.Range(0, (wav.Length - 44) / 4).Select(i => BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(44 + (i * 4))))];

    // The amplitude of one frequency in the samples, by correlation with its sine and cosine.
    private static double Amplitude(short[] samples, double hertz)
    {
        var (sin, cos) = (0.0, 0.0);
        for (var i = 0; i < samples.Length; i++)
        {
            var angle = 2 * Math.PI * hertz * i / Synth.SampleRate;
            sin += samples[i] * Math.Sin(angle);
            cos += samples[i] * Math.Cos(angle);
        }
        return 2 * Math.Sqrt((sin * sin) + (cos * cos)) / samples.Length;
    }

    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Metaphor.sln")))
            {
                return folder.FullName;
            }
        }
        throw new DirectoryNotFoundException("no Metaphor.sln above the test folder");
    }
}
