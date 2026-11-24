using System.Buffers.Binary;

namespace Metaphor.Core.Tests;

/// <summary>The capture build's log lines (#693, #949, #950, #702, #930, #809, #253, #199): numbers only, one line per
/// probe, so a session's log can be searched by the probe's name. Made-up values.</summary>
[Trait("Category", "Unit")]
public sealed class CaptureLinesTests
{
    [Fact]
    public void AZoneLineHasItsOriginForwardAxisRadiusAngleAndAiState()
    {
        var zone = new byte[CaptureLines.ZoneLength];
        Floats(zone, 0x00, 1, 2, 3);
        Floats(zone, 0x10, 0, 0, 1);
        Floats(zone, 0x20, 0, 1, 0);
        Floats(zone, 0x30, 450, 0.5f);

        var line = CaptureLines.Zone(0x1234, 3.456, "aiState=8 aiNext=? aiPending=? hp=42", zone);

        Assert.Equal(
            "zone enemy=0x1234 dist=3.46 origin=1.00,2.00,3.00 forward=0.00,0.00,1.00 axis=0.00,1.00,0.00 " +
            "radius=450.00 angle=0.5000 aiState=8 aiNext=? aiPending=? hp=42",
            line);
    }

    [Fact]
    public void AShortZoneReadSaysTheZoneIsUnknown() =>
        Assert.Equal("zone enemy=0x10 dist=1.00 zone=? ai", CaptureLines.Zone(0x10, 1, "ai", new byte[4]));

    [Fact]
    public void APickupLineHasItsRowLinkBitAndTakenState() =>
        Assert.Equal(
            "pickup unit=0xabc at=120,-40 row=7 link=-1 bit=300 taken=1",
            CaptureLines.Pickup(0xabc, 120.4f, -40.2f, 7, -1, 300, true));

    [Fact]
    public void APickupLineMarksWhatItCouldNotRead() =>
        Assert.Equal(
            "pickup unit=0x1 at=0,0 row=? link=5 bit=? taken=?",
            CaptureLines.Pickup(1, 0, 0, null, 5, null, null));

    [Theory]
    [InlineData(0b0000_1000, 3, true)]
    [InlineData(0b0000_1000, 11, true)]
    [InlineData(0b0000_1000, 2, false)]
    [InlineData(0b1000_0000, 7, true)]
    public void ATakenBitIsBitNModEightOfItsByte(int value, int bit, bool expected) =>
        Assert.Equal(expected, CaptureLines.BitSet((byte)value, bit));

    [Fact]
    public void AChestSeenFirstLogsItsWholeBlock()
    {
        var block = new byte[] { 0x00, 0x01, 0xff };

        Assert.Equal("chest unit=0x50 layout=12 block=0001ff", CaptureLines.Chest(0x50, 12, [], block));
    }

    [Fact]
    public void AChestSeenAgainLogsOnlyTheBytesThatChanged()
    {
        var before = new byte[] { 0x00, 0x01, 0xff, 0x10 };
        var after = new byte[] { 0x00, 0x02, 0xff, 0x00 };

        Assert.Equal(
            "chest unit=0x50 layout=? changed=+0x2881:01>02,+0x2883:10>00",
            CaptureLines.Chest(0x50, null, before, after));
    }

    [Fact]
    public void ALockLineHasTheLockWordsFreeRoamAndMap()
    {
        Assert.Equal(
            "locks lock34CC=8 action34C8=0 freeRoam=0 map=371",
            CaptureLines.Locks("lock34CC=8 action34C8=0", freeRoam: false, map: 371));
        Assert.Equal("locks lock34CC=? freeRoam=1 map=?", CaptureLines.Locks(null, freeRoam: true, map: null));
    }

    [Fact]
    public void APressLineNamesTheWatchedButtonsThatWentDown()
    {
        // Start 0x0008, RT 0x0200, RS 0x0004 (BindingNames); A 0x4000 isn't watched.
        Assert.Equal("press pad0 Start,RT", CaptureLines.Presses(0, 0x0004, 0x0004 | 0x0008 | 0x0200 | 0x4000));
        Assert.Equal("press pad1 RS", CaptureLines.Presses(1, 0, 0x0004));
    }

    [Fact]
    public void NoPressLineWhenNoWatchedButtonWentDown()
    {
        Assert.Null(CaptureLines.Presses(0, 0x0008, 0x0008));
        Assert.Null(CaptureLines.Presses(0, 0, 0x4000));
    }

    [Fact]
    public void ANameEntryEventLineHasTheKeyboardAndItsBits()
    {
        Assert.Equal("name-entry-events keyboard=0x9a0 bits=0x00000028",
            CaptureLines.NameEntryEvents(0x9a0, 0x28));
        Assert.Equal("name-entry-events keyboard=0x9a0 bits=?", CaptureLines.NameEntryEvents(0x9a0, null));
    }

    [Fact]
    public void EnterIsHeldOnlyWhenItsCodeIsInTheKeyList()
    {
        Assert.Equal("enter=held", CaptureLines.Enter([0x41, 0x0D]));
        Assert.Equal("enter=-", CaptureLines.Enter([0x41]));
    }

    [Fact]
    public void APaneOwnerLineHasTheElementAndItsClass()
    {
        Assert.Equal("pane-owner elem=0x6bcd51d0 class=TextElement@ui@app",
            CaptureLines.PaneOwner(0x6bcd51d0, "TextElement@ui@app"));
        Assert.Equal("pane-owner elem=0x1 class=?", CaptureLines.PaneOwner(1, null));
    }

    [Fact]
    public void AFollowerRankLineListsEachFollowersRank()
    {
        var ranks = new byte[CaptureLines.FollowerRanksLength];
        BinaryPrimitives.WriteInt32LittleEndian(ranks.AsSpan(0), 1);
        BinaryPrimitives.WriteInt32LittleEndian(ranks.AsSpan(22 * 4), 8);

        Assert.Equal("follower-ranks 1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,8", CaptureLines.Ranks(ranks));
    }

    [Fact]
    public void TheMessageLogWritesACaptureLineWithItsTime()
    {
        var text = new StringWriter();

        new MessageLog(text, () => 900).Capture("press pad0 Start");

        Assert.Equal($"900 ms capture press pad0 Start{Environment.NewLine}", text.ToString());
    }

    private static void Floats(byte[] into, int at, params float[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(into.AsSpan(at + i * 4), values[i]);
        }
    }
}
