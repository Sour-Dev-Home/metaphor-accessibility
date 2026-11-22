using Metaphor.Core;
using Metaphor.Features.Field;
using static System.FormattableString;

namespace Metaphor.Hooks;

/// <summary>Reads the main mission table (#658): the pointer <c>M</c> at the global the <c>.ecode</c> row search
/// <c>MissionTableRow</c> (<c>0x3c5c80</c>) loads with <c>mov rdi,[rip+disp32]</c> (7 bytes) at <c>+0x19</c>. <c>M+0x60</c>
/// is a <c>fw::StaticArray</c> of four row vectors, its vtable at <c>0x17e7380</c> and its count at <c>+0x68</c>; its
/// slot 2 returns <c>this+0x10</c>, so the first vector, the main missions, is <c>{begin, end}</c> at <c>M+0x70</c>
/// (docs/findings/mission-title-from-the-mission-table.md). Game build 548ddc95… (Steam build 18330018). Only read,
/// through <see cref="ModuleMemoryReader"/>, and only when the vtable and count match; nothing is hooked, called or
/// written. [NEEDS VERIFICATION] in memory.</summary>
internal sealed class MissionTableRead : IReadsGlobal
{
    private const int StaticArray = 0x60;
    private const int Count = 0x68;
    private const int MainBegin = 0x70;
    private const int MainEnd = 0x78;
    private const int Categories = 4;
    private const long ArrayVtable = 0x17e7380;

    private static readonly GlobalSpec Spec = new(
        "mission table", [GameBuilds.Steam18330018], "MissionTableRow",
        LoadOffset: 0x19, DisplacementOffset: 3, InstructionLength: 7, TurnsOff: "the mission line");

    private readonly Action<string>? log;
    private readonly Lock gate = new();
    private string? lastState;

    // Set once by the loader before any hook goes live; pointer-sized fields are read and written whole.
    private nint global;
    private nint vtable;

    /// <summary>A reader that writes each change in what a read found, as numbers, to <paramref name="log"/> when there
    /// is one (Debug builds), for the next session to check the layout in memory.</summary>
    public MissionTableRead(Action<string>? log = null) => this.log = log;

    public GlobalSpec Global => Spec;

    public void Locate(nint address)
    {
        vtable = ModuleMemoryReader.MainModuleBase() + (nint)ArrayVtable;
        global = address;
    }

    /// <summary>The main mission rows, or null when the table is unknown, not loaded or not as expected.</summary>
    public IReadOnlyList<MainMissionRow>? Rows()
    {
        var (rows, state) = Read();
        if (log is not null)
        {
            lock (gate)
            {
                if (state == lastState)
                {
                    return rows;
                }
                lastState = state;
            }
            log("mission table " + state);
        }
        return rows;
    }

    private (IReadOnlyList<MainMissionRow>? Rows, string State) Read()
    {
        if (global == 0)
        {
            return (null, "not located");
        }
        if (ModuleMemoryReader.ReadPointer(global) is not { } table || table == 0)
        {
            return (null, "not loaded");
        }
        if (ModuleMemoryReader.ReadPointer(table + StaticArray) is not { } found || found != vtable)
        {
            return (null, "vtable unexpected");
        }
        // The count bounds the vector index; only the first vector is read [NEEDS VERIFICATION]: that it is always 4.
        if (ModuleMemoryReader.ReadInt32(table + Count) is not { } count || count is < 1 or > Categories)
        {
            return (null, "count unexpected");
        }
        if (ModuleMemoryReader.ReadPointer(table + MainBegin) is not { } begin || begin == 0 ||
            ModuleMemoryReader.ReadPointer(table + MainEnd) is not { } end)
        {
            return (null, Invariant($"count={count} vector unreadable"));
        }
        var length = (long)end - begin;
        if (length < 0 || length > (long)MainMissionTable.MaxRows * MainMissionTable.RowSize)
        {
            return (null, Invariant($"count={count} length={length} unexpected"));
        }
        var bytes = new byte[length];
        if (!ModuleMemoryReader.ReadBytes(begin, bytes))
        {
            return (null, Invariant($"count={count} rows unreadable"));
        }
        var rows = MainMissionTable.Rows(bytes);
        return (rows, rows is null
            ? Invariant($"count={count} length={length} not main rows")
            : Invariant($"count={count} rows={rows.Count}"));
    }
}
