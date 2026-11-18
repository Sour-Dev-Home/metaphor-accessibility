using System.Buffers.Binary;
using Metaphor.Core.Menus;

namespace Metaphor.Core.Tests.Menus;

/// <summary>The #249 probe's read of a cursor manager's list. Made-up managers only.</summary>
[Trait("Category", "Unit")]
public sealed class CursorListCountTests
{
    private static byte[] Manager(int count, long begin, long end)
    {
        var bytes = new byte[CursorListCount.ManagerBytes];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x58), count);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(0x88), begin);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(0x90), end);
        return bytes;
    }

    [Fact]
    public void ReadsTheCountAndTheVectorLengthInSixteenByteItems() =>
        Assert.Equal((7, 7L), CursorListCount.From(Manager(7, 0x1000, 0x1000 + (7 * 16))));

    [Fact]
    public void AnEmptyVectorHasNoItems() => Assert.Equal((0, 0L), CursorListCount.From(Manager(0, 0, 0)));

    [Fact]
    public void ABackwardVectorOrAShortReadGivesNothing()
    {
        Assert.Null(CursorListCount.From(Manager(3, 0x2000, 0x1000)));
        Assert.Null(CursorListCount.From(new byte[CursorListCount.ManagerBytes - 1]));
    }

    [Fact]
    public void LogsNumbersOnly() => Assert.Equal(
        "mgr=0xabc count=5 items=5 flag8=1 flag9=?",
        new CursorListCount(0xabc, 5, 5, 1, null).ToString());
}
