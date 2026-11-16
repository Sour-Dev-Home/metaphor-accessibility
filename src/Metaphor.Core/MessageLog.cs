using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Metaphor.Core.Messages;

namespace Metaphor.Core;

/// <summary>The #55 spike's log of what the game's message functions saw: each message file compiled, with its source
/// text, and each message looked up, with when. Read after a play session to find which call carries the line on
/// screen. Debug builds only; it holds game text, so it stays in the local mod folder and is never committed. Safe to
/// call from any game thread, and never throws.</summary>
public sealed class MessageLog(TextWriter writer, Func<long> millisecondsSinceStart)
    : IMessageFileObserver, ILineRequestObserver
{
    /// <summary>The log's file name in the mod's folder.</summary>
    public const string FileName = "message-log.txt";

    /// <summary>The function a <see cref="LineRequested"/> came from, as its record names it (#86).</summary>
    private const string LineRequestFunction = "MsgRequestByName";

    private readonly object gate = new();

    /// <summary>A message file the game compiled: the number it gave it, its path, and its source text.</summary>
    public void Observe(MessageFileCompiled file) => Write(() =>
    {
        var source = file.Source ?? "";
        var record = new StringBuilder(Line($"{millisecondsSinceStart()} ms compile id={file.Id} path={file.Path} chars={source.Length}"));
        foreach (var line in source.ReplaceLineEndings("\n").Split('\n'))
        {
            record.Append("  | ").Append(line).AppendLine();
        }
        return record.ToString();
    });

    /// <summary>A message the game looked up, by message number and label index.</summary>
    public void LookedUp(long messageId, int labelIndex) =>
        Write(() => Line($"{millisecondsSinceStart()} ms lookup id={messageId} label={labelIndex}"));

    /// <summary>A function the game registered for its scripts: its script name, and where the native function is in
    /// the game module. <paramref name="viaUserdata"/> says the address came from the userdata the game pushed (a game
    /// function behind a shared trampoline) and not from the registered function itself.</summary>
    public void Registered(string name, long rva, bool viaUserdata) =>
        Write(() => Line($"{millisecondsSinceStart()} ms register name={name} rva=0x{rva:x} {(viaUserdata ? "userdata" : "direct")}"));

    /// <summary>A message the game's scripts asked for: which request function ran, and what it was passed, as the
    /// listening hook formatted it.</summary>
    public void Requested(string function, string arguments) =>
        Write(() => Line($"{millisecondsSinceStart()} ms request via={function} {arguments}"));

    /// <summary>A message the game resolved by key (#74, #106): which lookup ran, its arguments and its result, as the
    /// listening hook formatted them.</summary>
    public void Resolved(string function, string arguments) =>
        Write(() => Line($"{millisecondsSinceStart()} ms resolve via={function} {arguments}"));

    /// <summary>A string that could be the player's chosen name (#106): only its length, how it compares with the
    /// typed name and where it was, never its text.</summary>
    public void PlayerNameSeen(string function, PlayerNameSighting sighting) =>
        Write(() => Line($"{millisecondsSinceStart()} ms player-name via={function} {sighting}"));

    /// <summary>A line the game asked for by label, with its kind.</summary>
    public void Observe(LineRequested line) => Write(() =>
        Line($"{millisecondsSinceStart()} ms request via={LineRequestFunction} kind={line.Kind} text=\"{LogText.Escaped(line.Label)}\""));

    private static string Line(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture) + Environment.NewLine;

    /// <summary>Builds and writes one record under the lock. Building it is inside the try too, so nothing a caller
    /// passes can make this throw.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A spike log must never reach the game.")]
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
                // A lost line in a spike log is better than a crashed game.
            }
        }
    }
}
