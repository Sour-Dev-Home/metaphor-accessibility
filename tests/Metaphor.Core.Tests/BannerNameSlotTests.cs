using Metaphor.Core.Battle;

namespace Metaphor.Core.Tests;

/// <summary>#739's probe: message variable slot 0 as the log line ends, from the bytes a Debug build read.</summary>
[Trait("Category", "Unit")]
public sealed class BannerNameSlotTests
{
    private static byte[] Slot(uint value, byte tag)
    {
        var slot = new byte[BannerNameSlot.Read];
        BitConverter.GetBytes(value).CopyTo(slot, 0);
        slot[BannerNameSlot.TagAt] = tag;
        return slot;
    }

    [Fact]
    public void ASlotSaysItsFirstU32InHexAndItsTag()
    {
        Assert.Equal(" slot0=0x97 tag=0", BannerNameSlot.Describe(Slot(0x97, 0)));
        Assert.Equal(" slot0=0x1a6c tag=2", BannerNameSlot.Describe(Slot(0x1a6c, 2)));
    }

    [Fact]
    public void ASlotNotReadInFullIsUnread()
    {
        Assert.Equal(" slot0=unread", BannerNameSlot.Describe([]));
        Assert.Equal(" slot0=unread", BannerNameSlot.Describe(new byte[BannerNameSlot.TagAt]));
    }

    private static byte[] FullSlot(byte tag)
    {
        var slot = new byte[BannerNameSlot.Length];
        slot[BannerNameSlot.TagAt] = tag;
        return slot;
    }

    private static byte[] StringSlot(ulong first, long size, long capacity)
    {
        var slot = FullSlot(3);
        BitConverter.GetBytes(first).CopyTo(slot, 0);
        BitConverter.GetBytes(size).CopyTo(slot, 0x10);
        BitConverter.GetBytes(capacity).CopyTo(slot, 0x18);
        return slot;
    }

    private static byte[]? NoRead(long address, int length) => throw new InvalidOperationException("no read expected");

    [Fact]
    public void ATag2SlotNamesItsSerial()
    {
        var slot = FullSlot(2);
        BitConverter.GetBytes(0xa7u).CopyTo(slot, 0);

        Assert.Equal(new BannerName(0xa7, null), BannerNameSlot.Name(slot, NoRead));
    }

    [Fact]
    public void AShortTag3StringIsReadFromTheSlotItself()
    {
        var slot = StringSlot(0, 4, 15);
        "Will"u8.CopyTo(slot);

        Assert.Equal(new BannerName(null, "Will"), BannerNameSlot.Name(slot, NoRead));
    }

    [Fact]
    public void ALongTag3StringIsReadThroughItsPointer()
    {
        var slot = StringSlot(0x1234_5678, 17, 31);
        long? askedAt = null;
        var name = BannerNameSlot.Name(slot, (address, length) =>
        {
            askedAt = address;
            return "Seventeen letters"u8.ToArray()[..length];
        });

        Assert.Equal(0x1234_5678, askedAt);
        Assert.Equal(new BannerName(null, "Seventeen letters"), name);
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(BannerNameSlot.LongestName + 1, 127)]
    [InlineData(20, 15)]
    public void ATag3StringOfAnImpossibleSizeIsNoName(long size, long capacity) =>
        Assert.Null(BannerNameSlot.Name(StringSlot(0, size, capacity), (_, _) => new byte[64]));

    [Fact]
    public void ATag3StringThatIsNotUtf8OrFailsToReadIsNoName()
    {
        var slot = StringSlot(0, 2, 15);
        slot[0] = 0xff;
        slot[1] = 0xfe;

        Assert.Null(BannerNameSlot.Name(slot, NoRead));
        Assert.Null(BannerNameSlot.Name(StringSlot(0x1000, 17, 31), (_, _) => null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void OtherTagsAndShortReadsAreNoName(byte tag)
    {
        Assert.Null(BannerNameSlot.Name(FullSlot(tag), NoRead));
        Assert.Null(BannerNameSlot.Name(new byte[BannerNameSlot.TagAt], NoRead));
    }
}
