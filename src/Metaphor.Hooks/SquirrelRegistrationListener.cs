using Metaphor.Core;

namespace Metaphor.Hooks;

/// <summary>Issue #55's spike: a development-only hook on <c>sq_newclosure</c> that logs every function the game
/// registers for its Squirrel scripts, by script name and address, to find the one that shows a dialogue line. The
/// loader's adapter passes every call through unchanged (#88).
///
/// The technique (which stack slots hold the name and the userdata) and the signatures follow MF.Toolkit's
/// <c>SquirrelService.NewClosure</c> (RyoTune/MF.Toolkit at b1f876da, AGPL-3.0;
/// docs/raw-sources/mf-toolkit-squirrel-registration-2026-11-16.md).</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed unsafe class SquirrelRegistrationListener : IGameHook, IObserves3
{
    // sq_newclosure(vm, func, freeVariables) returns nothing. MF.Toolkit declares the integers as int. They are
    // observed pointer-sized: sq_newclosure's own signature reads its third argument as 64 bits ("49 8B F8",
    // mov rdi, r8), and Win64 leaves the upper half of a register undefined for a 32-bit argument, so passing a 32-bit
    // value through could hand the game garbage. Pointer-sized is right whichever width the game reads.

    private readonly MessageLog? log;
    private delegate* unmanaged<nint, nint> getTop;
    private delegate* unmanaged<nint, nint, nint*, nint> getString;
    private delegate* unmanaged<nint, nint, nint*, nint*, nint> getUserdata;
    private nint moduleBase;

    public SquirrelRegistrationListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "sq_newclosure";

    public IReadOnlyList<string> Calls => ["sq_gettop", "sq_getstring", "sq_getuserdata"];

    public void UseCalls(IReadOnlyDictionary<string, nint> calls)
    {
        ArgumentNullException.ThrowIfNull(calls);
        getTop = (delegate* unmanaged<nint, nint>)calls["sq_gettop"];
        getString = (delegate* unmanaged<nint, nint, nint*, nint>)calls["sq_getstring"];
        getUserdata = (delegate* unmanaged<nint, nint, nint*, nint*, nint>)calls["sq_getuserdata"];
        moduleBase = ModuleMemoryReader.MainModuleBase();
    }

    public void Observe(nint vm, nint func, nint freeVariables)
    {
        if (log is not null)
        {
            LogRegistration(log, vm, func);
        }
    }

    /// <summary>The name is the string at the bottom of the script stack, and for a game function the native address
    /// sits inside the userdata above it, with <paramref name="func"/> a shared trampoline. A Squirrel call succeeds
    /// when its 32-bit result is zero or more. Everything read through a pointer the game returned goes through
    /// <c>ReadProcessMemory</c>.</summary>
    private void LogRegistration(MessageLog log, nint vm, nint func)
    {
        var stackSize = (int)getTop(vm);
        nint name = 0;
        if (stackSize <= 0 || (int)getString(vm, -stackSize + 1, &name) < 0 ||
            ModuleMemoryReader.ReadAnsiString(name) is not { } text)
        {
            return;
        }
        var location = func;
        var viaUserdata = false;
        nint userdata = 0;
        if (stackSize > 2 && (int)getUserdata(vm, -stackSize + 2, &userdata, null) >= 0 &&
            ModuleMemoryReader.ReadPointer(userdata) is { } native)
        {
            (location, viaUserdata) = (native, true);
        }
        log.Registered(text, location - moduleBase, viaUserdata);
    }
}
