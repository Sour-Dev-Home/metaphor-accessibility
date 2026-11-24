using Metaphor.Core.Cues;

namespace Metaphor.Features.Field;

/// <summary>The generated navigation sounds (#841) the sonar, the wall tones and the route pings play in place of their
/// sine tones: the files <c>tools/Metaphor.NavSounds</c> writes to <c>data/sounds/</c>, which ship in the mod folder.
/// Each lookup gives the file's sound only while <see cref="FieldFeature.SoundFiles"/> is on and the file loaded; null
/// otherwise, and the feature then plays its tone as before, so a missing or malformed file is never silence.</summary>
public sealed class NavSamples
{
    private readonly SoundBank bank;
    private readonly Func<bool> on;

    /// <summary>The sounds in <paramref name="bank"/>, played while <paramref name="on"/> says so.</summary>
    public NavSamples(SoundBank bank, Func<bool> on)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(on);
        this.bank = bank;
        this.on = on;
    }

    /// <summary>No sounds: every feature plays its tones.</summary>
    public static NavSamples None { get; } = new(SoundBank.Empty, () => false);

    /// <summary>The file a sonar kind plays: <c>sonar-people.wav</c>, <c>sonar-objects.wav</c>, <c>sonar-exits.wav</c>.
    /// </summary>
    public static string FileName(SonarKind kind) => $"sonar-{Lower(kind)}.wav";

    /// <summary>The loop a wall direction plays: <c>wall-ahead.wav</c> and so on.</summary>
    public static string FileName(WallDirection direction) => $"wall-{Lower(direction)}.wav";

    /// <summary>The file a route ping kind plays: <c>route-straight.wav</c>, <c>route-around.wav</c>,
    /// <c>route-nopath.wav</c>.</summary>
    public static string FileName(RoutePingKind kind) => $"route-{Lower(kind)}.wav";

    /// <summary>The sonar kind's sound, or null for its tone.</summary>
    public CueSample? For(SonarKind kind) => Find(FileName(kind));

    /// <summary>The wall direction's loop, or null for its tone.</summary>
    public CueSample? For(WallDirection direction) => Find(FileName(direction));

    /// <summary>The route ping kind's sound, or null for its notes.</summary>
    public CueSample? For(RoutePingKind kind) => Find(FileName(kind));

    private CueSample? Find(string name) => on() ? bank.Find(name) : null;

#pragma warning disable CA1308 // The files are named in lower case; nothing compares these names as identifiers.
    private static string Lower(Enum value) => value.ToString().ToLowerInvariant();
#pragma warning restore CA1308
}
