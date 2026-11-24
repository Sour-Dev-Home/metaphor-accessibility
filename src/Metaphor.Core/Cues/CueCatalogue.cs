using Metaphor.Core.Settings;

namespace Metaphor.Core.Cues;

/// <summary>A short sound the mod plays to say what just happened, faster than words (#636, ADR 0019). Each one is
/// explained for players in <c>docs/players/reference/sound-cues.md</c>; a new cue is added here and there, with its
/// own key in the settings file's <c>sounds</c> object (#661).</summary>
public enum Cue
{
    /// <summary>One of the player's party was hit: two low notes, falling.</summary>
    [SettingKey("sounds", "allyHit", "Ally hit sound", Description = "Someone in your party was hit.")]
    AllyHit,

    /// <summary>An enemy was hit: two high notes, rising.</summary>
    [SettingKey("sounds", "enemyHit", "Enemy hit sound", Description = "An enemy was hit.")]
    EnemyHit,

    /// <summary>The mark key wrote a marker to the report log (#597): one short high note, then the same again.
    /// </summary>
    [SettingKey("sounds", "mark", "Mark sound", Description = "The mark key wrote a marker.")]
    Mark,

    /// <summary>A field enemy noticed the player (#649): two quick notes leaping up, sharp and high.</summary>
    [SettingKey("sounds", "enemyNoticed", "Enemy noticed sound", Description = "A field enemy noticed you, from its " +
        "side. Needs `field.enemyAwareness`.")]
    EnemyNoticed,

    /// <summary>A field enemy gave up the search (#649): three notes falling, lower and gentler than the notice.</summary>
    [SettingKey("sounds", "enemyGaveUp", "Enemy gave up sound", Description = "A field enemy gave up the search, " +
        "from its side. Needs `field.enemyAwareness`.")]
    EnemyGaveUp,
}

/// <summary>One note of a cue: a sine at <paramref name="Hertz"/> for <paramref name="Milliseconds"/>.</summary>
public readonly record struct ToneStep(double Hertz, int Milliseconds);

/// <summary>A cue's notes, played one after the other.</summary>
public sealed record CueTone(IReadOnlyList<ToneStep> Steps);

/// <summary>What each cue sounds like. Cues differ in pitch and direction, so even one heard alone is told apart:
/// allies low and falling, enemies high and rising.</summary>
public static class CueCatalogue
{
    // A4 then D4, a falling fifth: 140 ms.
    private static readonly CueTone AllyHit = new([new ToneStep(440, 70), new ToneStep(294, 70)]);

    // A5 then E6, a rising fifth: 100 ms, shorter and brighter.
    private static readonly CueTone EnemyHit = new([new ToneStep(880, 50), new ToneStep(1320, 50)]);

    // D6 twice with a silent gap (a 0 Hz step), like a pen's click: 110 ms, neither rising nor falling, so it isn't a
    // hit.
    private static readonly CueTone Mark = new([new ToneStep(1175, 40), new ToneStep(0, 30), new ToneStep(1175, 40)]);

    // G6 then D7, a leaping fifth, the second held: 110 ms, higher and sharper than any hit, so it stands out.
    private static readonly CueTone EnemyNoticed = new([new ToneStep(1568, 40), new ToneStep(2349, 70)]);

    // E5, C5, A4: a falling third twice, 150 ms, below the notice and evenly paced, so it reads as letting go.
    private static readonly CueTone EnemyGaveUp =
        new([new ToneStep(659, 50), new ToneStep(523, 50), new ToneStep(440, 50)]);

    /// <summary>The notes <paramref name="cue"/> plays.</summary>
    public static CueTone For(Cue cue) => cue switch
    {
        Cue.AllyHit => AllyHit,
        Cue.EnemyHit => EnemyHit,
        Cue.Mark => Mark,
        Cue.EnemyNoticed => EnemyNoticed,
        Cue.EnemyGaveUp => EnemyGaveUp,
        _ => throw new ArgumentOutOfRangeException(nameof(cue)),
    };
}
