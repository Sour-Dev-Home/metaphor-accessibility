using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Metaphor.Core.Logging;

/// <summary>Values the mod met but has no mapping for yet (#824), such as an icon tag with no word or a screen class no
/// feature knows: each written once per kind and value per session, so a long session's log names every gap without
/// repeating it. At most <see cref="MaxValues"/> a session, then one line saying the list is full. Only names and
/// numbers are written; a value with any other character is written as its length. Safe to use from several threads,
/// and never throws.</summary>
/// <remarks>Logging each unknown value once is an idea from Arcana's log, seen in a log a player shared on 2026-11-23
/// (ChipsAhoiMcCoy, https://github.com/ChipsAhoiMcCoy/arcana, Zlib); no code was copied (CREDITS.md).</remarks>
public sealed class UnknownValues
{
    /// <summary>The most values listed in one session.</summary>
    public const int MaxValues = 256;

    /// <summary>The longest value written; anything longer is written as its length.</summary>
    private const int MaxValueLength = 96;

    private readonly Action<string> write;
    private readonly int limit;
    private readonly HashSet<(string Kind, string Value)> seen = [];
    private readonly Lock gate = new();
    private bool full;

    /// <param name="write">Where each first sighting goes: the <c>unknown</c> area's log.</param>
    /// <param name="limit">The most values listed.</param>
    public UnknownValues(Action<string> write, int limit = MaxValues)
    {
        ArgumentNullException.ThrowIfNull(write);
        this.write = write;
        this.limit = limit;
    }

    /// <summary>A list that writes nowhere, for features built without a log.</summary>
    public static UnknownValues None { get; } = new(_ => { });

    /// <summary>Writes "<paramref name="kind"/> <paramref name="value"/>" the first time this pair is reported.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A report log must never reach the game.")]
    public void Report(string kind, string value)
    {
        string? line = null;
        lock (gate)
        {
            if (full || !seen.Add((kind ?? "", value ?? "")))
            {
                return;
            }
            if (seen.Count > limit)
            {
                full = true;
                line = string.Create(CultureInfo.InvariantCulture, $"list full at {limit} values; later ones aren't listed");
            }
        }
        line ??= $"{Safe(kind)} {Safe(value)}";
        try
        {
            write(line);
        }
        catch (Exception)
        {
            // A lost line in a report is better than a crashed game.
        }
    }

    /// <summary>A value as written: names and numbers as they are, anything else as its length, as it could be text.
    /// </summary>
    private static string Safe(string? value) =>
        value is { Length: > 0 and <= MaxValueLength } &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '_' or '@' or ':' or '<' or '>' or ',' or '.' or '-')
            ? value
            : string.Create(CultureInfo.InvariantCulture, $"({value?.Length ?? 0} chars)");
}
