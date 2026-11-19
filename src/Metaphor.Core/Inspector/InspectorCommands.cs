using System.Globalization;
using System.Text;

namespace Metaphor.Core.Inspector;

/// <summary>The live inspector's commands (#291): read-only questions about the running game's memory, each answered
/// through <see cref="MemoryText.ReadAddress"/>, so nothing here can write memory, call game code or hook. A request is
/// one line, a command name and its arguments separated by spaces; the response is one line, <c>ok ...</c> or
/// <c>err ...</c>. The commands are documented in docs/contributors/how-to/inspect-the-running-game.md. Not
/// thread-safe: one session asks at a time.</summary>
public sealed class InspectorCommands(long moduleBase, long moduleSize, MemoryText.ReadAddress read)
{
    /// <summary>The most bytes one <c>read</c> returns.</summary>
    public const int MaxReadBytes = 4096;

    /// <summary>The most offsets one <c>follow</c> walks.</summary>
    public const int MaxPathSteps = 16;

    /// <summary>The most watches a session keeps.</summary>
    public const int MaxWatches = 64;

    /// <summary>The most bytes one watch covers.</summary>
    public const int MaxWatchBytes = 256;

    /// <summary>Every command, in the order <c>help</c> lists them.</summary>
    public static IReadOnlyList<string> Names { get; } = ["help", "base", "read", "follow", "rtti", "watch", "watches", "changes", "unwatch"];

    private const string Help =
        "ok help | base | read <addr> <count> | follow <addr> <offset>... | rtti <addr> | watch <addr> <count> | " +
        "watches | changes | unwatch <id>; <addr> is 0x<hex> or base+0x<hex>, <offset> 0x<hex> or -0x<hex>";

    private readonly SortedDictionary<int, Watch> watches = [];
    private int nextWatch = 1;

    /// <summary>The response line to the request <paramref name="line"/>, without a newline. Never throws on a bad
    /// request.</summary>
    public string Respond(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words is [] ? Unknown : words[0] switch
        {
            "help" when words.Length == 1 => Help,
            "base" when words.Length == 1 => $"ok {Hex(moduleBase)} {Hex(moduleSize)}",
            "read" => Read(words),
            "follow" => Follow(words),
            "rtti" => Rtti(words),
            "watch" => AddWatch(words),
            "watches" when words.Length == 1 => ListWatches(),
            "changes" when words.Length == 1 => Changes(),
            "unwatch" => Unwatch(words),
            _ => Unknown,
        };
    }

    private const string Unknown = "err unknown command; try help";

    private string Read(string[] words)
    {
        if (words.Length != 3 || Address(words[1]) is not { } address || Count(words[2]) is not { } count)
        {
            return "err usage: read <addr> <count>";
        }
        if (count is < 1 or > MaxReadBytes)
        {
            return $"err count must be 1 to {MaxReadBytes}";
        }
        var buffer = new byte[count];
        return read(address, buffer) ? "ok " + Convert.ToHexStringLower(buffer) : "err unreadable";
    }

    private string Follow(string[] words)
    {
        if (words.Length < 3 || Address(words[1]) is not { } address)
        {
            return "err usage: follow <addr> <offset>...";
        }
        if (words.Length - 2 > MaxPathSteps)
        {
            return $"err at most {MaxPathSteps} offsets";
        }
        var offsets = new long[words.Length - 2];
        for (var i = 0; i < offsets.Length; i++)
        {
            if (Offset(words[i + 2]) is not { } offset)
            {
                return "err usage: follow <addr> <offset>...";
            }
            offsets[i] = offset;
        }
        for (var step = 0; step < offsets.Length; step++)
        {
            if (ReadInt64(address) is not { } pointer)
            {
                return $"err unreadable at step {step + 1} ({Hex(address)})";
            }
            address = unchecked(pointer + offsets[step]);
        }
        return "ok " + Hex(address);
    }

    private string Rtti(string[] words)
    {
        if (words.Length != 2 || Address(words[1]) is not { } address)
        {
            return "err usage: rtti <addr>";
        }
        var name = RttiName.Of(
            address, moduleBase, moduleSize,
            ReadInt64,
            ReadInt32,
            at => MemoryText.ReadAnsi(read, at, 256));
        return name is null ? "err no class" : "ok " + name;
    }

    // Arrays rather than stackalloc: the architecture rules treat a span over a raw pointer as a memory write.
    private long? ReadInt64(long address)
    {
        var buffer = new byte[sizeof(long)];
        return read(address, buffer) ? BitConverter.ToInt64(buffer) : null;
    }

    private int? ReadInt32(long address)
    {
        var buffer = new byte[sizeof(int)];
        return read(address, buffer) ? BitConverter.ToInt32(buffer) : null;
    }

    private string AddWatch(string[] words)
    {
        if (words.Length != 3 || Address(words[1]) is not { } address || Count(words[2]) is not { } count)
        {
            return "err usage: watch <addr> <count>";
        }
        if (count is < 1 or > MaxWatchBytes)
        {
            return $"err count must be 1 to {MaxWatchBytes}";
        }
        if (watches.Count >= MaxWatches)
        {
            return $"err at most {MaxWatches} watches";
        }
        var id = nextWatch++;
        watches[id] = new Watch(address, count, Snapshot(address, count));
        return $"ok {id.ToString(CultureInfo.InvariantCulture)}";
    }

    private string ListWatches() =>
        Ok(watches.Select(pair => $"{Id(pair.Key)}={Hex(pair.Value.Address)}+{pair.Value.Count.ToString(CultureInfo.InvariantCulture)}"));

    private string Changes()
    {
        var changed = new List<string>();
        foreach (var (id, watch) in watches)
        {
            var now = Snapshot(watch.Address, watch.Count);
            if (now != watch.Last)
            {
                watch.Last = now;
                changed.Add($"{Id(id)}={now ?? "unreadable"}");
            }
        }
        return Ok(changed);
    }

    private string Unwatch(string[] words)
    {
        if (words.Length != 2 || !int.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            return "err usage: unwatch <id>";
        }
        return watches.Remove(id) ? "ok" : $"err no watch {Id(id)}";
    }

    /// <summary>The bytes at <paramref name="address"/> as hex, or null when they can't all be read.</summary>
    private string? Snapshot(long address, int count)
    {
        var buffer = new byte[count];
        return read(address, buffer) ? Convert.ToHexStringLower(buffer) : null;
    }

    private static string Ok(IEnumerable<string> items)
    {
        var text = new StringBuilder("ok");
        foreach (var item in items)
        {
            text.Append(' ').Append(item);
        }
        return text.ToString();
    }

    /// <summary><c>0x</c> and hex digits for an address, or <c>base+0x</c> and hex digits for one in the module.</summary>
    private long? Address(string word) =>
        word.StartsWith("base+", StringComparison.Ordinal)
            ? ParseHex(word["base+".Length..]) is { } rva ? unchecked(moduleBase + rva) : null
            : ParseHex(word);

    /// <summary><c>0x</c> and hex digits, with an optional leading minus.</summary>
    private static long? Offset(string word) =>
        word.StartsWith('-') ? ParseHex(word[1..]) is { } value ? -value : null : ParseHex(word);

    /// <summary>Decimal digits or <c>0x</c> and hex digits; a minus sign is accepted so the range check refuses it.</summary>
    private static int? Count(string word) =>
        word.StartsWith("0x", StringComparison.Ordinal)
            ? ParseHex(word) is { } value && value <= int.MaxValue ? (int)value : null
            : int.TryParse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var count) ? count : null;

    private static long? ParseHex(string word) =>
        word.StartsWith("0x", StringComparison.Ordinal) && word.Length is > 2 and <= 18 &&
        ulong.TryParse(word.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value)
            ? unchecked((long)value)
            : null;

    private static string Hex(long value) => "0x" + value.ToString("x", CultureInfo.InvariantCulture);

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);

    private sealed class Watch(long address, int count, string? last)
    {
        public long Address { get; } = address;
        public int Count { get; } = count;
        public string? Last { get; set; } = last;
    }
}
