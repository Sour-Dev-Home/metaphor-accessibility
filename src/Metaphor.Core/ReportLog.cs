using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Metaphor.Core.Messages;

namespace Metaphor.Core;

/// <summary>The log a player can attach to a bug report (#188), written in every build: labels, message events, hook
/// counts and errors, and never message text (ground rule 4). It hears the same message events as
/// <see cref="MessageLog"/> but keeps only their numbers and labels. Safe to call from any game thread, and never
/// throws.</summary>
public sealed class ReportLog(TextWriter writer, Func<long> millisecondsSinceStart, IEnumerable<string> redact)
    : IMessageFileObserver, ILineRequestObserver, IChoiceRequestObserver
{
    /// <summary>The log's file name in the mod's folder.</summary>
    public const string FileName = "report-log.txt";

    /// <summary>The longest label written; anything longer is counted instead.</summary>
    private const int MaxLabelLength = 64;

    private readonly object gate = new();
    private readonly string[] redacted = [.. redact.Where(s => !string.IsNullOrEmpty(s)).OrderByDescending(s => s.Length)];

    /// <summary>A message file the game compiled: its number, its path (as a label, so usually only its length) and
    /// its length, not its source.</summary>
    public void Observe(MessageFileCompiled file) => Write(() =>
        Line($"compile id={file.Id} path={Label(file.Path)} chars={(file.Source ?? "").Length}"));

    /// <summary>A line the game asked for: its kind and its label, not its text.</summary>
    public void Observe(LineRequested line) => Write(() => Line($"request kind={line.Kind} label={Label(line.Label)}"));

    /// <summary>A choice the game opened: its kind and the labels of its prompt and options, not their text.</summary>
    public void Observe(ChoiceRequested choice) => Write(() =>
        Line($"select kind={choice.Kind} prompt={Label(choice.PromptLabel)} choice={Label(choice.ChoiceLabel)}"));

    /// <summary>One line from the mod itself, such as the startup status with its hook counts, or an error. Each
    /// string the log was made to redact (the mod's folder, the player's user name) is replaced first.</summary>
    public void Note(string line) => Write(() =>
        Line($"{redacted.Aggregate(line ?? "", (text, s) => text.Replace(s, "<redacted>", StringComparison.OrdinalIgnoreCase))}"));

    /// <summary>A label in the shape the game's labels take: letters and digits joined by underscores, with at least
    /// one underscore (<c>MES_e00_002_001_004</c>, <c>Title_System_08</c>). Anything else, a one-word line or a path
    /// included, could be message text, so only its length is written.</summary>
    private static string Label(string? label) => label switch
    {
        null => "(unreadable)",
        { Length: <= MaxLabelLength } when IsLabelShaped(label) => label,
        _ => string.Create(CultureInfo.InvariantCulture, $"(not a label, {label.Length} chars)"),
    };

    private static bool IsLabelShaped(string text)
    {
        var parts = text.Split('_');
        return parts.Length >= 2 && parts.All(part => part.Length > 0 && part.All(char.IsAsciiLetterOrDigit));
    }

    private string Line(FormattableString text) =>
        $"{millisecondsSinceStart().ToString(CultureInfo.InvariantCulture)} ms {text.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}";

    /// <summary>Builds and writes one record under the lock. Building it is inside the try too, so nothing a caller
    /// passes can make this throw.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A report log must never reach the game.")]
    private void Write(Func<string> record)
    {
        lock (gate)
        {
            try
            {
                writer.Write(record());
                writer.Flush();
            }
            catch (Exception)
            {
                // A lost line in a report is better than a crashed game.
            }
        }
    }
}
