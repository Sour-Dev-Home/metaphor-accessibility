using System.Globalization;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Core.Replay;

/// <summary>One read of a pad as the input hook saw it (#58): the pad's number, its 16-bit button mask and what the read
/// returned.</summary>
public sealed record PadRead(int Pad, int Buttons, int Returned);

/// <summary>One record of a fixture, at its time in milliseconds from the fixture's first record.</summary>
public abstract record SessionRecord(long Milliseconds);

/// <summary>The game compiled a message file; its source is not in the fixture.</summary>
public sealed record CompileRecord(long Milliseconds, string Path, int Id) : SessionRecord(Milliseconds);

/// <summary>The game's scripts asked for a line.</summary>
public sealed record LineRecord(long Milliseconds, LineRequested Line) : SessionRecord(Milliseconds);

/// <summary>The game's scripts opened a choice.</summary>
public sealed record ChoiceRecord(long Milliseconds, ChoiceRequested Choice) : SessionRecord(Milliseconds);

/// <summary>A menu's cursor manager handled an input.</summary>
public sealed record CursorRecord(long Milliseconds, CursorInput Input) : SessionRecord(Milliseconds);

/// <summary>The game read a pad.</summary>
public sealed record PadRecord(long Milliseconds, PadRead Read) : SessionRecord(Milliseconds);

/// <summary>The input hooks read whether the game has its menus open (#268); a failed read is closed.</summary>
public sealed record MenusRecord(long Milliseconds, bool Open) : SessionRecord(Milliseconds);

/// <summary>A recorded game session's events, for tests to replay through the seams the hooks feed (#149). Written by
/// <see cref="SessionLogConverter"/> from a local <see cref="MessageLog"/>, one file per behaviour under
/// <c>tests/fixtures/sessions/</c>. The format: <c>#</c> lines first, one of them <c># game build: &lt;sha256&gt;</c>;
/// then one record per line, <c>&lt;ms&gt; &lt;kind&gt; &lt;field&gt;=&lt;value&gt; …</c>, with kinds <c>compile</c>
/// (<c>id</c>, <c>path</c>), <c>request</c> (<c>kind</c>, <c>label</c>), <c>select</c> (<c>kind</c>, <c>prompt</c>,
/// <c>choice</c>; <c>-</c> for an unreadable label), <c>cursor</c> (<c>mgr</c>, <c>class</c>, <c>cmd</c>, <c>result</c>,
/// <c>top</c>, <c>row</c>, <c>flags</c>, and <c>screen</c> when its screen class is known), <c>pad</c> (<c>pad</c>, <c>buttons</c>, <c>returned</c>) and
/// <c>menus</c> (<c>open</c>, 1 or 0). No game text: a
/// test that needs text invents it, keyed by the real label.</summary>
public sealed class SessionFixture
{
    private const string BuildPrefix = "# game build: ";

    private SessionFixture(string gameBuild, IReadOnlyList<SessionRecord> records)
    {
        GameBuild = gameBuild;
        Records = records;
    }

    /// <summary>The game build the session was recorded on.</summary>
    public string GameBuild { get; }

    /// <summary>The records, in the order the game made them.</summary>
    public IReadOnlyList<SessionRecord> Records { get; }

    /// <summary>Reads a fixture. Throws <see cref="FormatException"/> when it names no game build or holds a record
    /// this format doesn't have.</summary>
    public static SessionFixture Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string? build = null;
        var records = new List<SessionRecord>();
        var number = 0;
        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            number++;
            if (line.StartsWith(BuildPrefix, StringComparison.Ordinal))
            {
                build = line[BuildPrefix.Length..].Trim();
            }
            else if (line.Length > 0 && !line.StartsWith('#'))
            {
                records.Add(Record(line, number));
            }
        }
        return build is null
            ? throw new FormatException("A session fixture must name its game build (# game build: <sha256>).")
            : new SessionFixture(build, records);
    }

    /// <summary>Tells each record, in order, to the seam the hook for it feeds: compiled files, line requests and
    /// choices to <paramref name="messages"/>, cursor inputs to <paramref name="cursors"/>, pad reads to
    /// <paramref name="pad"/>, menus-open reads to <paramref name="menus"/>. A compiled file's source is
    /// <paramref name="source"/>'s for its path, made up by the test; nothing when omitted.</summary>
    public void Replay(
        MessageEvents messages, CursorEvents cursors, Action<PadRead>? pad = null, Func<string, string>? source = null,
        Action<bool>? menus = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(cursors);
        foreach (var record in Records)
        {
            switch (record)
            {
                case CompileRecord file:
                    messages.Compiled(new MessageFileCompiled(file.Path, file.Id, source?.Invoke(file.Path) ?? ""));
                    break;
                case LineRecord line:
                    messages.Requested(line.Line);
                    break;
                case ChoiceRecord choice:
                    messages.ChoiceOpened(choice.Choice);
                    break;
                case CursorRecord cursor:
                    cursors.Handled(cursor.Input);
                    break;
                case PadRecord read:
                    pad?.Invoke(read.Read);
                    break;
                case MenusRecord state:
                    menus?.Invoke(state.Open);
                    break;
            }
        }
    }

    private static SessionRecord Record(string line, int number)
    {
        var parts = line.Split(' ');
        var fields = parts.Skip(2).Select(part => part.Split('=', 2)).Where(pair => pair.Length == 2)
            .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);
        if (parts.Length < 2 || fields.Count != parts.Length - 2)
        {
            throw Refused(number);
        }
        var ms = Long(parts[0], number);
        string Field(string name) => fields.TryGetValue(name, out var value) ? value : throw Refused(number);
        int Number(string name) => (int)Long(Field(name), number);
        string? Label(string name) => Field(name) == "-" ? null : Field(name);
        return parts[1] switch
        {
            "compile" => new CompileRecord(ms, Field("path"), Number("id")),
            "request" => new LineRecord(ms, new LineRequested(Number("kind"), Field("label"))),
            "select" => new ChoiceRecord(ms, new ChoiceRequested(Number("kind"), Label("prompt"), Label("choice"))),
            "cursor" => new CursorRecord(ms, new CursorInput(Long(Field("mgr"), number),
                Field("class") == "?" ? null : Long(Field("class"), number), Number("cmd"), Number("result"),
                Number("top"), Number("row"), Number("flags"), fields.GetValueOrDefault("screen"))),
            "pad" => new PadRecord(ms, new PadRead(Number("pad"), Number("buttons"), Number("returned"))),
            "menus" => new MenusRecord(ms, Field("open") switch
            {
                "1" => true,
                "0" => false,
                _ => throw Refused(number),
            }),
            _ => throw Refused(number),
        };
    }

    private static long Long(string value, int number) =>
        value.StartsWith("0x", StringComparison.Ordinal)
            ? long.TryParse(value.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex) ? hex : throw Refused(number)
            : long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var dec) ? dec : throw Refused(number);

    private static FormatException Refused(int number) =>
        new(string.Create(CultureInfo.InvariantCulture, $"line {number}: not a session fixture record."));
}
