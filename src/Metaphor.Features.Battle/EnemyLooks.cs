using System.Text.Json;

namespace Metaphor.Features.Battle;

/// <summary>What an enemy looks like (#699): a short description the mod's contributors write, keyed by the enemy's
/// name as the game shows it ("Goblin"), so every enemy id that shares a name shares its description. The descriptions
/// are our own words, written from what the enemy looks like in play; no game text is copied. The table ships embedded
/// as <see cref="Resource"/> and starts empty. The idea of a key that describes an enemy's look comes from Arcana, the
/// Persona 3 Reload accessibility mod (<c>ChipsAhoiMcCoy/arcana</c>, release notes 0.4.0, as
/// docs/raw-sources/arcana-combat-navigation-2026-11-22.md describes it); no text or code is copied.</summary>
/// <remarks>The file is <c>{"looks": {"Goblin": "A small ..."}}</c>; names match ignoring case, and a blank
/// description counts as none. Any other top-level member is ignored, so the file can carry a note for contributors.
/// </remarks>
public sealed class EnemyLooks
{
    /// <summary>The embedded table's resource name.</summary>
    public const string Resource = "Metaphor.Features.Battle.enemy-looks.json";

    /// <summary>The largest table read, in bytes; a bigger one is refused.</summary>
    public const int MaxSize = 512 * 1024;

    private readonly Dictionary<string, string> looks;

    private EnemyLooks(Dictionary<string, string> looks) => this.looks = looks;

    /// <summary>A table with no descriptions.</summary>
    public static EnemyLooks Empty { get; } = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    /// <summary>How many enemies have a description.</summary>
    public int Count => looks.Count;

    /// <summary>The table <paramref name="json"/> holds, or null when it isn't a table of that shape.</summary>
    public static EnemyLooks? Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaxSize)
        {
            return null;
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("looks", out var table) || table.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            var looks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in table.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.String)
                {
                    return null;
                }
                if (entry.Name.Trim() is { Length: > 0 } name && entry.Value.GetString()!.Trim() is { Length: > 0 } look)
                {
                    looks[name] = look;
                }
            }
            return new EnemyLooks(looks);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The table shipped in this assembly, or <see cref="Empty"/> when it is missing or unreadable.</summary>
    public static EnemyLooks Embedded()
    {
        using var stream = typeof(EnemyLooks).Assembly.GetManifestResourceStream(Resource);
        if (stream is null)
        {
            return Empty;
        }
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return Parse(reader.ReadToEnd()) ?? Empty;
    }

    /// <summary>The description of the enemy named <paramref name="name"/>, or null for none.</summary>
    public string? Of(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return looks.TryGetValue(name.Trim(), out var look) ? look : null;
    }

    /// <summary>What the look key says for an enemy labelled <paramref name="label"/> ("Second enemy") and named
    /// <paramref name="name"/>: "Goblin. A small ...", "Goblin, no description yet", or, with no name known,
    /// "Second enemy, name not known yet".</summary>
    public string Line(string label, string? name)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (name is null)
        {
            return $"{label}, name not known yet";
        }
        return Of(name) is { } look ? $"{name}. {look}" : $"{name}, no description yet";
    }
}
