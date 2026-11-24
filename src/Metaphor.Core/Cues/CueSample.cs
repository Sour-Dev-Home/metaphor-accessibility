using System.Buffers.Binary;
using System.Globalization;

namespace Metaphor.Core.Cues;

/// <summary>A recorded sound a held tone plays in place of its sine (#841): mono at <see cref="CueWave.SampleRate"/>,
/// scaled so its loudest sample is 1, so the mixer gives it the same peak the sine would have. It is read from a WAV
/// file: 16-bit PCM, one or two channels, at <see cref="CueWave.SampleRate"/>, at most <see cref="MaxMilliseconds"/>
/// long. A stereo file is folded to mono along its own balance, so a file made with a pan baked in (as
/// <c>tools/Metaphor.NavSounds</c> writes them) gives back the mono sound, and the mixer places it afresh.</summary>
public sealed class CueSample
{
    /// <summary>The longest sound read; a longer file is refused.</summary>
    public const int MaxMilliseconds = 10_000;

    private readonly float[] samples;

    private CueSample(string name, float[] samples)
    {
        Name = name;
        this.samples = samples;
    }

    /// <summary>The file name it was read from.</summary>
    public string Name { get; }

    /// <summary>Its mono samples, from -1 to 1.</summary>
    public ReadOnlySpan<float> Samples => samples;

    /// <summary>How long it plays, rounded up to a whole millisecond.</summary>
    public int Milliseconds => (int)Math.Ceiling(samples.Length * 1000.0 / CueWave.SampleRate);

    /// <summary>The sound in <paramref name="wav"/>, a WAV file's bytes, named <paramref name="name"/>; or null, with
    /// what is wrong with it in <paramref name="problem"/>.</summary>
    public static CueSample? Parse(string name, ReadOnlySpan<byte> wav, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(name);
        problem = Check(wav, out var channels, out var data);
        if (problem is not null)
        {
            return null;
        }
        var frames = data.Length / (2 * channels);
        if (frames == 0)
        {
            problem = "it holds no sound";
            return null;
        }
        if (frames > (long)MaxMilliseconds * CueWave.SampleRate / 1000)
        {
            problem = string.Create(CultureInfo.InvariantCulture, $"it is longer than {MaxMilliseconds / 1000} seconds");
            return null;
        }
        var mono = Fold(data, channels, frames);
        var peak = mono.Max(sample => Math.Abs(sample));
        if (peak == 0)
        {
            problem = "it is silent";
            return null;
        }
        for (var i = 0; i < mono.Length; i++)
        {
            mono[i] /= peak;
        }
        return new CueSample(name, mono);
    }

    // The RIFF layout: a "fmt " chunk for 16-bit PCM at the cue rate and a "data" chunk, each inside the file. Returns
    // what is wrong, or null.
    private static string? Check(ReadOnlySpan<byte> wav, out int channels, out ReadOnlySpan<byte> data)
    {
        channels = 0;
        data = default;
        if (wav.Length < 12 || !wav[..4].SequenceEqual("RIFF"u8) || !wav[8..12].SequenceEqual("WAVE"u8))
        {
            return "it is not a WAV file";
        }
        var format = false;
        var offset = 12;
        while (offset + 8 <= wav.Length)
        {
            var id = wav.Slice(offset, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(wav[(offset + 4)..]);
            if (size > (uint)(wav.Length - offset - 8))
            {
                return "a chunk runs past the end of the file";
            }
            var body = wav.Slice(offset + 8, (int)size);
            if (id.SequenceEqual("fmt "u8))
            {
                if (body.Length < 16 || BinaryPrimitives.ReadUInt16LittleEndian(body) != 1)
                {
                    return "it is not plain PCM";
                }
                channels = BinaryPrimitives.ReadUInt16LittleEndian(body[2..]);
                var rate = BinaryPrimitives.ReadInt32LittleEndian(body[4..]);
                var bits = BinaryPrimitives.ReadUInt16LittleEndian(body[14..]);
                if (channels is not (1 or 2) || rate != CueWave.SampleRate || bits != 16)
                {
                    return string.Create(CultureInfo.InvariantCulture,
                        $"it is {channels} channels at {rate} Hz and {bits} bits, not 16-bit mono or stereo at {CueWave.SampleRate} Hz");
                }
                format = true;
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (!format)
                {
                    return "its sound comes before its format";
                }
                data = body;
                return null;
            }
            // Chunks are padded to an even length.
            offset += 8 + (int)size + (int)(size & 1);
        }
        return format ? "it has no sound data" : "it has no format";
    }

    // One channel as it is; two folded along the file's balance: with each side's share of the energy as the gains of
    // an equal-power pan, left × its gain + right × its gain is the sound before the pan.
    private static float[] Fold(ReadOnlySpan<byte> data, int channels, int frames)
    {
        var mono = new float[frames];
        if (channels == 1)
        {
            for (var i = 0; i < frames; i++)
            {
                mono[i] = BinaryPrimitives.ReadInt16LittleEndian(data[(i * 2)..]);
            }
            return mono;
        }
        double left = 0, right = 0;
        for (var i = 0; i < frames; i++)
        {
            double l = BinaryPrimitives.ReadInt16LittleEndian(data[(i * 4)..]);
            double r = BinaryPrimitives.ReadInt16LittleEndian(data[((i * 4) + 2)..]);
            left += l * l;
            right += r * r;
        }
        var total = left + right;
        var (leftGain, rightGain) = total == 0 ? (Math.Sqrt(0.5), Math.Sqrt(0.5)) : (Math.Sqrt(left / total), Math.Sqrt(right / total));
        for (var i = 0; i < frames; i++)
        {
            mono[i] = (float)((BinaryPrimitives.ReadInt16LittleEndian(data[(i * 4)..]) * leftGain) +
                (BinaryPrimitives.ReadInt16LittleEndian(data[((i * 4) + 2)..]) * rightGain));
        }
        return mono;
    }
}

/// <summary>The sound files in one folder (#841), read once: each <c>.wav</c> directly in it that <see
/// cref="CueSample.Parse"/> accepts, by file name. A file that can't be read or is malformed is left out and said, so
/// whatever would play it plays its tone instead.</summary>
public sealed class SoundBank
{
    /// <summary>The largest file read; a larger one is left out without being read.</summary>
    public const long MaxFileBytes = 4 * 1024 * 1024;

    private readonly Dictionary<string, CueSample> sounds;

    private SoundBank(Dictionary<string, CueSample> sounds) => this.sounds = sounds;

    /// <summary>A bank with no sounds.</summary>
    public static SoundBank Empty { get; } = new(new(StringComparer.OrdinalIgnoreCase));

    /// <summary>How many sounds it holds.</summary>
    public int Count => sounds.Count;

    /// <summary>The sounds in <paramref name="folder"/>; empty when it doesn't exist. Each file left out is said to
    /// <paramref name="problem"/> with its name and why.</summary>
    public static SoundBank Load(string folder, Action<string> problem)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(problem);
        var found = new Dictionary<string, CueSample>(StringComparer.OrdinalIgnoreCase);
        string[] files;
        try
        {
            files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.wav") : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            problem($"{folder}: the folder can't be read ({e.Message})");
            return Empty;
        }
        foreach (var path in files.Order(StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(path);
            try
            {
                if (new FileInfo(path).Length > MaxFileBytes)
                {
                    problem($"{name}: larger than {MaxFileBytes / (1024 * 1024)} MB, left out");
                    continue;
                }
                if (CueSample.Parse(name, File.ReadAllBytes(path), out var why) is { } sample)
                {
                    found[name] = sample;
                }
                else
                {
                    problem($"{name}: {why}, left out");
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                problem($"{name}: can't be read ({e.Message}), left out");
            }
        }
        return new SoundBank(found);
    }

    /// <summary>The sound read from the file <paramref name="name"/>, or null when there is none.</summary>
    public CueSample? Find(string name) => sounds.GetValueOrDefault(name);
}
