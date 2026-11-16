namespace Metaphor.Core;

/// <summary>What a signature scan found: every match of each signature by name, as RVAs in order, and whether every
/// chunk could be read. When one couldn't, a "single" match may have a twin in the part that wasn't read.</summary>
/// <param name="Matches">Every match of each signature by name, as RVAs in order.</param>
/// <param name="Complete">Whether every chunk could be read.</param>
/// <param name="ReadableBytes">How many bytes of the regions were read and scanned, each counted once.</param>
/// <param name="UnreadableBytes">How many bytes of the regions were in chunks that failed to read.</param>
public sealed record ScanResult(
    IReadOnlyDictionary<string, IReadOnlyList<long>> Matches, bool Complete, long ReadableBytes = 0, long UnreadableBytes = 0);

/// <summary>Finds hook signatures in the running game a chunk at a time (#39), so the mod never holds a copy of the
/// whole module. Each chunk is read with an overlap one byte shorter than the longest pattern, so a match that crosses
/// a chunk edge is found, and each match is counted once: in the chunk where it starts.</summary>
public static class ChunkedScan
{
    /// <summary>Copies <paramref name="buffer"/>.Length bytes from <paramref name="rva"/>; false when they can't all
    /// be read, and then the chunk is skipped and the scan is incomplete.</summary>
    public delegate bool ReadAt(long rva, Span<byte> buffer);

    /// <summary>Every match of each of <paramref name="signatures"/> in <paramref name="regions"/> (readable stretches
    /// of the module, as start RVA and length), read through <paramref name="read"/> <paramref name="chunkBytes"/> at a
    /// time.</summary>
    public static ScanResult FindAll(
        IReadOnlyList<(long Rva, long Length)> regions, ReadAt read, IReadOnlyList<HookSignature> signatures,
        int chunkBytes = 1 << 20)
    {
        ArgumentNullException.ThrowIfNull(signatures);
        return FindAll(regions, read, [.. signatures.Select(signature => (signature.Name, signature.Pattern))], chunkBytes);
    }

    /// <summary>Every match of each named pattern in <paramref name="regions"/>, as
    /// <see cref="FindAll(IReadOnlyList{ValueTuple{long, long}}, ReadAt, IReadOnlyList{HookSignature}, int)"/> finds
    /// them. The names must be unique.</summary>
    public static ScanResult FindAll(
        IReadOnlyList<(long Rva, long Length)> regions, ReadAt read, IReadOnlyList<(string Name, string Pattern)> named,
        int chunkBytes = 1 << 20)
    {
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(named);
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkBytes, 1);

        var patterns = named.Select(entry => (entry.Name, Pattern: BytePattern.Parse(entry.Pattern))).ToList();
        var found = patterns.ToDictionary(p => p.Name, _ => new List<long>());
        long readable = 0, unreadable = 0;
        if (patterns.Count > 0)
        {
            var overlap = patterns.Max(p => p.Pattern.Length) - 1;
            var buffer = new byte[chunkBytes + overlap];
            foreach (var (start, length) in regions)
            {
                for (long offset = 0; offset < length; offset += chunkBytes)
                {
                    var own = (int)Math.Min(chunkBytes, length - offset);
                    var size = (int)Math.Min(own + overlap, length - offset);
                    var chunk = buffer.AsSpan(0, size);
                    if (!read(start + offset, chunk))
                    {
                        unreadable += own;
                        continue;
                    }
                    readable += own;
                    foreach (var (name, pattern) in patterns)
                    {
                        // Matches starting in the overlap belong to the next chunk, which finds them itself.
                        found[name].AddRange(pattern.Matches(chunk).Where(at => at < own).Select(at => start + offset + at));
                    }
                }
            }
        }
        return new(found.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<long>)pair.Value), unreadable == 0, readable, unreadable);
    }
}
