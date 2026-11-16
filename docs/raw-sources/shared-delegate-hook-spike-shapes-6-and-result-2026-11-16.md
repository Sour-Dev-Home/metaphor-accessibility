# Hooking through one shared non-generic delegate: `IObserves6` and `IObserves1WithResult` (offline spike)

- **Source:** a throwaway console program, kept outside the repository, run once on the maintainer's machine. It follows
  the method of `shared-delegate-hook-spike-shapes-2026-11-16.md` for the two shapes added to
  `src/Metaphor.Hooks/PassThrough.cs` on branch `spike-prep-session-1` (at `7008e5f`) for the listeners in `39637f5`
  (`msg-select-functions-2026-11-16.md`). Tools: .NET SDK 9.0.319, runtime Microsoft.NETCore.App 9.0.21, Windows 11
  x64; NuGet `Reloaded.Hooks` **3.4.1** (standalone), which resolved `Reloaded.Hooks.Definitions` 1.11.0,
  `Reloaded.Assembler` 1.0.10, `Reloaded.Memory` 4.1.0, `Reloaded.Memory.Buffers` 1.4.1.
- **Saved:** 2026-11-16.
- **Game build:** none. The program never touches the game; it hooks functions it writes into its own memory.

Which Reloaded.Hooks version the Reloaded-II loader ships at run time is `[NEEDS VERIFICATION]`; the in-game smoke test
covers it.

## Result

Both shapes passed every check. `dotnet run -c Release` printed `ALL PASS` and exited 0.

| Shape (native function) | Installs | Observer saw (first hooked call) | Passed through | After forced GCs + 10,000 calls |
|---|---|---|---|---|
| `IObserves6`, 16-bit sixth (`add6w`) | pass | `(10,20,30,40,60,0x7EADBEE5CAFE000A)` | returned 170 (low 16 bits of the sixth only) | returned 30137, saw `(9999,20,30,40,10049,0x7EAD99E0CAFE270F)` |
| `IObserves6` (`echo6`) | pass | `(10,20,30,40,60,0x7EADBEE5CAFE000A)` | returned `0x7EADBEE5CAFE000A`: the sixth slot reached the original with its upper bits unchanged | returned and saw `0x7EAD99E0CAFE270F` |
| `IObserves1WithResult` (`counter`) | pass | `(cell,101)`, with the cell already 102 | returned 101 | returned 10102, saw `(cell,10102)` with the cell at 10103 |
| `IObserves1WithResult`, full 64-bit `rax` (`wide`) | pass | `(10,0xDEADBEEFFFFFFFFF)` | returned `0xDEADBEEFFFFFFFFF` | same, with first argument 9999 |
| `IObserves1WithResult`, observer that throws (`twice7`) | pass | threw on every call (10,002 times) | returned 27 | returned 20005 |

Baseline before hooking: `add6w(1,2,3,4,5,0x7EADBEEFCAFE0123)` = 306, `echo6` returned `0x7EADBEEFCAFE0123`, `counter`
returned 100, `wide` returned `0xDEADBEEFFFFFFFFF`, `twice7(5)` = 17.

So: both stack arguments arrive in the right order; the sixth reaches the observer and the original as the full 64-bit
slot, garbage upper bits included, and the callee's 16-bit read sees only its low word; the result observer runs after
the original (the cell was already incremented) and sees its whole `rax`, which the caller gets back unchanged.

## Steps

1. `dotnet new console --framework net9.0` in an empty folder outside the repository, then
   `dotnet add package Reloaded.Hooks --version 3.4.1`, and `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.
2. The program allocates a read-write-execute page per function with `VirtualAlloc` and writes each after 16 `nop`s
   (Windows x64 calling convention; on entry the fifth argument is at `[rsp+0x28]`, the sixth at `[rsp+0x30]`):
   - `add6w`: `48 8D 04 11` (`lea rax,[rcx+rdx]`), `4C 01 C0` (`add rax,r8`), `4C 01 C8` (`add rax,r9`),
     `48 03 44 24 28` (`add rax,[rsp+0x28]`), `4C 0F B7 54 24 30` (`movzx r10,word [rsp+0x30]`), `4C 01 D0`
     (`add rax,r10`), `C3`: returns `a+b+c+d+e+(ushort)f`, reading the sixth as 16 bits like `MsgRequestSelectByName`.
   - `echo6`: `48 8B 44 24 30 C3` (`mov rax,[rsp+0x30]; ret`): returns the sixth slot whole.
   - `counter(p)`: `48 8B 01` (`mov rax,[rcx]`), `48 FF 01` (`inc qword [rcx]`), `C3`: returns `*p`, then increments it.
   - `wide`: `48 B8 FF FF FF FF EF BE AD DE` (`mov rax,0xDEADBEEFFFFFFFFF`), `C3`.
   - `twice7`: `48 89 C8` (`mov rax,rcx`), `48 01 C0` (`add rax,rax`), `48 83 C0 07` (`add rax,7`), `C3`.
3. It calls each through a `delegate* unmanaged` function pointer before hooking (baseline), then hooks `add6w` and
   `echo6` with the `IObserves6` adapter and the other three with the `IObserves1WithResult` adapter, using
   `ReloadedHooks.Instance` and holding the adapters only in a list.
4. It calls every function once through the same pointers, then makes three forced, blocking, compacting collections
   (after a 64 MiB allocation each), then 10,000 rounds with another forced collection every 2,500. Round `i` passes
   `(i, 20, 30, 40, 50+i, 0x7EADBEEFCAFE0000 | i ^ i<<32)`, so both stack arguments and the sixth's upper bits change
   each call, and checks every result and every observer's arguments on every call.

## Differences from the branch's adapters

`Adapter6` and `Adapter1WithResult` are copied from `PassThrough.cs` at `7008e5f` (delegate, fields, constructor-assigned
callback, `Activate`, `OnCall`) unchanged except that, standalone, there is no `IGameHook`: the base class has no `Hook`
property or `UseCalls`, `For` switches on the observer itself, and `Install` takes no calls. `IReloadedHooks` is
`Reloaded.Hooks.Definitions`' (from `ReloadedHooks.Instance`), not the Reloaded-II controller's.

The caller here writes the sixth argument as a full 64-bit value with deliberate garbage above the low 16 bits. Whether
the game's callers write only the low 16 bits of that slot is `[NEEDS VERIFICATION]`; the adapter forwards the whole
slot either way.
