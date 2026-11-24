using System.Buffers.Binary;
using Metaphor.Core.Cues;

namespace Metaphor.Core.Tests.Cues;

/// <summary>Sound files for the cue mixer (#841): a WAV file is read as mono scaled to a peak of 1, a stereo one folded
/// back along its pan, and a malformed one refused with the reason, never thrown; a folder of them loads into a bank.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CueSampleTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "metaphor sounds ü " + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AMonoFileIsScaledSoItsLoudestSampleIsOne()
    {
        var sample = CueSample.Parse("a.wav", TestWav.Pcm([0, 5000, -10000, 2500]), out var problem);

        Assert.Null(problem);
        Assert.NotNull(sample);
        Assert.Equal("a.wav", sample.Name);
        Assert.Equal([0f, 0.5f, -1f, 0.25f], sample.Samples.ToArray());
    }

    [Fact]
    public void AStereoFileWithAPanBakedInFoldsBackToTheMonoSound()
    {
        var mono = TestWav.Sine(441);
        var (left, right) = CueWave.PanGains(-0.6);
        var stereo = mono.SelectMany(s => new[] { (short)Math.Round(s * left), (short)Math.Round(s * right) }).ToArray();

        var folded = CueSample.Parse("w.wav", TestWav.Pcm(stereo, channels: 2), out _)!;
        var plain = TestWav.Sample(mono);

        Assert.Equal(mono.Length, folded.Samples.Length);
        for (var i = 0; i < mono.Length; i++)
        {
            Assert.InRange(folded.Samples[i] - plain.Samples[i], -0.001f, 0.001f);
        }
    }

    [Fact]
    public void ItsLengthIsInWholeMillisecondsAndUnknownChunksAreSkipped()
    {
        // A "LIST" chunk of odd length, padded, before the data.
        var list = new byte[8 + 3 + 1];
        "LIST"u8.CopyTo(list);
        BinaryPrimitives.WriteInt32LittleEndian(list.AsSpan(4), 3);

        var sample = CueSample.Parse("x.wav", TestWav.Pcm(TestWav.Sine(4410), extraChunk: list), out var problem);

        Assert.Null(problem);
        Assert.Equal(100, sample!.Milliseconds);
    }

    public static TheoryData<string, byte[]> Malformed()
    {
        var good = TestWav.Pcm(TestWav.Sine(100));
        var truncated = good[..^20];
        var noData = good[..36];
        var notRiff = (byte[])good.Clone();
        notRiff[0] = (byte)'X';
        return new()
        {
            { "not a WAV file", notRiff },
            { "not a WAV file", [] },
            { "past the end", truncated },
            { "no sound data", noData },
            { "not plain PCM", TestWav.Pcm(TestWav.Sine(100), format: 3) },
            { "22050 Hz", TestWav.Pcm(TestWav.Sine(100), rate: 22050) },
            { "8 bits", TestWav.Pcm(TestWav.Sine(100), bits: 8) },
            { "6 channels", TestWav.Pcm(TestWav.Sine(102), channels: 6) },
            { "holds no sound", TestWav.Pcm([]) },
            { "silent", TestWav.Pcm(new short[100]) },
            { "longer than 10 seconds", TestWav.Pcm(new short[(CueSample.MaxMilliseconds * CueWave.SampleRate / 1000) + 1]) },
        };
    }

    [Theory]
    [MemberData(nameof(Malformed))]
    public void AMalformedFileIsRefusedWithItsReason(string reason, byte[] wav)
    {
        var sample = CueSample.Parse("bad.wav", wav, out var problem);

        Assert.Null(sample);
        Assert.Contains(reason, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ABankLoadsTheGoodFilesAndSaysEachBadOne()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "good.wav"), TestWav.Pcm(TestWav.Sine(441)));
        File.WriteAllBytes(Path.Combine(folder, "bad.wav"), [1, 2, 3]);
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "not a sound");
        var problems = new List<string>();

        var bank = SoundBank.Load(folder, problems.Add);

        Assert.Equal(1, bank.Count);
        Assert.NotNull(bank.Find("good.wav"));
        Assert.NotNull(bank.Find("GOOD.WAV"));
        Assert.Null(bank.Find("bad.wav"));
        Assert.Equal(["bad.wav: it is not a WAV file, left out"], problems);
    }

    [Fact]
    public void AMissingFolderIsAnEmptyBank()
    {
        var problems = new List<string>();

        var bank = SoundBank.Load(folder, problems.Add);

        Assert.Equal(0, bank.Count);
        Assert.Null(bank.Find("good.wav"));
        Assert.Empty(problems);
    }
}
