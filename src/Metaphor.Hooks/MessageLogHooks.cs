using System.Globalization;
using Metaphor.Core;

namespace Metaphor.Hooks;

// Issue #685: the game's message log (the backlog). The function, its argument and every field offset below are our
// reading of the exe on one build (docs/findings/message-log-screen-static.md), not yet checked in the running game.
// The one argument is pointer-sized (rule 9).

/// <summary>The update function of the game's message log, <c>UUIC_Common_MessageLog</c>'s 14th virtual function:
/// <c>void (UUIC_Common_MessageLog* this)</c>. The log isn't a choice, so it never calls the shared cursor getter and
/// its cursor inputs had no screen class (#685's comment of 2026-11-21: <c>screen=?</c>); its update moves the
/// <c>CursorManager</c> whose pointer is at <c>+0x30</c>, so each call records the log as that manager's owner, as
/// <see cref="DifficultySelectOwnerHook"/> does for the difficulty list. One pointer read a call. In Debug builds it
/// also logs the focused row's raw fields when the cursor moves, for the next session to find where the rows keep
/// their words (<c>[NEEDS VERIFICATION]</c>).</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class MessageLogOwnerHook : IGameHook, IObserves3
{
    private const int ManagerOffset = 0x30;
    // The manager's cursor index, as the menu cursor hook reads it (docs/findings/difficulty-select-shared-cursor.md).
    private const int IndexOffset = 0x60;
    // The layout object at +0x20, and the 30 list rows it copies to its +0x28e20: a StaticArray (vtable, count) then
    // rows of 0x120 bytes from +0x10, each 15 shared_ptr<fw::font::StringData> after its own 0x10 header, one more at
    // +0x100, an int at +0x118 (docs/findings/message-log-screen-static.md).
    private const int LayoutOffset = 0x20;
    private const int RowsOffset = 0x28e20;
    private const int RowsHeader = 0x10;
    private const int RowLength = 0x120;
    private const int RowCount = 30;
    private const int DumpLength = 0x40;

    private readonly CursorOwners owners;
    private readonly MessageLog? log;
    // The last (log, index) logged, so a Debug build logs a row once per move, not every frame.
    private long lastLogged = -1;

    public MessageLogOwnerHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        owners = services.ScreenClasses.Owners;
        log = services.Log;
    }

    public string SignatureName => "MessageLogUpdate";

    /// <summary>Observed before the update runs; only <paramref name="messageLog"/> is an argument, the other two are
    /// whatever the registers held.</summary>
    public void Observe(nint messageLog, nint second, nint third)
    {
        if (ModuleMemoryReader.ReadPointer(messageLog + ManagerOffset) is not { } manager || manager == 0)
        {
            return;
        }
        owners.Saw(messageLog, manager);
        if (log is not null)
        {
            LogFocusedRow(log, messageLog, manager);
        }
    }

    // Debug builds only (the log is null in Release): bounded reads through ModuleMemoryReader, hex only.
    private void LogFocusedRow(MessageLog messageLogFile, nint messageLog, nint manager)
    {
        if (ModuleMemoryReader.ReadInt32(manager + IndexOffset) is not { } index || index < 0 || index >= RowCount)
        {
            return;
        }
        var key = ((long)messageLog << 8) ^ index;
        if (Interlocked.Exchange(ref lastLogged, key) == key)
        {
            return;
        }
        var row = ModuleMemoryReader.ReadPointer(messageLog + LayoutOffset) is { } layout && layout != 0
            ? layout + RowsOffset + RowsHeader + (index * RowLength)
            : 0;
        messageLogFile.MenuNote(string.Create(CultureInfo.InvariantCulture,
            $"messagelog log=0x{messageLog:x} index={index} state={ModuleMemoryReader.ReadInt32(messageLog + 0x18)} " +
            $"count={ModuleMemoryReader.ReadInt32(messageLog + 0x50)} valid={ModuleMemoryReader.ReadInt32(messageLog + 0x58)} " +
            $"row=0x{row:x} strings={(row == 0 ? null : ModuleMemoryReader.ReadInt32(row + 8))} " +
            $"first={Dump(row == 0 ? null : ModuleMemoryReader.ReadPointer(row + 0x10))} " +
            $"extra={Dump(row == 0 ? null : ModuleMemoryReader.ReadPointer(row + 0x100))} " +
            $"tail={(row == 0 ? null : ModuleMemoryReader.ReadInt32(row + 0x118))}"));
    }

    private static string Dump(nint? at)
    {
        if (at is not { } address || address == 0)
        {
            return "none";
        }
        // An array, not stackalloc: a stack buffer's loads are raw-memory reads outside the reader (architecture rule 10).
        var bytes = new byte[DumpLength];
        return ModuleMemoryReader.ReadBytes(address, bytes)
            ? string.Create(CultureInfo.InvariantCulture, $"0x{address:x}:{Convert.ToHexString(bytes)}")
            : string.Create(CultureInfo.InvariantCulture, $"0x{address:x}:unreadable");
    }
}
