using System.Buffers.Binary;

namespace Metaphor.Features.Field;

/// <summary>The main mission table's rows as the game holds them (#658), on game build 548ddc95… (Steam build 18330018):
/// <c>0x30</c> bytes each, built from <c>Mission_Main_Data.bin</c> by <c>0x3c6600</c>. A row holds a label's MD5 at
/// <c>+0x00</c>, the empty label's MD5 at <c>+0x10</c> and the flag that starts the mission at <c>+0x20</c>
/// (docs/findings/mission-title-from-the-mission-table.md).</summary>
public static class MainMissionTable
{
    /// <summary>The size of one row.</summary>
    public const int RowSize = 0x30;

    /// <summary>The most rows a read takes, well above the table's size.</summary>
    public const int MaxRows = 0x400;

    private const int SecondHash = 0x10;
    private const int Flag = 0x20;
    private const int HashSize = 16;

    // MD5 of no bytes, which the build copies into every main row's +0x10 from 0x17e7168.
    private static readonly byte[] EmptyHash = Convert.FromHexString("D41D8CD98F00B204E9800998ECF8427E");

    /// <summary>The rows in <paramref name="bytes"/>; null when they aren't whole main rows, so a table read at the
    /// wrong place is never taken for one.</summary>
    public static IReadOnlyList<MainMissionRow>? Rows(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length % RowSize != 0 || bytes.Length / RowSize > MaxRows)
        {
            return null;
        }
        var rows = new List<MainMissionRow>(bytes.Length / RowSize);
        for (var at = 0; at < bytes.Length; at += RowSize)
        {
            var row = bytes.Slice(at, RowSize);
            if (!row.Slice(SecondHash, HashSize).SequenceEqual(EmptyHash))
            {
                return null;
            }
            rows.Add(new(BinaryPrimitives.ReadInt32LittleEndian(row[Flag..]), Convert.ToHexString(row[..HashSize])));
        }
        return rows;
    }
}
