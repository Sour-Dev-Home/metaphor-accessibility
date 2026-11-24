using System.Buffers.Binary;

namespace Metaphor.Core.Tests;

/// <summary>#625's probe line: one archetype learn call, and whether the member already had the class. Made-up slots.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LearnClassCallTests
{
    [Fact]
    public void AClassInAUsedSlotIsKnown()
    {
        var slots = Slots((3, Used: true, ClassId: 17));

        Assert.True(LearnClassCall.Known(slots, 17));
    }

    [Fact]
    public void AClassOnlyInAnUnusedSlotIsNew()
    {
        var slots = Slots((0, Used: false, ClassId: 17), (5, Used: true, ClassId: 4));

        Assert.False(LearnClassCall.Known(slots, 17));
    }

    [Fact]
    public void TheLastSlotIsScanned()
    {
        var slots = Slots((LearnClassCall.SlotCount - 1, Used: true, ClassId: 9));

        Assert.True(LearnClassCall.Known(slots, 9));
    }

    [Fact]
    public void AShortReadIsUnknown() =>
        Assert.Null(LearnClassCall.Known(new byte[LearnClassCall.SlotsLength - 1], 1));

    [Fact]
    public void TheLineHasTheMemberRecordClassAndWhetherItWasKnown()
    {
        Assert.Equal("learn-class member=2 record=0x7ff0 class=17 known=no", LearnClassCall.Line(2, 0x7ff0, 17, false));
        Assert.Equal("learn-class member=1 record=0x0 class=? known=?", LearnClassCall.Line(1, 0, null, null));
        Assert.Equal("learn-class member=3 record=0x10 class=5 known=yes", LearnClassCall.Line(3, 0x10, 5, true));
    }

    private static byte[] Slots(params (int Slot, bool Used, int ClassId)[] filled)
    {
        var slots = new byte[LearnClassCall.SlotsLength];
        foreach (var (slot, used, classId) in filled)
        {
            var at = slot * LearnClassCall.SlotSize;
            slots[at] = used ? (byte)1 : (byte)0;
            BinaryPrimitives.WriteInt32LittleEndian(slots.AsSpan(at + LearnClassCall.SlotClassOffset), classId);
        }
        return slots;
    }
}
