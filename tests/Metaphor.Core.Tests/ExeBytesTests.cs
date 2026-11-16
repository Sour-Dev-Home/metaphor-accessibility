using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Metaphor.Core.Tests;

/// <summary>The exe byte reader (issue #70), run against a made-up PE file: headers we write by hand, with sections
/// at known addresses holding known bytes.</summary>
[Trait("Category", "Unit")]
public sealed class ExeBytesTests : IDisposable
{
    private const int HeadersSize = 0x200;
    private const int RawSize = 0x200;

    private readonly string folder = Directory.CreateTempSubdirectory("metaphor-exebytes-").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    /// <summary>A section of the made-up exe: its name, where it loads, its bytes in the file (padded to
    /// <see cref="RawSize"/>), and how much memory it takes (more than its file bytes when set).</summary>
    private sealed record Section(string Name, int Rva, byte[] Data, int VirtualSize = RawSize);

    /// <summary>A minimal PE32+ image: a DOS header pointing at the PE header, a COFF header, an optional header with
    /// 16 empty data directories, then the section table. Section i's file bytes start at
    /// <see cref="HeadersSize"/> + i * <see cref="RawSize"/>.</summary>
    private string MadeUpExe(params Section[] sections)
    {
        var file = new byte[HeadersSize + (sections.Length * RawSize)];
        var span = file.AsSpan();
        file[0] = (byte)'M';
        file[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(span[0x3C..], 0x40);
        "PE\0\0"u8.CopyTo(span[0x40..]);
        var coff = span[0x44..];
        BinaryPrimitives.WriteUInt16LittleEndian(coff, 0x8664);
        BinaryPrimitives.WriteUInt16LittleEndian(coff[2..], (ushort)sections.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(coff[16..], 240);
        BinaryPrimitives.WriteUInt16LittleEndian(coff[18..], 0x22);
        var optional = span[0x58..];
        BinaryPrimitives.WriteUInt16LittleEndian(optional, 0x20B);
        BinaryPrimitives.WriteInt64LittleEndian(optional[24..], 0x140000000);
        BinaryPrimitives.WriteInt32LittleEndian(optional[32..], 0x1000);
        BinaryPrimitives.WriteInt32LittleEndian(optional[36..], 0x200);
        BinaryPrimitives.WriteInt32LittleEndian(optional[56..], 0x10000);
        BinaryPrimitives.WriteInt32LittleEndian(optional[60..], HeadersSize);
        BinaryPrimitives.WriteUInt16LittleEndian(optional[68..], 3);
        BinaryPrimitives.WriteInt32LittleEndian(optional[108..], 16);
        for (var i = 0; i < sections.Length; i++)
        {
            var header = span[(0x58 + 240 + (i * 40))..];
            System.Text.Encoding.ASCII.GetBytes(sections[i].Name).CopyTo(header);
            BinaryPrimitives.WriteInt32LittleEndian(header[8..], sections[i].VirtualSize);
            BinaryPrimitives.WriteInt32LittleEndian(header[12..], sections[i].Rva);
            BinaryPrimitives.WriteInt32LittleEndian(header[16..], RawSize);
            BinaryPrimitives.WriteInt32LittleEndian(header[20..], HeadersSize + (i * RawSize));
            sections[i].Data.CopyTo(span[(HeadersSize + (i * RawSize))..]);
        }
        var path = Path.Combine(folder, "game.exe");
        File.WriteAllBytes(path, file);
        return path;
    }

    private static byte[] Code(int length, params (int At, byte[] Bytes)[] parts)
    {
        var code = new byte[length];
        Array.Fill(code, (byte)0x90);
        foreach (var (at, bytes) in parts)
        {
            bytes.CopyTo(code, at);
        }
        return code;
    }

    private static (int ExitCode, string Output) Run(string exe, long rva, int length, params GameBuild[] knownBuilds)
    {
        using var output = new StringWriter();
        var exitCode = ExeBytes.Run(exe, rva, length, knownBuilds, output);
        return (exitCode, output.ToString());
    }

    private static string[] Lines(string output) => output.ReplaceLineEndings("\n").Split('\n');

    [Fact]
    public void PrintsTheBytesAtAnRvaFromTheSectionThatHoldsIt()
    {
        var exe = MadeUpExe(new Section(".text", 0x1000, Code(0x20, (4, [0x48, 0x83, 0xEC, 0x48]))));

        var (exitCode, output) = Run(exe, 0x1004, 4);

        Assert.Equal(0, exitCode);
        Assert.Contains("rva 0x1004 in .text, file offset 0x204, 4 bytes", output, StringComparison.Ordinal);
        Assert.Contains("0x1004  48 83 EC 48", Lines(output));
    }

    [Fact]
    public void NamesTheGameBuildItRead()
    {
        var exe = MadeUpExe(new Section(".text", 0x1000, Code(0x20)));
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(exe)));

        var (_, known) = Run(exe, 0x1000, 1, new GameBuild(sha, 18330018, null, "test"));
        var (_, unknown) = Run(exe, 0x1000, 1);

        Assert.StartsWith($"game build {sha} (Steam build 18330018)", known, StringComparison.Ordinal);
        Assert.StartsWith($"game build {sha} (not a known build)", unknown, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintsSixteenBytesALine()
    {
        var exe = MadeUpExe(new Section(".text", 0x1000, Code(0x20, (0x10, [0xAB]))));

        var (_, output) = Run(exe, 0x1000, 0x11);

        Assert.Contains("0x1000  90 90 90 90 90 90 90 90 90 90 90 90 90 90 90 90", Lines(output));
        Assert.Contains("0x1010  AB", Lines(output));
    }

    [Fact]
    public void ListsACallsTargetWithTheSectionItLandsIn()
    {
        // E8 rel32 at 0x1010: the target is relative to the next instruction, 0x1015, so -0x15 lands on 0x1000.
        var exe = MadeUpExe(new Section(".text", 0x1000, Code(0x20, (0x10, [0xE8, 0xEB, 0xFF, 0xFF, 0xFF]))));

        var (_, output) = Run(exe, 0x1010, 5);

        Assert.Contains("0x1010  call 0x1000 (.text)", Lines(output));
    }

    [Fact]
    public void ListsJumpsIntoAnotherSection()
    {
        // E9 rel32 at 0x1000 to 0x3000 (0x3000 - 0x1005 = 0x1ffb); EB rel8 at 0x1005 to 0x1009; 0F 84 rel32 at 0x1007
        // to 0x100d + 0x10 = 0x101d.
        var exe = MadeUpExe(
            new Section(".text", 0x1000, Code(0x20,
                (0, [0xE9, 0xFB, 0x1F, 0x00, 0x00]), (5, [0xEB, 0x02]), (7, [0x0F, 0x84, 0x10, 0x00, 0x00, 0x00]))),
            new Section(".text1", 0x3000, Code(0x20)));

        var (_, output) = Run(exe, 0x1000, 0xd);

        Assert.Contains("0x1000  jmp 0x3000 (.text1)", Lines(output));
        Assert.Contains("0x1005  jmp 0x1009 (.text)", Lines(output));
        Assert.Contains("0x1007  jcc 0x101d (.text)", Lines(output));
    }

    [Fact]
    public void ListsIndirectCallsAndJumpsThroughThePointerTheyRead()
    {
        // FF 15 disp32 at 0x1000 reads the pointer at 0x1006 + 0xffa = 0x2000; FF 25 at 0x1006 reads 0x100c + 0xff4.
        var exe = MadeUpExe(
            new Section(".text", 0x1000, Code(0x20, (0, [0xFF, 0x15, 0xFA, 0x0F, 0, 0]), (6, [0xFF, 0x25, 0xF4, 0x0F, 0, 0]))),
            new Section(".rdata", 0x2000, Code(0x20)));

        var (_, output) = Run(exe, 0x1000, 0xc);

        Assert.Contains("0x1000  call [0x2000] (.rdata)", Lines(output));
        Assert.Contains("0x1006  jmp [0x2000] (.rdata)", Lines(output));
    }

    [Fact]
    public void LeavesOutCandidatesThatLandOutsideTheImageOrRunPastTheBytesRead()
    {
        // E8 at 0x1000 targets 0x7f001005, in no section; E8 at 0x1005 has only two of its four offset bytes read.
        var exe = MadeUpExe(new Section(".text", 0x1000, Code(0x20,
            (0, [0xE8, 0x00, 0x00, 0x00, 0x7F]), (5, [0xE8, 0xF6, 0xFF, 0xFF, 0xFF]))));

        var (_, output) = Run(exe, 0x1000, 8);

        Assert.DoesNotContain("  call ", output, StringComparison.Ordinal);
        Assert.Contains("no candidate call or jump targets", output, StringComparison.Ordinal);
    }

    [Fact]
    public void FailsForAnRvaInNoSection()
    {
        var exe = MadeUpExe(new Section(".text", 0x1000, Code(0x20)));

        var (exitCode, output) = Run(exe, 0x5000, 4);

        Assert.Equal(1, exitCode);
        Assert.Contains("rva 0x5000 is in no section", output, StringComparison.Ordinal);
    }

    [Fact]
    public void FailsForBytesTheFileDoesNotHold()
    {
        // .bss-like: 0x1000 bytes in memory, only RawSize of them in the file; the rest is zero-filled at load.
        var exe = MadeUpExe(new Section(".data", 0x1000, Code(0x20), VirtualSize: 0x1000));

        var (beyond, beyondOutput) = Run(exe, 0x1000 + RawSize, 4);
        var (crossing, crossingOutput) = Run(exe, 0x1000 + RawSize - 2, 4);

        Assert.Equal(1, beyond);
        Assert.Contains("not in the file", beyondOutput, StringComparison.Ordinal);
        Assert.Equal(1, crossing);
        Assert.Contains("not in the file", crossingOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void FailsForBytesPastTheEndOfATruncatedFile()
    {
        var exe = MadeUpExe(new Section(".text", 0x1000, Code(0x20)));
        File.WriteAllBytes(exe, File.ReadAllBytes(exe)[..(HeadersSize + 0x10)]);

        var (exitCode, output) = Run(exe, 0x1008, 0x10);

        Assert.Equal(1, exitCode);
        Assert.Contains("not in the file", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsTheHeadersAtTheImagesStart()
    {
        var exe = MadeUpExe(new Section(".text", 0x1000, Code(0x20)));

        var (exitCode, output) = Run(exe, 0, 2);

        Assert.Equal(0, exitCode);
        Assert.Contains("rva 0x0 in the headers, file offset 0x0, 2 bytes", output, StringComparison.Ordinal);
        Assert.Contains("0x0  4D 5A", Lines(output));
    }

    [Fact]
    public void RefusesAFileThatIsNotAPortableExecutable()
    {
        var exe = Path.Combine(folder, "notes.exe");
        File.WriteAllBytes(exe, [1, 2, 3, 4]);

        var (exitCode, output) = Run(exe, 0, 2);

        Assert.Equal(2, exitCode);
        Assert.Contains("not a PE image", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0x9f8240", 0x9f8240)]
    [InlineData("0X10", 0x10)]
    [InlineData("64", 64)]
    public void ParsesNumbersAsHexWith0xAndDecimalWithout(string text, long expected) =>
        Assert.Equal(expected, ExeBytes.ParseNumber(text));

    [Theory]
    [InlineData("9f8240")]
    [InlineData("0x")]
    [InlineData("-1")]
    [InlineData("")]
    public void RejectsOtherNumbers(string text) => Assert.Null(ExeBytes.ParseNumber(text));

    [Theory]
    [InlineData(0)]
    [InlineData(ExeBytes.MaxLength + 1)]
    public void RefusesALengthOutsideItsRange(int length)
    {
        var exe = MadeUpExe(new Section(".text", 0x1000, Code(0x20)));

        var (exitCode, output) = Run(exe, 0x1000, length);

        Assert.Equal(2, exitCode);
        Assert.Contains($"length must be 1 to {ExeBytes.MaxLength}", output, StringComparison.Ordinal);
    }
}
