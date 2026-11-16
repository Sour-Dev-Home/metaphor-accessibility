using System.Diagnostics.CodeAnalysis;
using Reloaded.Hooks.Definitions;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace Metaphor.Hooks;

/// <summary>Issue #55's spike: a development-only hook on <c>sq_newclosure</c> that logs every function the game
/// registers for its Squirrel scripts, by script name and address, to find the one that shows a dialogue line. It
/// passes every call through unchanged.
///
/// The technique (which stack slots hold the name and the userdata) and the signatures follow MF.Toolkit's
/// <c>SquirrelService.NewClosure</c> (RyoTune/MF.Toolkit at b1f876da, AGPL-3.0;
/// docs/raw-sources/mf-toolkit-squirrel-registration-2026-11-16.md).</summary>
[GameHook("548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504", DevOnly = true)]
internal sealed unsafe class SquirrelRegistrationListener : IGameHook
{
    // MF.Toolkit declares the integers below as int. They are pointer-sized here: sq_newclosure's own signature
    // reads its third argument as 64 bits ("49 8B F8", mov rdi, r8), and Win64 leaves the upper half of a register
    // undefined for a 32-bit argument, so passing a 32-bit value through could hand the game garbage. Pointer-sized is
    // right whichever width the game reads.
    private delegate void SqNewClosure(nint vm, nint func, nint freeVariables);

    private readonly SqNewClosure callback;
    private IHook<SqNewClosure>? hook;
    private delegate* unmanaged<nint, nint> getTop;
    private delegate* unmanaged<nint, nint, nint*, nint> getString;
    private delegate* unmanaged<nint, nint, nint*, nint*, nint> getUserdata;
    private nint moduleBase;

    public SquirrelRegistrationListener() => callback = OnNewClosure;

    public string SignatureName => "sq_newclosure";

    public IReadOnlyList<string> Calls => ["sq_gettop", "sq_getstring", "sq_getuserdata"];

    public void Install(IReloadedHooks hooks, nint address, IReadOnlyDictionary<string, nint> calls)
    {
        getTop = (delegate* unmanaged<nint, nint>)calls["sq_gettop"];
        getString = (delegate* unmanaged<nint, nint, nint*, nint>)calls["sq_getstring"];
        getUserdata = (delegate* unmanaged<nint, nint, nint*, nint*, nint>)calls["sq_getuserdata"];
        moduleBase = ModuleMemoryReader.MainModuleBase();
        // Stored before it goes live: the game may call it the moment it's activated.
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    [SuppressMessage("Design", "CA1031", Justification = "An exception must never cross back into the game.")]
    private void OnNewClosure(nint vm, nint func, nint freeVariables)
    {
        try
        {
            if (MessageListening.Log is { } log)
            {
                LogRegistration(log, vm, func);
            }
        }
        catch (Exception)
        {
        }
        hook!.OriginalFunction(vm, func, freeVariables);
    }

    /// <summary>The name is the string at the bottom of the script stack, and for a game function the native address
    /// sits inside the userdata above it, with <paramref name="func"/> a shared trampoline. A Squirrel call succeeds
    /// when its 32-bit result is zero or more. Everything read through a pointer the game returned goes through
    /// <c>ReadProcessMemory</c>.</summary>
    private void LogRegistration(Metaphor.Core.MessageLog log, nint vm, nint func)
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
