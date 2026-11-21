using System.Text;
using Metaphor.Core;
using Metaphor.Core.Menus;

namespace Metaphor.Hooks;

// Issue #585: the name entry screen's input update, hooked in every build so the box's text can be echoed. The
// function, the fields and the guard are our reading of the exe on game build 548ddc95… (Steam build 18330018), in
// docs/findings/name-entry-static.md; nothing here is in or calls into .text1. The class names are the game's own,
// from its run-time type information. The typed text is the player's name: it goes to the observer only, and the log
// gets its length. Every argument is observed pointer-sized (rule 9).

/// <summary>What <c>fclNameEntry</c>'s update runs in its typing state: <c>(fclNameEntry* this) → 0</c>, once a frame
/// while the box takes input. Observed after it returns, it reads the keyboard object at <c>this+0x20</c> and that
/// object's text array at <c>+0x20</c>: 16-bit characters, their count at <c>+0x28</c>, the characters from
/// <c>+0x30</c>. The array's type is set in protected code, so it is read only when its vtable is the one the screen's
/// own name array at <c>this+0x458</c> has (<c>fw::StaticArray&lt;wchar_t,10&gt;</c>) and the count is at most 10;
/// otherwise nothing is reported. Six small reads a frame.</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class NameEntryInputHook : IGameHook, IObserves1WithResult
{
    private const int KeyboardOffset = 0x20;
    private const int TextArrayOffset = 0x20;
    private const int CountOffset = 0x28;
    private const int CharactersOffset = 0x30;
    private const int OwnNameArrayOffset = 0x458;
    private const int PlayerFlagOffset = 0x481;
    private const int MaxCharacters = 10;

    private readonly INameEntryObserver observer;
    private readonly MessageLog? log;
    private long loggedEntry;
    private int loggedLength = -1;

    public NameEntryInputHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        observer = services.NameEntry;
        log = services.Log;
    }

    public string SignatureName => "NameEntryInputUpdate";

    public void Observe(nint entry, nint result)
    {
        if (ModuleMemoryReader.ReadPointer(entry + KeyboardOffset) is not { } keyboard || keyboard == 0 ||
            ModuleMemoryReader.ReadPointer(entry + OwnNameArrayOffset) is not { } nameArrayType || nameArrayType == 0 ||
            ModuleMemoryReader.ReadPointer(keyboard + TextArrayOffset) != nameArrayType ||
            ModuleMemoryReader.ReadInt32(keyboard + CountOffset) is not { } count || count is < 0 or > MaxCharacters)
        {
            return;
        }
        var characters = new byte[count * 2];
        var flag = new byte[1];
        if (!ModuleMemoryReader.ReadBytes(keyboard + CharactersOffset, characters) ||
            !ModuleMemoryReader.ReadBytes(entry + PlayerFlagOffset, flag))
        {
            return;
        }
        var frame = new NameEntryFrame(entry, flag[0] != 0, Encoding.Unicode.GetString(characters));
        if (log is not null && (entry != loggedEntry || count != loggedLength))
        {
            loggedEntry = entry;
            loggedLength = count;
            log.NameEntryChanged(frame);
        }
        observer.Observe(frame);
    }
}
