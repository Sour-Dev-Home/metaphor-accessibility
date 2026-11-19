using System.Buffers.Binary;
using System.Text;

namespace Metaphor.Core.Tests;

/// <summary>The exe import lister (issue #58), run against a made-up PE file: an executable <c>.text</c> at 0x1000 whose
/// instructions read two import slots, and an <c>.idata</c> at 0x2000 that imports one function by name and one by
/// ordinal from one DLL.</summary>
[Trait("Category", "Unit")]
public sealed class ExeImportsTests : IDisposable
{
    private const int HeadersSize = 0x200;
    private const int RawSize = 0x200;
    private const int OptionalHeader = 0x58;

    private readonly string folder = Directory.CreateTempSubdirectory("metaphor-exeimports-").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    /// <summary>The exe, with its import table at 0x2000 (regular) or its delay-load table at 0x20c0. Both describe the
    /// same DLL and slots: GetKeyboardState at slot 0x2060, ordinal 3 at slot 0x2068. <paramref name="edit"/> changes
    /// the finished file, and <paramref name="length"/> cuts it short.</summary>
    private string MadeUpExe(bool delayLoaded = false, bool noImportTable = false, Action<byte[]>? edit = null, int? length = null)
    {
        var file = new byte[HeadersSize + (2 * RawSize)];
        var span = file.AsSpan();
        file[0] = (byte)'M';
        file[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(span[0x3C..], 0x40);
        "PE\0\0"u8.CopyTo(span[0x40..]);
        var coff = span[0x44..];
        BinaryPrimitives.WriteUInt16LittleEndian(coff, 0x8664);
        BinaryPrimitives.WriteUInt16LittleEndian(coff[2..], 2);
        BinaryPrimitives.WriteUInt16LittleEndian(coff[16..], 240);
        BinaryPrimitives.WriteUInt16LittleEndian(coff[18..], 0x22);
        var optional = span[OptionalHeader..];
        BinaryPrimitives.WriteUInt16LittleEndian(optional, 0x20B);
        BinaryPrimitives.WriteInt64LittleEndian(optional[24..], 0x140000000);
        BinaryPrimitives.WriteInt32LittleEndian(optional[32..], 0x1000);
        BinaryPrimitives.WriteInt32LittleEndian(optional[36..], 0x200);
        BinaryPrimitives.WriteInt32LittleEndian(optional[56..], 0x10000);
        BinaryPrimitives.WriteInt32LittleEndian(optional[60..], HeadersSize);
        BinaryPrimitives.WriteUInt16LittleEndian(optional[68..], 3);
        BinaryPrimitives.WriteInt32LittleEndian(optional[108..], 16);
        // Data directory 1 is the import table, 13 the delay-load one.
        if (!noImportTable)
        {
            BinaryPrimitives.WriteInt32LittleEndian(optional[(delayLoaded ? 112 + (13 * 8) : 112 + 8)..], delayLoaded ? 0x20C0 : 0x2000);
        }

        Section(span, 0, ".text", 0x1000, 0x60000020, Code());
        Section(span, 1, ".idata", 0x2000, 0x40000040, ImportData());
        edit?.Invoke(file);
        var path = Path.Combine(folder, "game.exe");
        File.WriteAllBytes(path, length is { } cut ? file[..cut] : file);
        return path;
    }

    // Where the edits land: a section header's fields, and the bytes at an rva in .text or .idata.
    private const int CodeSectionHeader = OptionalHeader + 240;
    private const int ImportSectionHeader = CodeSectionHeader + 40;

    private static int FileOffset(int rva) => rva < 0x2000 ? HeadersSize + rva - 0x1000 : HeadersSize + RawSize + rva - 0x2000;

    private static void Put32(byte[] file, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(at), value);

    private static void Put64(byte[] file, int at, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(at), value);

    /// <summary>Puts <paramref name="opcode"/> at <paramref name="rva"/> in .text, followed by the 32-bit displacement
    /// that makes it reach <paramref name="target"/> from the next instruction.</summary>
    private static void Instruction(byte[] file, int rva, int target, params byte[] opcode)
    {
        opcode.CopyTo(file, FileOffset(rva));
        var next = rva + opcode.Length + 4;
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(FileOffset(rva) + opcode.Length), target - next);
    }

    private static string[] ReferenceLines(string[] lines) =>
        [.. lines.Where(line => line.StartsWith("    0x", StringComparison.Ordinal))];

    private static void Section(Span<byte> file, int index, string name, int rva, uint characteristics, byte[] data)
    {
        var header = file[(OptionalHeader + 240 + (index * 40))..];
        Encoding.ASCII.GetBytes(name).CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], RawSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[12..], rva);
        BinaryPrimitives.WriteInt32LittleEndian(header[16..], RawSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[20..], HeadersSize + (index * RawSize));
        BinaryPrimitives.WriteUInt32LittleEndian(header[36..], characteristics);
        data.CopyTo(file[(HeadersSize + (index * RawSize))..]);
    }

    /// <summary>Each instruction's operand is relative to the next instruction.</summary>
    private static byte[] Code()
    {
        var code = new byte[RawSize];
        Array.Fill(code, (byte)0xCC);
        // 0x1000: call [0x2060]; 0x1010: mov rax, [0x2068]; 0x1020: jmp [0x2068], a thunk; 0x1030: call 0x1020;
        // 0x1040: rex call [0x2060], whose FF 15 must not count twice.
        ((byte[])[0xFF, 0x15, 0x5A, 0x10, 0x00, 0x00]).CopyTo(code, 0x00);
        ((byte[])[0x48, 0x8B, 0x05, 0x51, 0x10, 0x00, 0x00]).CopyTo(code, 0x10);
        ((byte[])[0xFF, 0x25, 0x42, 0x10, 0x00, 0x00]).CopyTo(code, 0x20);
        ((byte[])[0xE8, 0xEB, 0xFF, 0xFF, 0xFF]).CopyTo(code, 0x30);
        ((byte[])[0x48, 0xFF, 0x15, 0x19, 0x10, 0x00, 0x00]).CopyTo(code, 0x40);
        return code;
    }

    private static byte[] ImportData()
    {
        var data = new byte[RawSize];
        var span = data.AsSpan();
        // Regular descriptor at 0x2000: name table 0x2040, DLL name 0x2080, slots 0x2060; then a zero descriptor.
        BinaryPrimitives.WriteInt32LittleEndian(span[0x00..], 0x2040);
        BinaryPrimitives.WriteInt32LittleEndian(span[0x0C..], 0x2080);
        BinaryPrimitives.WriteInt32LittleEndian(span[0x10..], 0x2060);
        foreach (var table in new[] { 0x40, 0x60 })
        {
            BinaryPrimitives.WriteUInt64LittleEndian(span[table..], 0x2090);
            BinaryPrimitives.WriteUInt64LittleEndian(span[(table + 8)..], 0x8000000000000003);
        }
        "XINPUT1_4.dll\0"u8.CopyTo(span[0x80..]);
        "\0\0GetKeyboardState\0"u8.CopyTo(span[0x90..]);
        // Delay-load descriptor at 0x20c0: attributes, DLL name, module handle, slots, name table; then a zero one.
        BinaryPrimitives.WriteInt32LittleEndian(span[0xC0..], 1);
        BinaryPrimitives.WriteInt32LittleEndian(span[0xC4..], 0x2080);
        BinaryPrimitives.WriteInt32LittleEndian(span[0xC8..], 0x21F0);
        BinaryPrimitives.WriteInt32LittleEndian(span[0xCC..], 0x2060);
        BinaryPrimitives.WriteInt32LittleEndian(span[0xD0..], 0x2040);
        return data;
    }

    private static (int ExitCode, string[] Lines) Run(string exe, string? filter = null)
    {
        using var output = new StringWriter();
        var exitCode = ExeImports.Run(exe, filter, [], output);
        return (exitCode, output.ToString().ReplaceLineEndings("\n").Split('\n'));
    }

    [Fact]
    public void ListsEachImportWithItsSlotAndHowManyInstructionsReadIt()
    {
        var (exitCode, lines) = Run(MadeUpExe());

        Assert.Equal(0, exitCode);
        Assert.StartsWith("game build ", lines[0], StringComparison.Ordinal);
        Assert.Equal("XINPUT1_4.dll", lines[1]);
        Assert.Equal("  GetKeyboardState  slot 0x2060  2 references", lines[2]);
        Assert.Equal("  ordinal 3  slot 0x2068  3 references", lines[3]);
    }

    [Fact]
    public void AFilterListsTheReferencesIncludingCallsToAThunk()
    {
        var (_, lines) = Run(MadeUpExe(), "ordinal");

        Assert.Equal(
            ["  ordinal 3  slot 0x2068  3 references",
             "    0x1010  mov rax, [slot] (.text)",
             "    0x1020  jmp [slot] (.text)",
             "    0x1030  call 0x1020 (thunk) (.text)"],
            lines[2..6]);
        Assert.DoesNotContain(lines, line => line.Contains("GetKeyboardState", StringComparison.Ordinal));
    }

    [Fact]
    public void ARexPrefixedCallIsOneReferenceAtItsPrefix()
    {
        var (_, lines) = Run(MadeUpExe(), "getkeyboard");

        Assert.Equal(["    0x1000  call [slot] (.text)", "    0x1040  call [slot] (.text)"], lines[3..5]);
    }

    [Fact]
    public void ListsDelayLoadedImports()
    {
        var (_, lines) = Run(MadeUpExe(delayLoaded: true));

        Assert.Equal("XINPUT1_4.dll (delay-loaded)", lines[1]);
        Assert.Equal("  GetKeyboardState  slot 0x2060  2 references", lines[2]);
    }

    [Fact]
    public void AFilterThatMatchesNothingExitsOne()
    {
        var (exitCode, lines) = Run(MadeUpExe(), "dinput8");

        Assert.Equal(1, exitCode);
        Assert.Equal("no import matches dinput8", lines[1]);
    }

    [Fact]
    public void AFileThatIsNotAPeImageExitsTwo()
    {
        var path = Path.Combine(folder, "not.exe");
        File.WriteAllBytes(path, [1, 2, 3]);

        var (exitCode, lines) = Run(path);

        Assert.Equal(2, exitCode);
        Assert.Equal("the file is not a PE image", lines[1]);
    }

    [Fact]
    public void MissingBuildsOrOutputAreRefused()
    {
        var missing = Path.Combine(folder, "missing.exe");

        Assert.Throws<ArgumentNullException>("knownBuilds", () => ExeImports.Run(missing, null, null!, TextWriter.Null));
        Assert.Throws<ArgumentNullException>("output", () => ExeImports.Run(missing, null, [], null!));
    }

    [Fact]
    public void AnExeWithoutAnImportTableListsNoneWhateverItsDosHeaderHolds()
    {
        // DOS header bytes that would read as descriptors pointing at the made-up names.
        var (exitCode, lines) = Run(MadeUpExe(noImportTable: true, edit: file =>
        {
            for (var at = 4; at < 0x3C; at += 4)
            {
                Put32(file, at, 0x2040);
            }
        }));

        Assert.Equal(0, exitCode);
        Assert.Equal("no imports", lines[1]);
    }

    /// <summary>A second DLL's name at 0x2180 and a function's hint and name at 0x2190.</summary>
    private static void SecondDllNames(byte[] file)
    {
        "USER32.dll\0"u8.CopyTo(file.AsSpan(FileOffset(0x2180)));
        "\0\0GetCursorPos\0"u8.CopyTo(file.AsSpan(FileOffset(0x2190)));
    }

    [Fact]
    public void ListsEachDllInTheImportTableByItsNameTable()
    {
        var (_, lines) = Run(MadeUpExe(edit: file =>
        {
            SecondDllNames(file);
            // Second descriptor at 0x2014: name table 0x2140, DLL name 0x2180, slots 0x2150 already bound to an address.
            Put32(file, FileOffset(0x2014), 0x2140);
            Put32(file, FileOffset(0x2014 + 12), 0x2180);
            Put32(file, FileOffset(0x2014 + 16), 0x2150);
            Put64(file, FileOffset(0x2140), 0x2190);
            Put64(file, FileOffset(0x2150), 0x7FF812345678);
        }));

        Assert.Equal(["USER32.dll", "  GetCursorPos  slot 0x2150  0 references"], lines[4..6]);
    }

    [Fact]
    public void ListsEachDllInTheDelayLoadTableAndReadsOneWithoutANameTableFromItsSlots()
    {
        var (_, lines) = Run(MadeUpExe(delayLoaded: true, edit: file =>
        {
            SecondDllNames(file);
            // Second descriptor at 0x20e0: DLL name 0x2180, slots 0x2160, no name table.
            Put32(file, FileOffset(0x20E0), 1);
            Put32(file, FileOffset(0x20E0 + 4), 0x2180);
            Put32(file, FileOffset(0x20E0 + 12), 0x2160);
            Put64(file, FileOffset(0x2160), 0x2190);
        }));

        Assert.Equal(["USER32.dll (delay-loaded)", "  GetCursorPos  slot 0x2160  0 references"], lines[4..6]);
    }

    [Fact]
    public void ADescriptorWithoutADllNameEndsTheTable()
    {
        var (_, lines) = Run(MadeUpExe(edit: file =>
        {
            Put32(file, FileOffset(0x2014 + 16), 0x2150);
            Put64(file, FileOffset(0x2150), 0x7FF812345678);
        }));

        Assert.Equal("  ordinal 3  slot 0x2068  3 references", lines[3]);
        Assert.DoesNotContain(lines, line => line.Contains("slot 0x2150", StringComparison.Ordinal));
    }

    [Fact]
    public void OnlyExecutableSectionsAreSearchedForReferences()
    {
        var (_, lines) = Run(MadeUpExe(edit: file => Put32(file, CodeSectionHeader + 36, 0x40000040)));

        Assert.Equal("  GetKeyboardState  slot 0x2060  0 references", lines[2]);
        Assert.Equal("  ordinal 3  slot 0x2068  0 references", lines[3]);
    }

    [Fact]
    public void ACodeSectionLongerThanTheFileIsSearchedToTheFilesEnd()
    {
        var (exitCode, lines) = Run(MadeUpExe(edit: file => Put32(file, CodeSectionHeader + 16, 0x10000)));

        Assert.Equal(0, exitCode);
        Assert.Equal("  GetKeyboardState  slot 0x2060  2 references", lines[2]);
    }

    [Fact]
    public void AnExeCutShortListsWhatItHolds()
    {
        // Cut inside the name table's second entry, before the DLL and function names.
        var (exitCode, lines) = Run(MadeUpExe(length: FileOffset(0x204C)));

        Assert.Equal(0, exitCode);
        Assert.Equal(["?", "  ?  slot 0x2060  2 references"], lines[1..3]);
        Assert.DoesNotContain(lines, line => line.Contains("slot 0x2068", StringComparison.Ordinal));
    }

    [Fact]
    public void ATableEntryPastItsSectionsRawDataEndsTheTable()
    {
        var (_, lines) = Run(MadeUpExe(edit: file =>
        {
            // .idata keeps 0x100 bytes of raw data; the file's bytes after them aren't the section's.
            Put32(file, ImportSectionHeader + 16, 0x100);
            Put32(file, FileOffset(0x2000), 0x20F8);
            Put64(file, FileOffset(0x20F8), 0x2090);
            Put64(file, FileOffset(0x2100), 0x2090);
        }));

        Assert.Equal("  GetKeyboardState  slot 0x2060  2 references", lines[2]);
        Assert.DoesNotContain(lines, line => line.Contains("slot 0x2068", StringComparison.Ordinal));
    }

    [Fact]
    public void ANameRunningOffTheFilesEndIsUnknown()
    {
        var (_, lines) = Run(MadeUpExe(edit: file =>
        {
            Put32(file, FileOffset(0x200C), 0x21FA);
            "XINPUT"u8.CopyTo(file.AsSpan(FileOffset(0x21FA)));
        }));

        Assert.Equal("?", lines[1]);
    }

    [Theory]
    [InlineData(ExeImports.MaxReferencesShown, "")]
    [InlineData(ExeImports.MaxReferencesShown + 1, "    and 1 more")]
    public void AFilterShowsAtMostSoManyReferencesEach(int references, string after)
    {
        // Two instructions read the slot already; the rest are calls from 0x1060 on.
        var exe = MadeUpExe(edit: file =>
        {
            for (var i = 0; i < references - 2; i++)
            {
                Instruction(file, 0x1060 + (6 * i), 0x2060, 0xFF, 0x15);
            }
        });

        var (_, lines) = Run(exe, "getkeyboard");

        Assert.Equal($"  GetKeyboardState  slot 0x2060  {references} references", lines[2]);
        Assert.Equal(ExeImports.MaxReferencesShown, ReferenceLines(lines).Length);
        Assert.Equal(after, lines[3 + ExeImports.MaxReferencesShown]);
    }

    [Fact]
    public void AnyRexPrefixMakesAReferenceOneByteLonger()
    {
        var (_, lines) = Run(MadeUpExe(edit: file =>
        {
            Instruction(file, 0x1100, 0x2060, 0x40, 0xFF, 0x15);
            Instruction(file, 0x1110, 0x2060, 0x4F, 0xFF, 0x15);
            Instruction(file, 0x1120, 0x2060, 0x40, 0xFF, 0x25);
            Instruction(file, 0x1130, 0x2060, 0x4F, 0xFF, 0x25);
            // After the 0xCC filler, which is no prefix.
            Instruction(file, 0x1141, 0x2060, 0xFF, 0x15);
        }), "getkeyboard");

        Assert.Equal(
            ["    0x1000  call [slot] (.text)",
             "    0x1040  call [slot] (.text)",
             "    0x1100  call [slot] (.text)",
             "    0x1110  call [slot] (.text)",
             "    0x1120  jmp [slot] (.text)",
             "    0x1130  jmp [slot] (.text)",
             "    0x1141  call [slot] (.text)"],
            ReferenceLines(lines));
    }

    [Fact]
    public void AMovNamesTheRegisterItLoads()
    {
        string[] registers =
            ["rax", "rcx", "rdx", "rbx", "rsp", "rbp", "rsi", "rdi", "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15"];
        var (_, lines) = Run(MadeUpExe(edit: file =>
        {
            for (var r = 0; r < registers.Length; r++)
            {
                Instruction(file, 0x1100 + (8 * r), 0x2060, (byte)(r < 8 ? 0x48 : 0x4C), 0x8B, (byte)(0x05 | ((r & 7) << 3)));
            }
        }), "getkeyboard");

        Assert.Equal(
            registers.Select((name, r) => $"    0x{0x1100 + (8 * r):x}  mov {name}, [slot] (.text)"),
            ReferenceLines(lines)[2..]);
    }

    [Fact]
    public void OnlyCallsAndJumpsToAThunkCountAsItsCallers()
    {
        var exe = MadeUpExe(edit: file =>
        {
            // A call to a call, which is no thunk.
            Instruction(file, 0x1150, 0x1000, 0xE8);
            Instruction(file, 0x1160, 0x1020, 0xE9);
            // Not a call or a jump.
            Instruction(file, 0x1170, 0x1020, 0x90);
            // The last five bytes of .text.
            Instruction(file, 0x11FB, 0x1020, 0xE8);
        });

        Assert.Equal("  GetKeyboardState  slot 0x2060  2 references", Run(exe).Lines[2]);
        Assert.Equal(
            ["    0x1010  mov rax, [slot] (.text)",
             "    0x1020  jmp [slot] (.text)",
             "    0x1030  call 0x1020 (thunk) (.text)",
             "    0x1160  jmp 0x1020 (thunk) (.text)",
             "    0x11fb  call 0x1020 (thunk) (.text)"],
            ReferenceLines(Run(exe, "ordinal").Lines));
    }
}
