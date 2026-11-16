using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Metaphor.Core;

/// <summary>The #55 spike's log of what the game's message functions saw: each message file compiled, with its source
/// text, and each message looked up, with when. Read after a play session to find which call carries the line on
/// screen. Debug builds only; it holds game text, so it stays in the local mod folder and is never committed. Safe to
/// call from any game thread, and never throws.</summary>
public sealed class MessageLog(TextWriter writer, Func<long> millisecondsSinceStart)
{
    /// <summary>The log's file name in the mod's folder.</summary>
    public const string FileName = "message-log.txt";

    private readonly object gate = new();

    /// <summary>A message file the game compiled: the number it gave it, its path, and its source text.</summary>
    public void Compiled(long id, string path, string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var record = new StringBuilder(Line($"{millisecondsSinceStart()} ms compile id={id} path={path} chars={source.Length}"));
        foreach (var line in source.ReplaceLineEndings("\n").Split('\n'))
        {
            record.Append("  | ").Append(line).AppendLine();
        }
        Write(record.ToString());
    }

    /// <summary>A message the game looked up, by message number and label index.</summary>
    public void LookedUp(long messageId, int labelIndex) =>
        Write(Line($"{millisecondsSinceStart()} ms lookup id={messageId} label={labelIndex}"));

    private static string Line(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture) + Environment.NewLine;

    [SuppressMessage("Design", "CA1031", Justification = "A spike log must never reach the game.")]
    private void Write(string record)
    {
        lock (gate)
        {
            try
            {
                writer.Write(record);
                writer.Flush();
            }
            catch (Exception)
            {
                // A lost line in a spike log is better than a crashed game.
            }
        }
    }
}
