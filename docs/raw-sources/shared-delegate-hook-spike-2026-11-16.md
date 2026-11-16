# Hooking through one shared non-generic delegate per shape (offline spike)

- **Source:** a throwaway console program (issue #88's spike), kept outside the repository, run once on the maintainer's
  machine. Tools: .NET SDK 9.0.319, runtime Microsoft.NETCore.App 9.0.21, Windows 11 x64; NuGet `Reloaded.Hooks`
  **3.4.1** (standalone), which resolved `Reloaded.Hooks.Definitions` 1.11.0 (the version the mod compiles against),
  `Reloaded.Assembler` 1.0.10, `Reloaded.Memory` 4.1.0, `Reloaded.Memory.Buffers` 1.4.1.
- **Saved:** 2026-11-16.
- **Game build:** none. The program never touches the game; it hooks a function it writes into its own memory.

Which Reloaded.Hooks version the Reloaded-II loader ships at run time is `[NEEDS VERIFICATION]`; the in-game smoke test
covers it.

## Result

All four checks passed, plus three extra ones.

| Check | Result | Output |
|---|---|---|
| Native function works unhooked (baseline) | pass | `add3(1,2,3)=6, addWithDouble(1,2,4.0,3)=10` |
| 1. The hook installs | pass | `CreateHook` and `Activate` returned for both adapters |
| 2. The observer sees the arguments | pass | `observer saw (10,20,30)` |
| 3. The call passes through | pass | `hooked add3(10,20,30) returned 60` |
| 4. It survives a forced garbage collection | pass | `10000 calls after forced compacting collections, last seen (9999,9999,9999)` |
| Extra: `nint, nint, double, nint` shape | pass | `observer saw (1,2,7.5,3), returned 13` |
| Extra: an observer that throws still passes through | pass | `returned 3` |

## Steps

1. `dotnet new console --framework net9.0` in an empty folder outside the repository, then
   `dotnet add package Reloaded.Hooks --version 3.4.1`, and `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.
2. The program allocates a read-write-execute page with `VirtualAlloc` and writes two functions into it, each after 16
   `nop`s so the hook has room for its jump:
   - `add3`: `48 8D 04 11` (`lea rax,[rcx+rdx]`), `4C 01 C0` (`add rax,r8`), `C3` (`ret`): returns `a + b + c`.
   - `addWithDouble(a, b, d, c)`: `lea rax,[rcx+rdx]`, `4C 01 C8` (`add rax,r9`), `F2 4C 0F 2C D2`
     (`cvttsd2si r10,xmm2`), `4C 01 D0` (`add rax,r10`), `ret`: returns `a + b + (long)d + c`.
3. It calls each through a `delegate* unmanaged` function pointer before hooking (baseline), then builds one adapter per
   function with `ReloadedHooks.Instance`, holding the adapters only in a list.
4. It calls the functions through the same function pointers, then makes three forced, blocking, compacting
   collections (after a 64 MiB allocation each), then 10,000 more calls with another forced collection every 2,500.
5. `dotnet run -c Release` printed `ALL PASS` and exited 0.

## Key snippet

The shape under test: one non-generic delegate per parameter shape, declared once and shared by every hook of that
shape. The adapter keeps the delegate and the hook in its own fields, stores the hook before activating it, guards the
observer with a catch-all and always calls the original.

```csharp
internal delegate nint Pass3(nint a, nint b, nint c);

internal sealed class Adapter3
{
    private readonly IObserves3 observer;
    private readonly Pass3 callback;
    private readonly IHook<Pass3> hook;

    public Adapter3(IReloadedHooks hooks, nint address, IObserves3 observer)
    {
        this.observer = observer;
        callback = OnCall;
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    private nint OnCall(nint a, nint b, nint c)
    {
        try { observer.Observe(a, b, c); } catch (Exception) { }
        return hook.OriginalFunction(a, b, c);
    }
}
```

## Why not the alternatives

Not tested here; recorded in issue #88. Generic delegates are refused by .NET's interop marshalling
(`Marshal.GetDelegateForFunctionPointer`: "Generics are not supported in interop scenarios"), and
`[UnmanagedCallersOnly]` callbacks must be static, which architecture rule 12 rules out.
