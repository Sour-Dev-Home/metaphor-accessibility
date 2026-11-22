using Metaphor.Core.Messages;

namespace Metaphor.Features.Field.Tests;

/// <summary>The main mission table's rows as the game holds them (#658): 0x30 bytes each, the title label's MD5 at
/// +0x00, the empty label's MD5 at +0x10 and the flag at +0x20. Made-up labels and flags only.</summary>
[Trait("Category", "Unit")]
public sealed class MainMissionTableTests
{
    private static readonly byte[] EmptyHash = Convert.FromHexString(MessageLibrary.LabelHash(""));

    private static byte[] Row(string label, int flag, byte[]? second = null)
    {
        var row = new byte[MainMissionTable.RowSize];
        Convert.FromHexString(MessageLibrary.LabelHash(label)).CopyTo(row, 0);
        (second ?? EmptyHash).CopyTo(row, 0x10);
        BitConverter.GetBytes(flag).CopyTo(row, 0x20);
        BitConverter.GetBytes(0xffff).CopyTo(row, 0x24);
        return row;
    }

    [Fact]
    public void ReadsEachRowsFlagAndTitleHash()
    {
        byte[] bytes = [.. Row("MissionText_Test_01", 0x1166), .. Row("MissionText_Test_02", 0x116a)];

        Assert.Equal(
        [
            new MainMissionRow(0x1166, MessageLibrary.LabelHash("MissionText_Test_01")),
            new MainMissionRow(0x116a, MessageLibrary.LabelHash("MissionText_Test_02")),
        ], MainMissionTable.Rows(bytes));
    }

    [Fact]
    public void BytesThatArentMainRowsGiveNull()
    {
        Assert.Null(MainMissionTable.Rows(new byte[0x31]));
        Assert.Null(MainMissionTable.Rows(Row("MissionText_Test_01", 0x1166, second: new byte[16])));
        Assert.Equal([], MainMissionTable.Rows([]));
    }
}
