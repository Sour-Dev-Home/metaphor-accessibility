using System.Globalization;

namespace Metaphor.Core;

/// <summary>A signature's bytes, parsed: fixed bytes and wildcards that match any byte.</summary>
public sealed class BytePattern
{
    private readonly byte?[] bytes;

    // The longest run of fixed bytes and where it starts in the pattern. Searching for it first lets the vectorised
    // IndexOf skip most of the file.
    private readonly byte[] anchor;
    private readonly int anchorOffset;

    private BytePattern(byte?[] bytes)
    {
        this.bytes = bytes;
        int bestStart = 0, bestLength = 0, start = 0;
        for (var i = 0; i <= bytes.Length; i++)
        {
            if (i == bytes.Length || bytes[i] is null)
            {
                if (i - start > bestLength)
                {
                    (bestStart, bestLength) = (start, i - start);
                }
                start = i + 1;
            }
        }
        anchorOffset = bestStart;
        anchor = [.. bytes.Skip(bestStart).Take(bestLength).Select(b => b!.Value)];
    }

    /// <summary>Parses hex bytes separated by whitespace, with <c>??</c> (or <c>?</c>) for any byte:
    /// <c>48 8B ?? 10</c>.</summary>
    /// <exception cref="FormatException">A token isn't a byte or a wildcard, or the pattern has no fixed byte.</exception>
    public static BytePattern Parse(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var tokens = pattern.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var bytes = new byte?[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            bytes[i] = tokens[i] switch
            {
                "?" or "??" => null,
                { Length: 2 } token when byte.TryParse(token, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var b) => b,
                var token => throw new FormatException($"'{token}' is neither a hex byte nor ??"),
            };
        }
        return bytes.Any(b => b is not null)
            ? new BytePattern(bytes)
            : throw new FormatException("a pattern needs at least one fixed byte");
    }

    /// <summary>Every offset in <paramref name="data"/> where the pattern starts, including matches that overlap.</summary>
    public IReadOnlyList<long> Matches(ReadOnlySpan<byte> data)
    {
        var matches = new List<long>();
        var from = anchorOffset;
        while (from <= data.Length - anchor.Length)
        {
            var found = data[from..].IndexOf(anchor);
            if (found < 0)
            {
                break;
            }
            var start = from + found - anchorOffset;
            if (start + bytes.Length <= data.Length && IsMatchAt(data, start))
            {
                matches.Add(start);
            }
            from += found + 1;
        }
        return matches;
    }

    private bool IsMatchAt(ReadOnlySpan<byte> data, int start)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] is { } b && data[start + i] != b)
            {
                return false;
            }
        }
        return true;
    }
}
