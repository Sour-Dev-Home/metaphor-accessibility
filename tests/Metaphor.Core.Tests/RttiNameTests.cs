using System.Buffers.Binary;
using System.Text;

namespace Metaphor.Core.Tests;

/// <summary>An object's class name from the run-time type information its vtable points at (from the development kits'
/// screen class tests, #213). Made-up memory and class names only.</summary>
[Trait("Category", "Unit")]
public sealed class RttiNameTests
{
    private const long ModuleBase = 0x140000000;
    private const long ModuleSize = 0x20000000;

    /// <summary>A sparse fake of process memory: 8-byte cells and NUL-terminated strings at addresses.</summary>
    private sealed class FakeMemory
    {
        private readonly Dictionary<long, byte> bytes = [];

        public void Pointer(long at, long value) => Write(at, BitConverter.GetBytes(value));

        public void Int32(long at, int value) => Write(at, BitConverter.GetBytes(value));

        public void Text(long at, string value) => Write(at, [.. Encoding.ASCII.GetBytes(value), 0]);

        public string? ClassNameOf(long instance) =>
            RttiName.Of(instance, ModuleBase, ModuleSize, ReadPointer, ReadInt32, ReadAnsi);

        private long? ReadPointer(long at) => Read(at, 8) is { } b ? BinaryPrimitives.ReadInt64LittleEndian(b) : null;

        private int? ReadInt32(long at) => Read(at, 4) is { } b ? BinaryPrimitives.ReadInt32LittleEndian(b) : null;

        private string? ReadAnsi(long at)
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

        private void Write(long at, byte[] value)
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

    /// <summary>Lays out an MSVC x64 type descriptor, complete object locator and vtable for a class in the module, and
    /// returns the vtable's address.</summary>
    private static long Class(FakeMemory memory, long vtableRva, long locatorRva, long descriptorRva, string decorated)
    {
        memory.Text(ModuleBase + descriptorRva + 0x10, decorated);
        memory.Int32(ModuleBase + locatorRva, 1);
        memory.Int32(ModuleBase + locatorRva + 0x0C, (int)descriptorRva);
        memory.Int32(ModuleBase + locatorRva + 0x14, (int)locatorRva);
        memory.Pointer(ModuleBase + vtableRva - 8, ModuleBase + locatorRva);
        return ModuleBase + vtableRva;
    }

    [Fact]
    public void AnObjectsClassIsNamedByTheTypeInformationItsVtablePointsAt()
    {
        var memory = new FakeMemory();
        memory.Pointer(0x5000, Class(memory, 0x21f10b0, 0x1a00000, 0x2b00000, ".?AVVictory@phase@btl@@"));

        Assert.Equal("Victory@phase@btl", memory.ClassNameOf(0x5000));
    }

    [Fact]
    public void AnObjectWithoutTypeInformationHasNoClassName()
    {
        var memory = new FakeMemory();
        var vtable = Class(memory, 0x21f10b0, 0x1a00000, 0x2b00000, ".?AVVictory@phase@btl@@");
        memory.Pointer(0x5000, vtable);
        memory.Int32(ModuleBase + 0x1a00000 + 0x14, 0x1234);
        memory.Pointer(0x6000, 0x7000);
        memory.Pointer(0x7000 - 8, ModuleBase + 0x1a00000);

        Assert.Null(memory.ClassNameOf(0x5000));
        Assert.Null(memory.ClassNameOf(0x6000));
        Assert.Null(memory.ClassNameOf(0x8000));
    }
}
