using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace Metaphor.Core;

/// <summary>Reads the game exe's bytes at an address relative to the module (an rva), mapped to the file through the PE
/// section table, without launching the game (issue #70). It replaces the hand mapping in
/// docs/raw-sources/msg-request-functions-2026-11-16.md. It doesn't disassemble: the call and jump targets it lists are
/// candidates, read from any byte that could start a call or jump.</summary>
public static class ExeBytes
{
    /// <summary>The most bytes one run prints.</summary>
    public const int MaxLength = 4096;

    private const int BytesPerLine = 16;

    /// <summary>Writes the game build, the <paramref name="length"/> bytes at <paramref name="rva"/> in
    /// <paramref name="gameExe"/>, and the candidate call and jump targets among them. Never writes the exe's path.</summary>
    /// <returns>0 when the bytes were written, 1 when the file doesn't hold them, 2 for a bad length or a file that
    /// isn't a PE image.</returns>
    public static int Run(string gameExe, long rva, int length, IReadOnlyList<GameBuild> knownBuilds, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(knownBuilds);
        ArgumentNullException.ThrowIfNull(output);
        if (length is < 1 or > MaxLength)
        {
            output.WriteLine(Invariant($"length must be 1 to {MaxLength}"));
            return 2;
        }
        var bytes = File.ReadAllBytes(gameExe);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var known = knownBuilds.FirstOrDefault(build => build.Sha256 == sha);
        output.WriteLine($"game build {sha} ({(known is null ? "not a known build" : Invariant($"Steam build {known.SteamBuildId}"))})");
        var headers = HeadersOrNone(bytes);
        if (headers is null)
        {
            output.WriteLine("the file is not a PE image");
            return 2;
        }

        var place = Place(headers, rva);
        if (place is null)
        {
            output.WriteLine(Invariant($"rva 0x{rva:x} is in no section"));
            return 1;
        }
        var (where, fileOffset, inFile) = place.Value;
        // A truncated file holds less than its section table says.
        inFile = Math.Min(inFile, rva + (bytes.Length - fileOffset));
        if (rva + length > inFile)
        {
            output.WriteLine(Invariant(
                $"rva 0x{rva:x} in {where}: bytes from 0x{Math.Max(rva, inFile):x} on are not in the file (zero-filled at load)"));
            return 1;
        }

        output.WriteLine(Invariant($"rva 0x{rva:x} in {where}, file offset 0x{fileOffset:x}, {length} bytes"));
        var range = bytes.AsSpan((int)fileOffset, length);
        for (var at = 0; at < length; at += BytesPerLine)
        {
            var line = range[at..Math.Min(at + BytesPerLine, length)];
            output.WriteLine(Invariant($"0x{rva + at:x}  {string.Join(' ', line.ToArray().Select(b => b.ToString("X2", CultureInfo.InvariantCulture)))}"));
        }

        var candidates = Candidates(range, rva)
            .Select(c => (c.At, c.Text, Section: SectionName(headers, c.Target)))
            .Where(c => c.Section is not null)
            .ToList();
        output.WriteLine(candidates.Count == 0
            ? "no candidate call or jump targets"
            : "candidate call and jump targets (not disassembled: any byte here can look like an opcode):");
        foreach (var (at, text, section) in candidates)
        {
            output.WriteLine(Invariant($"0x{at:x}  {text} ({section})"));
        }
        return 0;
    }

    /// <summary>A number as the tool's arguments take it: hex after <c>0x</c>, otherwise decimal. Null for anything
    /// else, including a negative number.</summary>
    public static long? ParseNumber(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex) ? hex : null
            : long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    /// <summary>Every call or jump that fits in the bytes read, with its target rva and how it reads: <c>call</c> and
    /// <c>jmp</c> rel32 (E8, E9), <c>jmp</c> rel8 (EB), <c>jcc</c> rel32 (0F 80–8F), and <c>call</c> and <c>jmp</c>
    /// through a pointer at an rip-relative address (FF 15, FF 25), whose target is the pointer's address. Short
    /// conditional jumps (70–7F) are left out: those bytes are too common to be worth listing.</summary>
    private static List<(long At, long Target, string Text)> Candidates(ReadOnlySpan<byte> range, long rva)
    {
        var found = new List<(long, long, string)>();
        for (var i = 0; i < range.Length; i++)
        {
            var rest = range[i..];
            var at = rva + i;
            (int Size, long Offset, string Kind)? jump = rest switch
            {
                [0xE8, _, _, _, _, ..] => (5, BinaryPrimitives.ReadInt32LittleEndian(rest[1..]), "call"),
                [0xE9, _, _, _, _, ..] => (5, BinaryPrimitives.ReadInt32LittleEndian(rest[1..]), "jmp"),
                [0xEB, var offset, ..] => (2, (sbyte)offset, "jmp"),
                [0x0F, >= 0x80 and <= 0x8F, _, _, _, _, ..] => (6, BinaryPrimitives.ReadInt32LittleEndian(rest[2..]), "jcc"),
                [0xFF, 0x15, _, _, _, _, ..] => (6, BinaryPrimitives.ReadInt32LittleEndian(rest[2..]), "call ["),
                [0xFF, 0x25, _, _, _, _, ..] => (6, BinaryPrimitives.ReadInt32LittleEndian(rest[2..]), "jmp ["),
                _ => null,
            };
            if (jump is var (size, relative, kind))
            {
                var target = at + size + relative;
                var text = kind.EndsWith('[')
                    ? Invariant($"{kind}0x{target:x}]")
                    : Invariant($"{kind} 0x{target:x}");
                found.Add((at, target, text));
            }
        }
        return found;
    }

    /// <summary>Where <paramref name="rva"/> is: its section's name (or "the headers"), its file offset, and the rva
    /// where that section's bytes in the file end. Null when it's in neither.</summary>
    private static (string Where, long FileOffset, long InFileUntil)? Place(PEHeaders headers, long rva)
    {
        var headerSize = headers.PEHeader!.SizeOfHeaders;
        if (rva >= 0 && rva < headerSize)
        {
            return ("the headers", rva, headerSize);
        }
        foreach (var section in headers.SectionHeaders)
        {
            if (Holds(section, rva))
            {
                return (section.Name, rva - section.VirtualAddress + section.PointerToRawData,
                    section.VirtualAddress + Math.Min(section.SizeOfRawData, Size(section)));
            }
        }
        return null;
    }

    private static string? SectionName(PEHeaders headers, long rva) =>
        headers.SectionHeaders.Where(section => Holds(section, rva)).Select(section => section.Name).FirstOrDefault();

    private static bool Holds(SectionHeader section, long rva) =>
        rva >= section.VirtualAddress && rva < section.VirtualAddress + Size(section);

    // A section's size in memory: VirtualSize, or the file size when a linker left VirtualSize at 0.
    private static long Size(SectionHeader section) =>
        section.VirtualSize == 0 ? section.SizeOfRawData : section.VirtualSize;

    /// <summary>The PE headers, or null when the file isn't a PE image.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A file that isn't a PE image is reported, not thrown.")]
    private static PEHeaders? HeadersOrNone(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var headers = new PEHeaders(stream);
            return headers.PEHeader is null ? null : headers;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
