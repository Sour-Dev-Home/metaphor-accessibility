# Query the Ghidra project

A local Ghidra project of the game exe answers static questions in one command: a function's code, who calls it, what
touches a struct field or a global, what class a vtable belongs to. Build it once per game build and every agent and
worktree shares it, so nobody disassembles from scratch.

It reads the exe on disk only. Nothing is launched, attached to, decrypted or unpacked (`AGENTS.md` rule 5). The
project holds the exe's bytes and every answer holds decompiled code, so they live in the main checkout's `out/ghidra/`
folder, which ignores itself and which the checks block by folder and by extension (rule 4). Quote addresses and names
in notes, not the output.

## Install once

Ghidra 12.2 and the JDK 25 it needs, from their GitHub releases with their published SHA-256 checked
([`ghidra-and-jdk-install-2026-11-17.md`](../../raw-sources/ghidra-and-jdk-install-2026-11-17.md)). Extract both to
`%LOCALAPPDATA%\Programs`, where the script finds the newest `ghidra_*_PUBLIC` and `jdk-*` folder, or set
`GHIDRA_INSTALL_DIR` and `JAVA_HOME`. No Python is needed.

## Build the project

```powershell
pwsh scripts/dev/ghidra.ps1 build
```

It finds the exe as the other scripts do (`-GameExe`, `METAPHOR_GAME_EXE`, then Reloaded-II) and names the project
folder after its build's SHA-256. A first build takes about 10 minutes; a new game build gets a new project.

A second run on the same build prints `reusing`, then names any `hook-signatures.json` entries added since (step 3
alone) and lists them, or prints `no new signature names`. Run it after adding a signature. `-Rebuild` starts over, and
is still needed when:

- a signature is renamed or removed, or its pattern now matches elsewhere: the old name stays where it was;
- `tools/ghidra/MetaphorBuild.java`'s analysis or vtable naming changes: those ran once, when the project was built;
- the project is damaged or was saved over by hand.

The build:

1. imports the exe with no analysis, then renames `.debug` to `.data.debug` and `.udata` to `.rdata.udata` in the
   project only, because Ghidra's RTTI analyzer searches only blocks with those prefixes and the type descriptors are
   in `.debug` (`tools/ghidra/MetaphorBuild.java` says how that was found);
2. auto-analyses every section except `.text1`, `.xcode` and `.xtext` (the protection's, [ADR 0010](../../adr/0010-text1-hooks-stay-opt-in.md)),
   which are non-executable while it runs so disassembly never flows into them. The "Create Address Tables" analyzer
   is off: it runs out of a 16G heap on this exe;
3. names each [`hook-signatures.json`](../../../src/Metaphor.Core/hook-signatures.json) entry that matches exactly once
   in its section;
4. names each unnamed function in a vtable after its class and slot (`ConfigMenuChoice::vf2`), from the vtables
   Ghidra's RTTI analyzer labelled in step 2. An inherited function takes the first class by vtable address and lists
   the others in its plate comment. This replaces Ghidra's `RecoverClassesFromRTTIScript.java`, which also recovers
   struct layouts but ran for over 50 minutes decompiling `.text1` bodies that don't decode.

## Ask it

```powershell
pwsh scripts/dev/ghidra.ps1 decompile 0x21e1c0
pwsh scripts/dev/ghidra.ps1 disasm CursorManagerInput
pwsh scripts/dev/ghidra.ps1 callers 0x21e1c0
pwsh scripts/dev/ghidra.ps1 callees CursorManagerInput
pwsh scripts/dev/ghidra.ps1 offset 0x48 CursorManagerInput
pwsh scripts/dev/ghidra.ps1 global 0x2f04960
pwsh scripts/dev/ghidra.ps1 rtti 0xd4ab90
pwsh scripts/dev/ghidra.ps1 vtable <class name or vtable address>
```

A target is an RVA as the raw sources write them (`0x21e1c0`), a full address (`0x14021e1c0`) or a symbol name. Output
addresses are RVAs.

| Command | Answers |
|---|---|
| `decompile`, `disasm` | the function containing the target |
| `callers`, `callees` | each call site, with the calling or called function |
| `offset <displacement> [<function>]` | instructions with a `[register + displacement]` operand, read or written, in one function or all analysed code |
| `global <address>` | instructions with a RIP-relative operand at that global (`mov rcx,[rip+disp32]`), as `read`, `write`, `rw`, `addr` (`lea`) or `call`, with each one's function, or the nearest name before it when no function holds it. Proven on the menus' handle and the message window ([finding](../../findings/global-query-finds-the-reader-loads.md)) |
| `rtti` | the class of a vtable, of an object holding a vtable pointer, or of each vtable holding a pointer to a function |
| `vtable` | the vtable's class and each slot's function |

A target in a section the build skipped is disassembled for that query only; the project opens read-only, so nothing a
query does is saved. The answer is also kept in `out/ghidra/out/`, and Ghidra's logs in `out/ghidra/logs/`.

Ghidra locks the project, so run one command at a time.

## Ask many at once

Every call pays for Ghidra's startup and for opening the project, which is most of a short query's time, and holds the
lock for all of it. When you have several questions, put them in a file and ask them in one run:

```powershell
pwsh scripts/dev/ghidra.ps1 batch queries.txt
```

The file has one query per line, in the form the commands above take (`<query> <target> [<in>]`). Blank lines and lines
starting with `#` are skipped.

```text
# who touches the cursor
disasm 0x21e1c0
callers CursorManagerInput
offset 0x48 CursorManagerInput
```

Each answer prints under a `=== <query> ===` line and is kept in `out/ghidra/out/` as a single query's is; the project is
still opened read-only. A line that fails (an unknown query, no such symbol) prints its `# error:` and the rest still
run; the exit code is 1 if any line failed. Queries run in file order in one session, so a function one of them creates
on demand (see above) is already there for the next, which then doesn't print the "created for this query only" note.

## Browse it by hand

To look around rather than ask, open the project in Ghidra's own window.

1. Start Ghidra (`ghidraRun.bat` in its install folder) and choose File > Open Project.
2. Pick `out/ghidra/<build>/metaphor.gpr` in the main checkout, where `<build>` is the first 16 hex characters of the exe's
   SHA-256 (`scripts/dev/ghidra.ps1` names the folder this way, and the project `metaphor`). Open the main checkout's
   copy, not a worktree's.
3. Open the exe from the project window. Our names show in the Symbol Tree and Listing: signature names from
   `hook-signatures.json`, and `Class::vfN` for vtable functions, with any other inheriting classes in the plate comment.
   Filter the Symbol Tree for `::vf` to list the latter. [NEEDS VERIFICATION: the menu and filter steps; Ghidra 12.2's
   help is in its install's `docs/` but isn't saved to `docs/raw-sources/` yet.]

While the window has the project open it holds the project's lock, so `ghidra.ps1` queries can't open it until you
close the window (the script's header: "Ghidra locks the project"). Don't save changes over the shared project: the
queries open it read-only and every agent relies on it as built. To experiment, close Ghidra, copy the `out/ghidra/<build>/`
folder to another name in `out/ghidra/`, and delete the copy when done.

Rule 4 still holds: quote names and addresses in notes, never decompiled output.
