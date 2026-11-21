using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests.Messages;

/// <summary>A message reference (#545): a file's entry in the game's message-file table and a position in it, read
/// from the bytes a UI panel is handed, and found through the serials of the file with that entry
/// (docs/findings/travel-map-destination-text-static.md). File names follow the game's; the text is made up.</summary>
[Trait("Category", "Unit")]
public sealed class MessageReferenceTests
{
    private const string Places = "EN/message/Test_Places.msg";

    private const string PlacesSource = """
        @Test_Place_000
        {
        Harbour
        }

        @Test_Place_001
        {
        Old Mill
        }
        """;

    private static byte[] Bytes(int file, int position, byte fill = 0)
    {
        var bytes = Enumerable.Repeat(fill, MessageReference.Length).ToArray();
        BitConverter.GetBytes(file).CopyTo(bytes, 0);
        BitConverter.GetBytes(position).CopyTo(bytes, 4);
        return bytes;
    }

    private static MessageSerials Serials()
    {
        var serials = new MessageSerials([
            new SerialRange("message/system/Test_Menu.msg", 500, 3),
            new SerialRange("message/Test_Places.msg", 700, 2, Entry: 12),
        ]);
        serials.Observe(new MessageFileCompiled(Places, -1, PlacesSource));
        return serials;
    }

    [Fact]
    public void AReferenceIsItsFirstTwoNumbersWhateverFollows()
    {
        Assert.Equal(new MessageReference(34, 7), MessageReference.Read(Bytes(34, 7, fill: 0xAB)));
    }

    [Fact]
    public void AShortOrUnsetReferenceReadsAsNone()
    {
        Assert.Null(MessageReference.Read(Bytes(34, 7).AsSpan(0, MessageReference.Length - 1)));
        Assert.Null(MessageReference.Read(Bytes(-1, -1)));
        Assert.Null(MessageReference.Read(Bytes(34, -1)));
    }

    [Fact]
    public void AReferenceFindsTheMessageAtItsPositionInTheFileWithItsEntry()
    {
        var serials = Serials();

        Assert.Equal("Test_Place_001", serials.Find(new MessageReference(12, 1))?.Label);
        Assert.Equal("Test_Place_000", serials.Find(new MessageReference(12, 0))?.Label);
    }

    [Fact]
    public void AReferenceToAnotherEntryOrPastTheFileFindsNothing()
    {
        var serials = Serials();

        Assert.Null(serials.Find(new MessageReference(13, 0)));
        Assert.Null(serials.Find(new MessageReference(12, 2)));
    }

    // As the message-file table at 0x27b0cd0 holds them on build 548ddc95: entry, then the range test's first serial
    // and count.
    [Fact]
    public void TheBuildsPlaceNameTableHoldsTheThreeFilesWithTheirEntries()
    {
        Assert.Equal(
            [
                new SerialRange("message/AccessPointName.msg", 10873, 408, 34),
                new SerialRange("message/system/Big_Mapname.msg", 10102, 23, 40),
                new SerialRange("message/system/WorldMap_Message.msg", 10860, 13, 66),
            ],
            MessageSerials.Steam18330018PlaceNames);
    }
}
