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
    /// same DLL and slots: GetKeyboardState at slot 0x2060, ordinal 3 at slot 0x2068.</summary>
    private string MadeUpExe(bool delayLoaded = false)
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
        BinaryPrimitives.WriteInt32LittleEndian(optional[(delayLoaded ? 112 + (13 * 8) : 112 + 8)..], delayLoaded ? 0x20C0 : 0x2000);

        Section(span, 0, ".text", 0x1000, 0x60000020, Code());
        Section(span, 1, ".idata", 0x2000, 0x40000040, ImportData());
        var path = Path.Combine(folder, "game.exe");
        File.WriteAllBytes(path, file);
        return path;
    }

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
}
