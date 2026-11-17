using System.Buffers.Binary;
using System.Globalization;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Metaphor.Core;

/// <summary>Lists the game exe's imports as its PE headers declare them, from the file on disk, without launching the
/// game (issue #58). For each imported function it gives the import slot (the IAT entry the loader fills) and the
/// instructions in executable sections that read that slot: <c>call</c> or <c>jmp</c> through it (FF 15, FF 25, also
/// with a REX prefix) and <c>mov</c> of it into a 64-bit register (REX.W 8B with an rip-relative operand). Like
/// <see cref="ExeBytes"/> it doesn't disassemble, so a reference is a candidate; and a function the game looks up at
/// run time (GetProcAddress) has no slot and isn't listed.</summary>
public static class ExeImports
{
    /// <summary>The most references printed for one function; the count is always printed in full.</summary>
    public const int MaxReferencesShown = 64;

    private const uint ExecutableSection = 0x20000000;

    private sealed record Import(string Dll, string Function, long Slot, bool Delayed);

    /// <summary>Writes the game build, then each imported DLL and its functions with their slot and how many
    /// references read it. With <paramref name="filter"/>, only DLLs or functions whose name holds it (ignoring case),
    /// each followed by its references. Never writes the exe's path.</summary>
    /// <returns>0 when the imports were written, 1 when <paramref name="filter"/> matched none, 2 for a file that
    /// isn't a PE image.</returns>
    public static int Run(string gameExe, string? filter, IReadOnlyList<GameBuild> knownBuilds, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(knownBuilds);
        ArgumentNullException.ThrowIfNull(output);
        var bytes = File.ReadAllBytes(gameExe);
        ExeBytes.WriteBuild(bytes, knownBuilds, output);
        var headers = ExeBytes.HeadersOrNone(bytes);
        if (headers is null)
        {
            output.WriteLine("the file is not a PE image");
            return 2;
        }

        var image = new Image(bytes, headers);
        var imports = Imports(image, headers.PEHeader!.ImportTableDirectory.RelativeVirtualAddress, delayed: false)
            .Concat(Imports(image, headers.PEHeader.DelayImportTableDirectory.RelativeVirtualAddress, delayed: true))
            .Where(i => filter is null
                || i.Dll.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || i.Function.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (imports.Count == 0)
        {
            output.WriteLine(filter is null ? "no imports" : $"no import matches {filter}");
            return filter is null ? 0 : 1;
        }

        var references = References(bytes, headers, imports.Select(i => i.Slot).ToHashSet());
        foreach (var dll in imports.GroupBy(i => (i.Dll, i.Delayed)))
        {
            output.WriteLine(dll.Key.Delayed ? $"{dll.Key.Dll} (delay-loaded)" : dll.Key.Dll);
            foreach (var import in dll)
            {
                var found = references.GetValueOrDefault(import.Slot) ?? [];
                output.WriteLine(Invariant($"  {import.Function}  slot 0x{import.Slot:x}  {found.Count} references"));
                if (filter is null)
                {
                    continue;
                }
                foreach (var (at, text) in found.Take(MaxReferencesShown))
                {
                    output.WriteLine(Invariant($"    0x{at:x}  {text} ({ExeBytes.SectionName(headers, at)})"));
                }
                if (found.Count > MaxReferencesShown)
                {
                    output.WriteLine(Invariant($"    and {found.Count - MaxReferencesShown} more"));
                }
            }
        }
        return 0;
    }

    /// <summary>The functions of one import directory: the regular one (20-byte descriptors: name table, …, DLL name,
    /// slots) or the delay-load one (32-byte descriptors: attributes, DLL name, module handle, slots, name table).
    /// Stops at the zero descriptor or at anything outside the file.</summary>
    private static IEnumerable<Import> Imports(Image image, int directory, bool delayed)
    {
        if (directory == 0)
        {
            yield break;
        }
        var size = delayed ? 32 : 20;
        for (long descriptor = directory; ; descriptor += size)
        {
            var (dllName, slots, names) = delayed
                ? (image.U32(descriptor + 4), image.U32(descriptor + 12), image.U32(descriptor + 16))
                : (image.U32(descriptor + 12), image.U32(descriptor + 16), image.U32(descriptor));
            if (dllName is null or 0 || slots is null or 0)
            {
                yield break;
            }
            var dll = image.Ascii(dllName.Value) ?? "?";
            var table = names is null or 0 ? slots.Value : names.Value;
            for (long i = 0; ; i++)
            {
                var entry = image.U64(table + (i * 8));
                if (entry is null or 0)
                {
                    break;
                }
                var function = (entry.Value & (1UL << 63)) != 0
                    ? Invariant($"ordinal {entry.Value & 0xFFFF}")
                    : image.Ascii((long)(entry.Value & 0x7FFFFFFF) + 2) ?? "?";
                yield return new Import(dll, function, slots.Value + (i * 8), delayed);
            }
        }
    }

    /// <summary>Every instruction in an executable section whose rip-relative operand is one of
    /// <paramref name="slots"/>, by slot, in address order.</summary>
    private static Dictionary<long, List<(long At, string Text)>> References(byte[] bytes, PEHeaders headers, HashSet<long> slots)
    {
        var found = new Dictionary<long, List<(long, string)>>();
        foreach (var section in ExecutableSections(headers))
        {
            var code = Code(bytes, section);
            for (var i = 0; i + 6 <= code.Length; i++)
            {
                var reference = Read(code[i..]);
                if (reference is not var (size, text))
                {
                    continue;
                }
                var at = section.VirtualAddress + (long)i;
                var slot = at + size + BinaryPrimitives.ReadInt32LittleEndian(code[(i + size - 4)..]);
                if (slots.Contains(slot))
                {
                    if (!found.TryGetValue(slot, out var list))
                    {
                        found[slot] = list = [];
                    }
                    list.Add((at, text));
                    // Past this instruction, so the FF 15 inside a REX-prefixed call isn't counted again.
                    i += size - 1;
                }
            }
        }
        AddThunkCallers(bytes, headers, found);
        return found;
    }

    /// <summary>A <c>jmp [slot]</c> is usually an import thunk the code calls directly, so this adds each
    /// <c>call</c> or <c>jmp</c> rel32 (E8, E9) that lands on one, under that thunk's slot.</summary>
    private static void AddThunkCallers(byte[] bytes, PEHeaders headers, Dictionary<long, List<(long At, string Text)>> found)
    {
        var thunks = found
            .SelectMany(slot => slot.Value.Where(r => r.Text.StartsWith("jmp", StringComparison.Ordinal)).Select(r => (r.At, Slot: slot.Key)))
            .ToDictionary(t => t.At, t => t.Slot);
        if (thunks.Count == 0)
        {
            return;
        }
        foreach (var section in ExecutableSections(headers))
        {
            var code = Code(bytes, section);
            for (var i = 0; i + 5 <= code.Length; i++)
            {
                if (code[i] is not (0xE8 or 0xE9))
                {
                    continue;
                }
                var at = section.VirtualAddress + (long)i;
                var target = at + 5 + BinaryPrimitives.ReadInt32LittleEndian(code[(i + 1)..]);
                if (thunks.TryGetValue(target, out var slot))
                {
                    found[slot].Add((at, Invariant($"{(code[i] == 0xE8 ? "call" : "jmp")} 0x{target:x} (thunk)")));
                }
            }
        }
    }

    private static IEnumerable<SectionHeader> ExecutableSections(PEHeaders headers) =>
        headers.SectionHeaders.Where(s => ((uint)s.SectionCharacteristics & ExecutableSection) != 0);

    private static ReadOnlySpan<byte> Code(byte[] bytes, SectionHeader section)
    {
        var start = Math.Clamp(section.PointerToRawData, 0, bytes.Length);
        return bytes.AsSpan(start, Math.Clamp(section.SizeOfRawData, 0, bytes.Length - start));
    }

    /// <summary>The size and reading of an instruction that reads memory at rip + a 32-bit offset (its last four
    /// bytes), or null.</summary>
    private static (int Size, string Text)? Read(ReadOnlySpan<byte> code) => code switch
    {
        [0xFF, 0x15, ..] => (6, "call [slot]"),
        [0xFF, 0x25, ..] => (6, "jmp [slot]"),
        [>= 0x40 and <= 0x4F, 0xFF, 0x15, _, _, _, _, ..] => (7, "call [slot]"),
        [>= 0x40 and <= 0x4F, 0xFF, 0x25, _, _, _, _, ..] => (7, "jmp [slot]"),
        [0x48 or 0x4C, 0x8B, var modrm, _, _, _, _, ..] when (modrm & 0xC7) == 0x05 =>
            (7, $"mov {Registers[((code[0] & 0x04) << 1) | ((modrm >> 3) & 7)]}, [slot]"),
        _ => null,
    };

    private static readonly string[] Registers =
        ["rax", "rcx", "rdx", "rbx", "rsp", "rbp", "rsi", "rdi", "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15"];

    /// <summary>Reads the image's bytes by rva through the section table; null for an rva the file doesn't hold.</summary>
    private sealed class Image(byte[] bytes, PEHeaders headers)
    {
        public uint? U32(long rva) => At(rva, 4) is { } offset ? BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) : null;

        public ulong? U64(long rva) => At(rva, 8) is { } offset ? BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset)) : null;

        public string? Ascii(long rva)
        {
            if (At(rva, 1) is not { } offset)
            {
                return null;
            }
            var end = Array.IndexOf(bytes, (byte)0, offset, Math.Min(256, bytes.Length - offset));
            return end < 0 ? null : Encoding.ASCII.GetString(bytes, offset, end - offset);
        }

        private int? At(long rva, int length)
        {
            if (ExeBytes.Place(headers, rva) is not var (_, offset, inFileUntil) || rva + length > inFileUntil
                || offset + length > bytes.Length)
            {
                return null;
            }
            return (int)offset;
        }
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
