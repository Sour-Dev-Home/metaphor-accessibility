using System.Buffers.Binary;
using System.Text;

namespace Metaphor.Core.Tests;

/// <summary>What the #93 UI focus listeners decode: cursor inputs, text pane draw states, change gating and the text
/// probe. Made-up objects and text only.</summary>
[Trait("Category", "Unit")]
public sealed class UiFocusSpyTests
{
    private const long ModuleBase = 0x140000000;
    private const long ModuleSize = 0x20000000;

    private static byte[] Pane(float x = 10f, float y = 20f, long colour = 0x11223344, byte colourSet = 1, int timer = 0)
    {
        var bytes = new byte[PaneDrawn.Length];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, ModuleBase + 0x2205688);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x08), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x0C), 0x100);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x20), timer);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(0x30), x);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(0x34), y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(0x38), 0.5f);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x3C), 7);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(0x48), colour);
        bytes[0x50] = colourSet;
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(0x58), 0x5000);
        return bytes;
    }

    [Fact]
    public void APaneIsReadFromItsFields()
    {
        var pane = PaneDrawn.Read(0x40, ModuleBase, ModuleSize, Pane());

        Assert.Equal(new PaneDrawn(0x40, 0x2205688, 2, 0x100, 10f, 20f, 0.5f, 7, 0x11223344, true, 0x5000), pane);
    }

    [Fact]
    public void APaneWithTooFewBytesOrAVtableOutsideTheModuleIsReadAsSuch()
    {
        Assert.Null(PaneDrawn.Read(0x40, ModuleBase, ModuleSize, new byte[PaneDrawn.Length - 1]));
        Assert.Null(PaneDrawn.Read(0x40, ModuleBase, ModuleSize, new byte[PaneDrawn.Length])!.ClassRva);
    }

    [Fact]
    public void APanesStateKeyChangesWithItsColourButNotWithOtherFields()
    {
        Assert.Equal(PaneDrawn.StateKey(Pane()), PaneDrawn.StateKey(Pane(timer: 99)));
        Assert.NotEqual(PaneDrawn.StateKey(Pane()), PaneDrawn.StateKey(Pane(colour: 0x55667788)));
        Assert.NotEqual(PaneDrawn.StateKey(Pane()), PaneDrawn.StateKey(Pane(colourSet: 0)));
        Assert.NotEqual(PaneDrawn.StateKey(Pane()), PaneDrawn.StateKey(Pane(y: 21f)));
        Assert.Empty(PaneDrawn.StateKey(new byte[3]));
    }

    [Fact]
    public void TheGateLetsThroughOnlyChangesPerKey()
    {
        var gate = new ChangeGate();

        Assert.True(gate.Changed(1, [1, 2]));
        Assert.False(gate.Changed(1, [1, 2]));
        Assert.True(gate.Changed(2, [1, 2]));
        Assert.True(gate.Changed(1, [1, 3]));
        Assert.True(gate.Changed(1, [1, 2]));
    }

    [Fact]
    public void TheGateForgetsEverythingPastItsLimit()
    {
        var gate = new ChangeGate();
        for (var key = 0; key < ChangeGate.MaxKeys; key++)
        {
            gate.Changed(key, [0]);
        }

        gate.Changed(-1, [0]);

        Assert.True(gate.Changed(0, [0]));
    }

    [Fact]
    public void TheProbeFindsTheFirstFieldPointingAtText()
    {
        var fields = new byte[32];
        BinaryPrimitives.WriteInt64LittleEndian(fields, 5); // too low to be a pointer
        BinaryPrimitives.WriteInt64LittleEndian(fields.AsSpan(8), 0x20000); // points at no text
        BinaryPrimitives.WriteInt64LittleEndian(fields.AsSpan(16), 0x30000); // points at digits only
        BinaryPrimitives.WriteInt64LittleEndian(fields.AsSpan(24), 0x40000);
        var memory = new Dictionary<long, string>
        {
            [0x30000] = "123",
            [0x40000] = Encoding.Latin1.GetString(Encoding.UTF8.GetBytes("Nouvelle partie é")),
        };

        var found = TextProbe.FirstString(fields, address => memory.GetValueOrDefault(address));

        Assert.Equal((24, "Nouvelle partie é"), found);
    }

    [Fact]
    public void TheProbeRejectsControlCharactersAndSingleLetters()
    {
        var fields = new byte[16];
        BinaryPrimitives.WriteInt64LittleEndian(fields, 0x20000);
        BinaryPrimitives.WriteInt64LittleEndian(fields.AsSpan(8), 0x30000);
        var memory = new Dictionary<long, string> { [0x20000] = "a\u0001b", [0x30000] = "x" };

        Assert.Null(TextProbe.FirstString(fields, address => memory.GetValueOrDefault(address)));
    }
}
