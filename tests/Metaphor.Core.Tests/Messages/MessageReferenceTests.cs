using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests.Messages;

/// <summary>A message reference (#545): the id a file was compiled under and a position in it, read from the bytes a UI
/// panel is handed, as the travel map's references read in play on 2026-11-21
/// (docs/findings/travel-map-destination-text-static.md). File names follow the game's; the text is made up.</summary>
[Trait("Category", "Unit")]
public sealed class MessageReferenceTests
{
    private static byte[] Bytes(int file, int position, byte fill = 0)
    {
        var bytes = Enumerable.Repeat(fill, MessageReference.Length).ToArray();
        BitConverter.GetBytes(file).CopyTo(bytes, 0);
        BitConverter.GetBytes(position).CopyTo(bytes, 4);
        return bytes;
    }

    private static string Source(string label, int count) =>
        string.Join("\n\n", Enumerable.Range(0, count).Select(i => $"@{label}_{i:00}\n{{\n{label} {i}\n}}"));

    [Fact]
    public void AReferenceIsItsFirstTwoNumbersWhateverFollows()
    {
        // The shape logged on the travel map: id 4, position 0x17e, then bytes the mod doesn't read.
        Assert.Equal(new MessageReference(4, 382),
            MessageReference.Read(Convert.FromHexString("040000007e010000ffffffff00000000ffffffff0000000000000000")));
        Assert.Equal(new MessageReference(124, 15), MessageReference.Read(Bytes(124, 15, fill: 0xAB)));
    }

    [Fact]
    public void AShortOrUnsetReferenceReadsAsNone()
    {
        Assert.Null(MessageReference.Read(Bytes(4, 7).AsSpan(0, MessageReference.Length - 1)));
        Assert.Null(MessageReference.Read(Bytes(-1, -1)));
        Assert.Null(MessageReference.Read(Bytes(4, -1)));
    }

    [Fact]
    public void AReferenceFindsTheMessageAtItsPositionInTheFileCompiledUnderItsId()
    {
        var files = new MessageFilesById();
        files.Observe(new MessageFileCompiled("EN/message/Test_Names.msg", 4, Source("Test_Name", 400)));
        files.Observe(new MessageFileCompiled("EN/message/system/Test_Region.msg", 124, Source("Test_Spot", 16)));

        Assert.Equal("Test_Name_382", files.Find(new MessageReference(4, 382))?.Label);
        Assert.Equal("Test_Spot_15", files.Find(new MessageReference(124, 15))?.Label);
    }

    [Fact]
    public void AnUnknownIdAPositionPastTheFileOrAFileWithoutAnIdFindsNothing()
    {
        var files = new MessageFilesById();
        files.Observe(new MessageFileCompiled("EN/message/Test_Names.msg", 4, Source("Test_Name", 3)));
        files.Observe(new MessageFileCompiled("EN/message/event/Test_Event.msg", -1, Source("Test_Event", 3)));

        Assert.Null(files.Find(new MessageReference(5, 0)));
        Assert.Null(files.Find(new MessageReference(4, 3)));
        Assert.Null(files.Find(new MessageReference(-1, 0)));
    }

    [Fact]
    public void AFileCompiledUnderAnIdReplacesWhatTheIdHeld()
    {
        var files = new MessageFilesById();
        files.Observe(new MessageFileCompiled("EN/message/system/Test_Region.msg", 124, Source("Test_Spot", 2)));
        files.Observe(new MessageFileCompiled("EN/message/system/Test_Other.msg", 124, Source("Test_Other", 2)));
        files.Observe(new MessageFileCompiled("EN/message/system/Test_Region.msg", 135, Source("Test_Spot", 2)));

        Assert.Equal("Test_Other_01", files.Find(new MessageReference(124, 1))?.Label);
        Assert.Equal("Test_Spot_01", files.Find(new MessageReference(135, 1))?.Label);
    }

    [Fact]
    public void AFileNameFindsTheMessageAtAPositionInItsLatestCompile()
    {
        // The battle banners name their file by its table entry, not its compile id (#692).
        var files = new MessageFilesById();
        files.Observe(new MessageFileCompiled("EN/message/system/Test_Help.msg", 10, Source("Test_Old", 3)));
        files.Observe(new MessageFileCompiled("EN/message/system/Test_Help.msg", 40, Source("Test_Help", 3)));
        files.Observe(new MessageFileCompiled("EN/message/event/Test_Event.msg", -1, Source("Test_Event", 3)));

        Assert.Equal("Test_Help_02", files.FindInFile("Test_Help.msg", 2)?.Label);
        Assert.Equal("Test_Help_00", files.FindInFile("test_help.MSG", 0)?.Label);
        Assert.Null(files.FindInFile("Test_Help.msg", 3));
        Assert.Null(files.FindInFile("Help.msg", 0));
        Assert.Null(files.FindInFile("Test_Event.msg", 0));
    }
}
