using System.Buffers.Binary;
using System.Text;

namespace Metaphor.Core.Tests;

/// <summary>The target cursor's vtable swap (#253, ADR 0007): one slot written only when the vtable's class and the
/// slot's pointer are the ones expected, read back, and put back on failure and on restore. Made-up memory only.
/// </summary>
[Trait("Category", "Unit")]
public sealed class VtableSwapTests
{
    private const long ModuleBase = 0x140000000;
    private const long ModuleSize = 0x20000000;
    private const string CursorClass = "UUIC_Battle_TargetCursor@ui@app";
    private const long Original = ModuleBase + 0xf26ec0;
    private const long Replacement = 0x7ff0_1234_5000;
    private const int Slot = 4;

    private readonly FakeMemory memory = new();
    private readonly long vtable;

    public VtableSwapTests()
    {
        vtable = ModuleBase + 0x2250280;
        memory.Text(ModuleBase + 0x2b00000 + 0x10, ".?AV" + CursorClass + "@@");
        memory.Int32(ModuleBase + 0x248cfa0, 1);
        memory.Int32(ModuleBase + 0x248cfa0 + 0x0C, 0x2b00000);
        memory.Int32(ModuleBase + 0x248cfa0 + 0x14, 0x248cfa0);
        memory.Pointer(vtable - 8, ModuleBase + 0x248cfa0);
        memory.Pointer(vtable + 8 * Slot, Original);
    }

    private VtableSwap Swap(string expectedClass = CursorClass) =>
        new(expectedClass, ModuleBase, ModuleSize, memory.ReadPointer, memory.ReadInt32, memory.ReadAnsi, memory.Write);

    private long SlotNow() => memory.ReadPointer(vtable + 8 * Slot)!.Value;

    [Fact]
    public void TheSlotTakesTheReplacementWhenClassAndPointerMatch()
    {
        var swap = Swap();

        Assert.Equal(VtableSwapResult.Swapped, swap.Swap(vtable, Slot, Original, Replacement));

        Assert.Equal(Replacement, SlotNow());
        Assert.True(swap.IsSwapped);
        Assert.Equal(1, memory.Writes);
    }

    [Fact]
    public void RestorePutsTheOriginalPointerBack()
    {
        var swap = Swap();
        swap.Swap(vtable, Slot, Original, Replacement);

        Assert.True(swap.Restore());

        Assert.Equal(Original, SlotNow());
        Assert.False(swap.IsSwapped);
    }

    [Fact]
    public void ASlotHoldingAnotherPointerIsNeverWritten()
    {
        var swap = Swap();

        Assert.Equal(VtableSwapResult.NotMatched, swap.Swap(vtable, Slot, Original + 0x10, Replacement));

        Assert.Equal(Original, SlotNow());
        Assert.Equal(0, memory.Writes);
    }

    [Fact]
    public void AVtableOfAnotherClassIsNeverWritten()
    {
        var swap = Swap("UUIC_Field_TargetCursor@ui@app");

        Assert.Equal(VtableSwapResult.NotMatched, swap.Swap(vtable, Slot, Original, Replacement));

        Assert.Equal(0, memory.Writes);
    }

    [Fact]
    public void AWriteThatDoesNotReadBackPutsTheOriginalBack()
    {
        memory.Corrupts = true;
        var swap = Swap();

        Assert.Equal(VtableSwapResult.Failed, swap.Swap(vtable, Slot, Original, Replacement));

        Assert.Equal(Original, SlotNow());
        Assert.False(swap.IsSwapped);
    }

    [Fact]
    public void RestoreLeavesASlotSomeoneElseSwappedSince()
    {
        var swap = Swap();
        swap.Swap(vtable, Slot, Original, Replacement);
        memory.Pointer(vtable + 8 * Slot, 0x7ff0_9999_0000);

        Assert.False(swap.Restore());

        Assert.Equal(0x7ff0_9999_0000, SlotNow());
    }

    /// <summary>A sparse fake of process memory that counts writes; <see cref="Corrupts"/> makes the next write land
    /// as garbage and report failure, as a torn or failed write might.</summary>
    private sealed class FakeMemory
    {
        private readonly Dictionary<long, byte> bytes = [];

        public int Writes { get; private set; }

        public bool Corrupts { get; set; }

        public void Pointer(long at, long value) => Put(at, BitConverter.GetBytes(value));

        public void Int32(long at, int value) => Put(at, BitConverter.GetBytes(value));

        public void Text(long at, string value) => Put(at, [.. Encoding.ASCII.GetBytes(value), 0]);

        public bool Write(long at, long value)
        {
            Writes++;
            var corrupt = Corrupts;
            Corrupts = false;
            Pointer(at, corrupt ? value ^ 0xFF : value);
            return !corrupt;
        }

        public long? ReadPointer(long at) => Read(at, 8) is { } b ? BinaryPrimitives.ReadInt64LittleEndian(b) : null;

        public int? ReadInt32(long at) => Read(at, 4) is { } b ? BinaryPrimitives.ReadInt32LittleEndian(b) : null;

        public string? ReadAnsi(long at)
        {
            var text = new StringBuilder();
            for (var a = at; bytes.TryGetValue(a, out var b); a++)
            {
                if (b == 0)
                {
                    return text.ToString();
                }
                text.Append((char)b);
            }
            return null;
        }

        private void Put(long at, byte[] value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                bytes[at + i] = value[i];
            }
        }

        private byte[]? Read(long at, int length)
        {
            var value = new byte[length];
            for (var i = 0; i < length; i++)
            {
                if (!bytes.TryGetValue(at + i, out value[i]))
                {
                    return null;
                }
            }
            return value;
        }
    }
}
