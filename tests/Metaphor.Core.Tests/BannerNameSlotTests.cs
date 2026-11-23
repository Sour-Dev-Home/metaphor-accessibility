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
}
