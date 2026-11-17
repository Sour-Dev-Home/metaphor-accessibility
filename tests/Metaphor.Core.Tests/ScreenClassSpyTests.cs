using System.Buffers.Binary;
using System.Text;

namespace Metaphor.Core.Tests;

/// <summary>What the #213 screen class listener decodes: an object's class name from the run-time type information its
/// vtable points at, which choice owns a cursor manager, and the manager's fields that point at named objects. Made-up
/// memory and class names only.</summary>
[Trait("Category", "Unit")]
public sealed class ScreenClassSpyTests
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

        public string? ClassNameOf(long instance) =>
            RttiName.Of(instance, ModuleBase, ModuleSize, ReadPointer, ReadInt32, ReadAnsi);

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
        memory.Pointer(0x5000, Class(memory, 0x21f10b0, 0x1a00000, 0x2b00000, ".?AVTitleMenuChoice@ope@ui@app@@"));

        Assert.Equal("TitleMenuChoice@ope@ui@app", memory.ClassNameOf(0x5000));
    }

    [Fact]
    public void AnObjectWithoutTypeInformationHasNoClassName()
    {
        var memory = new FakeMemory();
        var vtable = Class(memory, 0x21f10b0, 0x1a00000, 0x2b00000, ".?AVTitleMenuChoice@ope@ui@app@@");
        memory.Pointer(0x5000, vtable);
        memory.Int32(ModuleBase + 0x1a00000 + 0x14, 0x1234);
        memory.Pointer(0x6000, 0x7000);
        memory.Pointer(0x7000 - 8, ModuleBase + 0x1a00000);

        Assert.Null(memory.ClassNameOf(0x5000));
        Assert.Null(memory.ClassNameOf(0x6000));
        Assert.Null(memory.ClassNameOf(0x8000));
    }

    [Fact]
    public void AManagerIsOwnedByTheLastChoiceThatHandedItOut()
    {
        var owners = new CursorOwners();

        owners.Saw(owner: 0x100, manager: 0xa0);
        owners.Saw(owner: 0x200, manager: 0xb0);
        owners.Saw(owner: 0x300, manager: 0xa0);

        Assert.Equal(0x300, owners.OwnerOf(0xa0));
        Assert.Equal(0x200, owners.OwnerOf(0xb0));
        Assert.Null(owners.OwnerOf(0xc0));
    }

    [Fact]
    public void AManagersFieldsThatPointAtNamedObjectsAreListedByOffset()
    {
        var memory = new FakeMemory();
        memory.Pointer(0x19000, Class(memory, 0x21e2830, 0x1a00100, 0x2b00100, ".?AVRootMenuChoice@ope@ui@app@@"));
        memory.Pointer(0x19100, Class(memory, 0x2204568, 0x1a00200, 0x2b00200, ".?AVCursorManagerSoundParts@ope@ui@app@@"));
        var fields = new byte[0x30];
        BinaryPrimitives.WriteInt64LittleEndian(fields.AsSpan(0x08), 0x19100);
        BinaryPrimitives.WriteInt64LittleEndian(fields.AsSpan(0x10), 0x17777);
        BinaryPrimitives.WriteInt32LittleEndian(fields.AsSpan(0x18), 3);
        BinaryPrimitives.WriteInt64LittleEndian(fields.AsSpan(0x28), 0x19000);

        var named = NamedFields.Of(fields, memory.ClassNameOf);

        Assert.Equal(
            [(0x08, "CursorManagerSoundParts@ope@ui@app"), (0x28, "RootMenuChoice@ope@ui@app")],
            named);
    }
}
