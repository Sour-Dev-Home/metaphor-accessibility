using System.Buffers.Binary;
using Metaphor.Core.Cues;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>One entry in <see cref="SoundLibrary"/> (#906, #651): what it is called, what it means in short on focus,
/// and the fuller description (meaning and sound) on the details key.</summary>
public sealed record SoundLibraryEntry(
    string Spoken, string Meaning, string Description, Cue? Cue = null, ToneChannel? Channel = null,
    string? SampleFile = null, IReadOnlyList<ToneStep>? FallbackSteps = null, double Pan = 0, int Volume = 90);

/// <summary>A sound library section in the mod's settings menu (#906, #651): lists every mod sound cue (<see cref="Cue"/>
/// and the field navigation sounds from <c>docs/players/reference/sound-cues.md</c>) and plays it on focus, and again on
/// Enter, A, Left or Right, with its spoken name and what it means, so players learn the sounds outside play.
/// <para>Idea from Coswars (MERLIN), a blind-accessible shooter, coswars.com, its public blog pages (read 2026-11-24);
/// idea only, no code published or read (CREDITS.md).</para></summary>
[FeatureSetting<MenusFeature>(MenusFeature.SoundLibrary)]
public sealed class SoundLibrary
{
    /// <summary>The settings menu section heading.</summary>
    public const string Section = "Sound library";

    private readonly ICuePlayer cues;
    private readonly Func<SoundBank> bank;
    private readonly Lock gate = new();
    private readonly Dictionary<string, CueSample> fallbackSamples = new(StringComparer.Ordinal);
    private ToneChannel? activeChannel;

    /// <param name="cues">The feature's gated cue player.</param>
    /// <param name="bank">The generated navigation WAV files from <c>data/sounds</c>, loaded when first needed.</param>
    public SoundLibrary(ICuePlayer cues, Func<SoundBank>? bank = null)
    {
        ArgumentNullException.ThrowIfNull(cues);
        this.cues = cues;
        this.bank = bank ?? (() => SoundBank.Empty);
    }

    /// <summary>The library entry for a <see cref="Cue"/> (#651, #906). Throws for any unmapped cue so a test catches a
    /// new <see cref="Cue"/> member missing from the library.</summary>
    public static SoundLibraryEntry ForCue(Cue cue) => cue switch
    {
        Cue.AllyHit => new(
            "Ally hit",
            "someone in your party was hit",
            "Someone in your party was hit, just before the damage number is read. Two low notes going down.",
            Cue: Cue.AllyHit),
        Cue.EnemyHit => new(
            "Enemy hit",
            "an enemy was hit",
            "An enemy was hit, just before the damage number is read. Two high notes going up, quicker and brighter.",
            Cue: Cue.EnemyHit),
        Cue.Mark => new(
            "Mark",
            "the mark key wrote a marker to the report log",
            "You pressed the mark key, F8, and the mod wrote a marker to its report log. Two short high clicks on the same note.",
            Cue: Cue.Mark),
        Cue.EnemyNoticed => new(
            "Enemy noticed",
            "a field enemy near you noticed you",
            "A field enemy near you noticed you. Two quick high notes leaping up, from the enemy's side.",
            Cue: Cue.EnemyNoticed),
        Cue.EnemyGaveUp => new(
            "Enemy gave up",
            "a field enemy gave up the search",
            "A field enemy that noticed you gave up the search. Three lower notes falling, from the enemy's side.",
            Cue: Cue.EnemyGaveUp),
        _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, $"Cue.{cue} has no SoundLibrary entry"),
    };

    /// <summary>Every cue in the library: all <see cref="Cue"/> members first, then the field navigation sounds.</summary>
    public static IReadOnlyList<SoundLibraryEntry> Entries { get; } =
    [
        .. Enum.GetValues<Cue>().Select(ForCue),
        new(
            "Guidance beep",
            "points toward what the mod is guiding you to",
            "The way to what you asked the mod to guide you to in the field: from the side it is on, high ahead and low behind, faster as you near it.",
            Channel: ToneChannel.Guidance,
            FallbackSteps: [new ToneStep(660, 90)],
            Volume: 85),
        new(
            "Sonar: person",
            "a person near you in the field",
            "A person near you in the field: a middle note from the side they are on, louder when nearer.",
            Channel: ToneChannel.Sonar,
            SampleFile: "sonar-people.wav",
            FallbackSteps: [new ToneStep(659, 70)]),
        new(
            "Sonar: object",
            "an object, chest or examine spot near you",
            "An object, chest or examine spot near you in the field: a high note from the side it is on, louder when nearer.",
            Channel: ToneChannel.Sonar,
            SampleFile: "sonar-objects.wav",
            FallbackSteps: [new ToneStep(988, 70)]),
        new(
            "Sonar: exit",
            "a door or exit near you",
            "A door or exit near you in the field: a low note from the side it is on, louder when nearer.",
            Channel: ToneChannel.Sonar,
            SampleFile: "sonar-exits.wav",
            FallbackSteps: [new ToneStep(330, 70)]),
        new(
            "Wall ahead",
            "a wall ahead of you in the field",
            "A wall or walkable edge ahead of you in the field: the highest of the four steady wall tones, louder as it nears.",
            Channel: ToneChannel.Wall,
            SampleFile: "wall-ahead.wav",
            FallbackSteps: [new ToneStep(262, 350)],
            Volume: 50),
        new(
            "Wall left",
            "a wall to your left in the field",
            "A wall or walkable edge to your left in the field: a low steady tone on the left, louder as it nears.",
            Channel: ToneChannel.WallLeft,
            SampleFile: "wall-left.wav",
            FallbackSteps: [new ToneStep(196, 350)],
            Pan: -1,
            Volume: 50),
        new(
            "Wall right",
            "a wall to your right in the field",
            "A wall or walkable edge to your right in the field: a low steady tone on the right, louder as it nears.",
            Channel: ToneChannel.WallRight,
            SampleFile: "wall-right.wav",
            FallbackSteps: [new ToneStep(196, 350)],
            Pan: 1,
            Volume: 50),
        new(
            "Wall behind",
            "a wall behind you in the field",
            "A wall or walkable edge behind you in the field: the lowest of the four steady wall tones, louder as it nears.",
            Channel: ToneChannel.WallBehind,
            SampleFile: "wall-behind.wav",
            FallbackSteps: [new ToneStep(131, 350)],
            Volume: 50),
        new(
            "Route ping: straight",
            "a straight path to the nearby place",
            "The way to the place you stepped to in the nearby list when the route runs straight: two quick high notes.",
            Channel: ToneChannel.Route,
            SampleFile: "route-straight.wav",
            FallbackSteps: [new ToneStep(1319, 50), new ToneStep(0, 30), new ToneStep(1319, 50)]),
        new(
            "Route ping: around a corner",
            "a path that bends around a corner",
            "The way to the place you stepped to in the nearby list when the route bends: two rising notes from the next corner.",
            Channel: ToneChannel.Route,
            SampleFile: "route-around.wav",
            FallbackSteps: [new ToneStep(784, 60), new ToneStep(1175, 60)]),
        new(
            "Route ping: no path",
            "no walking path found to the place",
            "When the walking map holds no path to the place you stepped to in the nearby list: one low note.",
            Channel: ToneChannel.Route,
            SampleFile: "route-nopath.wav",
            FallbackSteps: [new ToneStep(220, 150)]),
    ];

    /// <summary>The rows to append to <see cref="SettingsMenu"/> while <see cref="MenusFeature.SoundLibrary"/> is on.
    /// </summary>
    public IReadOnlyList<PlayerSetting> Rows() =>
        [.. Entries.Select(entry => (PlayerSetting)new Row(this, entry))];

    /// <summary>Plays <paramref name="entry"/> once.</summary>
    public void Play(SoundLibraryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Quiet();
        if (entry.Cue is { } cue)
        {
            cues.Play(cue);
            return;
        }
        if (entry.Channel is not { } channel)
        {
            return;
        }
        var sample = (entry.SampleFile is { } file ? bank().Find(file) : null) ?? SampleFor(entry);
        var hertz = entry.FallbackSteps is { Count: > 0 } steps && steps[0].Hertz > 0 ? steps[0].Hertz : 440;
        lock (gate)
        {
            activeChannel = channel;
        }
        cues.Hold(channel, new HeldTone(hertz, entry.Pan, entry.Volume, sample, Loops: false, Broadband: true));
    }

    /// <summary>Silences any preview channel currently playing.</summary>
    public void Quiet()
    {
        ToneChannel? channel;
        lock (gate)
        {
            channel = activeChannel;
            activeChannel = null;
        }
        if (channel is { } active)
        {
            cues.Silence(active);
        }
    }

    private CueSample? SampleFor(SoundLibraryEntry entry)
    {
        if (entry.FallbackSteps is not { Count: > 0 } steps)
        {
            return null;
        }
        lock (gate)
        {
            if (fallbackSamples.TryGetValue(entry.Spoken, out var cached))
            {
                return cached;
            }
            var rendered = CueWave.Samples(new CueTone(steps), 100);
            var wav = MonoWav(rendered);
            if (CueSample.Parse(entry.Spoken, wav, out _) is { } parsed)
            {
                fallbackSamples[entry.Spoken] = parsed;
                return parsed;
            }
            return null;
        }
    }

    private static byte[] MonoWav(ReadOnlySpan<short> samples)
    {
        var dataBytes = samples.Length * 2;
        var wav = new byte[44 + dataBytes];
        "RIFF"u8.CopyTo(wav);
        BinaryPrimitives.WriteUInt32LittleEndian(wav.AsSpan(4), (uint)(36 + dataBytes));
        "WAVEfmt "u8.CopyTo(wav.AsSpan(8));
        BinaryPrimitives.WriteUInt32LittleEndian(wav.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(wav.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(wav.AsSpan(22), 1);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(24), CueWave.SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(28), CueWave.SampleRate * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(wav.AsSpan(32), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(wav.AsSpan(34), 16);
        "data"u8.CopyTo(wav.AsSpan(36));
        BinaryPrimitives.WriteUInt32LittleEndian(wav.AsSpan(40), (uint)dataBytes);
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(44 + (i * 2)), samples[i]);
        }
        return wav;
    }

    private sealed record Row(SoundLibrary Library, SoundLibraryEntry Entry)
        : PlayerSetting(SoundLibrary.Section, Entry.Spoken, Entry.Description), IPreviewRow
    {
        public override string Value(ModSettings settings) => Entry.Meaning;

        public override SettingChange? Change(ModSettings settings, int steps) => null;

        public void Preview() => Library.Play(Entry);
    }
}

/// <summary>A row in <see cref="SettingsMenu"/> that plays a preview sound when focused or activated (#906).</summary>
internal interface IPreviewRow
{
    void Preview();
}
