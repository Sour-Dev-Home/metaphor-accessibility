# Hooking through one shared non-generic delegate per shape: every shape (offline spike)

- **Source:** a throwaway console program (review follow-up to issue #88), kept outside the repository, run once on the
  maintainer's machine. It extends `shared-delegate-hook-spike-2026-11-16.md`, which ran only the `3 × nint` and
  `nint, nint, double, nint` shapes, to every adapter shape in `src/Metaphor.Hooks/PassThrough.cs`. Tools: .NET SDK
  9.0.319, runtime Microsoft.NETCore.App 9.0.21, Windows 11 x64; NuGet `Reloaded.Hooks` **3.4.1** (standalone), which
  resolved `Reloaded.Hooks.Definitions` 1.11.0, `Reloaded.Assembler` 1.0.10, `Reloaded.Memory` 4.1.0,
  `Reloaded.Memory.Buffers` 1.4.1.
- **Saved:** 2026-11-16.
- **Game build:** none. The program never touches the game; it hooks functions it writes into its own memory.

Which Reloaded.Hooks version the Reloaded-II loader ships at run time is `[NEEDS VERIFICATION]`; the in-game smoke test
covers it.

## Result

Every shape passed every check. `dotnet run -c Release` printed `ALL PASS` and exited 0.

| Shape (native function) | Installs | Observer saw | Passed through | After forced GCs + 10,000 calls |
|---|---|---|---|---|
| `3 × nint` (`add3`) | pass | `(10,20,30)` | returned 60 | returned 10049, saw `(9999,20,30)` |
| `4 × nint` (`add4`) | pass | `(10,20,30,40)` | returned 100 | returned 10089, saw `(9999,20,30,40)` |
| `5 × nint`, fifth on the stack (`add5`) | pass | `(10,20,30,40,50)` | returned 150 | returned 10139, saw `(9999,20,30,40,50)` |
| `nint, nint, double, nint` (`add4d`) | pass | `(10,20,7.5,30)` | returned 67 | returned 10056, saw `(9999,20,7.5,30)` |
| `nint, nint, nint, double, nint`, fifth on the stack (`add5d`) | pass | `(10,20,30,7.5,50)` | returned 117 | returned 10106, saw `(9999,20,30,7.5,50)` |
| Function returning nothing, through the `nint`-returning `3 × nint` shape (`store3`) | pass | `(slot,10,7)` | stored 17 | stored 10006, saw `(slot,9999,7)` |

Baseline before hooking: `6,10,15,10,15`, and `store3` stored 9.

## Steps

1. `dotnet new console --framework net9.0` in an empty folder outside the repository, then
   `dotnet add package Reloaded.Hooks --version 3.4.1`, and `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.
2. The program allocates a read-write-execute page per function with `VirtualAlloc` and writes each after 16 `nop`s, so
   the hook has room for its jump (Windows x64 calling convention; the fifth argument is at `[rsp+0x28]` on entry):
   - `add3`: `48 8D 04 11` (`lea rax,[rcx+rdx]`), `4C 01 C0` (`add rax,r8`), `C3` (`ret`).
   - `add4`: as `add3`, plus `4C 01 C8` (`add rax,r9`) before `ret`.
   - `add5`: as `add4`, plus `48 03 44 24 28` (`add rax,[rsp+0x28]`) before `ret`.
   - `add4d(a, b, d, c)`: `lea rax,[rcx+rdx]`, `add rax,r9`, `F2 4C 0F 2C D2` (`cvttsd2si r10,xmm2`),
     `4C 01 D0` (`add rax,r10`), `ret`.
   - `add5d(a, b, c, d, e)`: `lea rax,[rcx+rdx]`, `add rax,r8`, `F2 4C 0F 2C D3` (`cvttsd2si r10,xmm3`),
     `add rax,r10`, `add rax,[rsp+0x28]`, `ret`.
   - `store3(p, b, c)`: `48 89 11` (`mov [rcx],rdx`), `4C 01 01` (`add [rcx],r8`), `ret`: returns nothing (`rax` is
     left as it was) and stores `b + c` at `p`.
3. It calls each through a `delegate* unmanaged` function pointer before hooking (baseline; `store3` through a
   `void`-returning one), then hooks each with an adapter built like `PassThrough`'s: one private non-generic `nint`-
   returning delegate per shape, the callback and the hook in fields, the hook stored before `Activate`, the observer
   behind a catch-all, the original always called. It uses `ReloadedHooks.Instance` and holds the adapters only in a
   list. `store3` is hooked with the `3 × nint` adapter.
4. It calls every function once through the same pointers, then makes three forced, blocking, compacting collections
   (after a 64 MiB allocation each), then 10,000 calls of every function with another forced collection every 2,500,
   checking each result and the observer's last arguments on every call.

One difference from the repository's adapters: the spike assigns the callback field in `Activate`, just before
`CreateHook`, rather than in the constructor. Either way the delegate lives in a field of an adapter the caller keeps.
