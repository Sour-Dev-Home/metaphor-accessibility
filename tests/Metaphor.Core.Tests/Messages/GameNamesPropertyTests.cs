using System.Buffers.Binary;
using System.Text;
using FsCheck;
using FsCheck.Fluent;
using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests.Messages;

/// <summary>Properties of what <see cref="GameNames"/> parses from game memory (ADR 0015): any bytes give a name or
/// none without throwing, a name written as the game writes it reads back, and the slot the game would pick is the one
/// chosen.</summary>
[Trait("Category", "Unit")]
[Trait("Category", "Property")]
public sealed class GameNamesPropertyTests
{
    // Units that are often zero, often letters, sometimes surrogates or out of range.
    private static readonly Gen<uint> Units = Gen.Frequency(
        (3, Gen.Constant(0u)),
        (6, Gen.Choose('A', 'z').Select(c => (uint)c)),
        (1, Gen.Choose(0xD800, 0xDFFF).Select(c => (uint)c)),
        (1, Gen.Elements(0x10FFFFu, 0x110000u, 0x80000000u, 0xFFFFFFFFu)),
        (1, Gen.Choose(0, int.MaxValue).Select(c => (uint)c)));

    private static readonly Arbitrary<byte[]> Fields = Gen.Frequency(
        (5, Gen.ArrayOf(Units, 17).Select(Pack)),
        (1, Gen.ArrayOf(Gen.Choose(0, 255).Select(b => (byte)b))))
        .ToArbitrary();

    // Names of 1 to 16 letters, some outside the basic plane, as a typed name could be.
    private static readonly Arbitrary<string> Names = Gen.Choose(1, 16)
        .SelectMany(n => Gen.ArrayOf(Gen.Frequency(
            (8, Gen.Choose('a', 'z').Select(c => ((char)c).ToString())),
            (1, Gen.Choose(0x1F600, 0x1F64F).Select(char.ConvertFromUtf32))), n))
        .Select(string.Concat)
        .ToArbitrary();

    // An entry's five slot flags, each none, set or clear.
    private static readonly Arbitrary<int[]> SlotStates = Gen.ArrayOf(Gen.Choose(0, 2), 5).ToArbitrary();

    [Fact]
    public void AnyFieldGivesABoundedNameOrNone() =>
        PropertyRun.Check(Prop.ForAll(Fields, field =>
        {
            var name = TypedNameText.Decode(field);
            Assert.True(name is null || (name.Length > 0 && name.Length <= field.Length / 2));
        }));

    [Fact]
    public void ANameWrittenAsTheGameWritesItReadsBack() =>
        PropertyRun.Check(Prop.ForAll(Names, name =>
        {
            var field = new byte[TypedNameText.Size];
            Encoding.UTF32.GetBytes(name).CopyTo(field, 0);
            Assert.Equal(name, TypedNameText.Decode(field));
        }));

    [Fact]
    public void AnyEntryBytesGiveAKeyOrNoneWithoutThrowing() =>
        PropertyRun.Check(Prop.ForAll(Fields, bytes =>
        {
            var key = CharacterEntry.ChooseKey(bytes, flag => flag % 3 == 0 ? null : flag % 2 == 0);
            Assert.True(key is null || key.Length == 16);
        }));

    [Fact]
    public void TheChosenKeyIsTheFirstSlotWhoseFlagIsSet() =>
        PropertyRun.Check(Prop.ForAll(SlotStates, states =>
        {
            // Slot i's key is sixteen bytes of i + 1, the default's sixteen zeros; slot i's flag is i, or none.
            var entry = new byte[CharacterEntry.Size];
            for (var i = 0; i < 5; i++)
            {
                var at = 0x10 + i * 0x12;
                entry.AsSpan(at, 16).Fill((byte)(i + 1));
                BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(at + 16), states[i] == 0 ? (ushort)0xffff : (ushort)i);
            }
            var first = Array.FindIndex(states, state => state == 2);

            var key = CharacterEntry.ChooseKey(entry, flag => states[flag] == 2);

            Assert.Equal(Enumerable.Repeat((byte)(first + 1), 16), key!);
        }));

    private static byte[] Pack(uint[] units)
    {
        var bytes = new byte[units.Length * 4];
        for (var i = 0; i < units.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), units[i]);
        }
        return bytes;
    }
}
