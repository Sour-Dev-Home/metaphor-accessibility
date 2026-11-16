using System.Text.RegularExpressions;

namespace Metaphor.Core.Announcements;

/// <summary>What the text cleaner does with each tag, as data, so #55's findings about the game's tags fill it in
/// without changing the cleaner. Tag names match in the case given here (ordinal); a tag with no rule is dropped.</summary>
public sealed record TextCleanerRules
{
    /// <summary>No tag rules: every tag is dropped.</summary>
    public static TextCleanerRules None { get; } = new();

    /// <summary>Tags that stand for text the screen shows, such as the player's chosen name: tag name to the key its
    /// value is looked up by. A placeholder fills whatever arguments it has.</summary>
    public IReadOnlyDictionary<string, string> Placeholders { get; init; } = new Dictionary<string, string>();

    /// <summary>Tags said as a word, such as button icons: the tag's name and arguments, single-spaced
    /// (<c>"PAD 1"</c>), to the word. Unlike a placeholder, the arguments must match too.</summary>
    public IReadOnlyDictionary<string, string> SpokenTags { get; init; } = new Dictionary<string, string>();
}

/// <summary>Turns text as the game stores it into what an announcement says (glossary: text cleaner): tags become a
/// word, a placeholder's value or nothing, and whitespace is squashed.</summary>
public sealed partial class TextCleaner
{
    private readonly TextCleanerRules rules;
    private readonly Func<string, string?> placeholderValues;

    /// <param name="rules">What to do with each tag.</param>
    /// <param name="placeholderValues">A placeholder's current value by key, or null when there is none.</param>
    public TextCleaner(TextCleanerRules rules, Func<string, string?> placeholderValues)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(placeholderValues);
        this.rules = rules;
        this.placeholderValues = placeholderValues;
    }

    /// <summary>A cleaner with no placeholder values.</summary>
    public TextCleaner(TextCleanerRules rules)
        : this(rules, _ => null)
    {
    }

    /// <summary>The text as it should be said.</summary>
    public string Clean(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Squash(Tag().Replace(text, match =>
        {
            if (match.Groups["closing"].Success)
            {
                return "";
            }
            var name = match.Groups["name"].Value;
            if (rules.Placeholders.TryGetValue(name, out var key))
            {
                return placeholderValues(key) ?? "";
            }
            var args = Squash(match.Groups["args"].Value.TrimStart('=', ':'));
            return rules.SpokenTags.TryGetValue(args.Length == 0 ? name : $"{name} {args}", out var word) ? word : "";
        }));
    }

    private static string Squash(string text) => Whitespace().Replace(text, " ").Trim();

    // <NAME>, </NAME>, and NAME followed by arguments after a space, '=' or ':'. The forms seen so far are
    // <NS>, <BUSTUP 0 0 0> and <VOICE "..."> (docs/raw-sources/game-text-hooks-community-2026-11-15.md); closing tags and
    // the '='/':' forms are a guess until #55, harmless when absent.
    [GeneratedRegex(@"<(?<closing>/)?(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<args>(?:[\s=:][^<>]*)?)>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
