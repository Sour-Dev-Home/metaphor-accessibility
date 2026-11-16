# How MF.Toolkit reads the game's script functions as they are registered (Squirrel)

- **Source:** `RyoTune/MF.Toolkit` at commit `b1f876da5b4c1f42b5eef07fc6c2174030beb77b` (AGPL-3.0), read through the
  GitHub REST API (`gh api`): `MF.Toolkit.Reloaded/Squirrel/SquirrelPatterns.cs`,
  `MF.Toolkit.Interfaces/Squirrel/SquirrelDefinitions.cs` and `MF.Toolkit.Reloaded/Squirrel/SquirrelService.cs`. The
  disk check below ran `tools/Metaphor.SignatureCheck --probes` with `mf-toolkit-squirrel-probes.json`.
- **Saved:** 2026-11-16.
- **Game build:** the source names none. The four signatures below were checked on disk on
  `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Signatures and delegates

`SquirrelPatterns.cs` (lines 10–21) and `SquirrelDefinitions.cs`:

| Function | Pattern | Delegate |
|---|---|---|
| `sq_newclosure` | `48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 49 8B F8 48 8B F1` | `void sq_newclosure(SQVM* v, nint func, int nfreevars)` |
| `sq_gettop` | `48 8B 41 48 48 2B 41 50 C3` | `int sq_gettop(SQVM* v)` |
| `sq_getstring` | `48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 4C 89 C3 48 89 CF` | `int sq_getstring(SQVM* v, int idx, nint* c)` |
| `sq_getuserdata` | `48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 49 8B D9 49 8B F8 48 8B F1` | `int sq_getuserdata(SQVM* v, int idx, nint* p, nint* typeTag)` |

The file has seven more (`sq_pushroottable`, `sq_pushnull`, `sq_next`, `sq_pop`, `sq_pushconsttable`, `sq_getclosure`,
`SQVM_GetAt`), not copied here.

## The registration hook

`SquirrelService.cs` hooks `sq_newclosure` (line 56) and reads the name the game pushed before registering a native
function (lines 71–90):

```csharp
private unsafe void NewClosure(SQVM* v, nint func, int nfreevars)
{
    var nameBuffer = (nint*)Marshal.AllocHGlobal(sizeof(nint));
    var funcBuffer = (nint*)Marshal.AllocHGlobal(sizeof(nint));

    var stackSize = sq_gettop(v);
    if (stackSize > 0 && SQ_SUCCEEDED(sq_getstring(v, stackSize * -1 + 1, nameBuffer)))
    {
        var funcLoc = func;

        // Get function pointer from userdata (game functions).
        if (stackSize > 2)
        {
            sq_getuserdata(v, stackSize * -1 + 2, funcBuffer, (nint*)0);
            funcLoc = **(nint**)funcBuffer;
        }
        ...
            var funcName = Marshal.PtrToStringAnsi(*nameBuffer)!;
```

So at registration the function's name is a string on the Squirrel stack, and for game functions the native function's
address is inside a userdata value, with `func` a shared trampoline ("premade functions that take in the native function
as a single userdata", its comment says). It then calls the original `sq_newclosure`. MF.Toolkit can dump the
resulting name-and-address list to a file (`dumpFunctions`).

## On our build, on disk

```text
game build 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504 (Steam build 18330018)
ok   sq_newclosure: 1 match at 0x13c9a50 (rva 0x13ca450)
ok   sq_gettop: 1 match at 0x13c92d0 (rva 0x13c9cd0)
ok   sq_getstring: 1 match at 0x12f87e50 (rva 0x138da250)
ok   sq_getuserdata: 1 match at 0x13c9400 (rva 0x13c9e00)
4 of 4 signatures matched exactly once
```

Whether the stack layout MF.Toolkit assumes holds on this build is `[NEEDS VERIFICATION]`: it is what issue #55's
registration listener tests.
