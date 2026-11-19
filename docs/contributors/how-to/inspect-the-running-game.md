# Inspect the running game

A Debug build of the mod can answer read-only questions about the game's memory while you play: read bytes at an
address or pointer path, name an object's class, and watch addresses for changes. An agent asks through
[`scripts/inspect.ps1`](../../../scripts/inspect.ps1), so a finding can be checked at once instead of after the next
capture. No debugger is attached ([ground rule 5](../../../AGENTS.md#ground-rules)).

The inspector ([`LiveInspector.cs`](../../../src/Metaphor.Hooks/LiveInspector.cs), #291) is compiled into Debug builds
only, which an architecture test checks, and starts only with its opt-in file. It listens on the local named pipe
`\\.\pipe\metaphor-accessibility-inspector`, which only the user running the game can open and which refuses network
clients. Every read goes through `ModuleMemoryReader`; no command writes memory, calls game code or hooks anything.
Reloaded-II's log and `report-log.txt` record only that it listens and when a client connects or leaves, never what
was read.

## Turn it on

1. Deploy a Debug build ([deploy a dev build](deploy-a-dev-build.md)); a deploy replaces the mod's folder.
2. Create an empty file named `opt-in-inspector.txt` in the deployed mod's folder (`<mods>\metaphor.accessibility`).
   A Release build has no inspector and ignores the file.
3. Start the game from Reloaded-II. Its log says `live inspector: listening on \\.\pipe\metaphor-accessibility-inspector`.

To turn it off, delete the file and restart the game.

## Ask it

```powershell
pwsh scripts/inspect.ps1 'base' 'read base+0x1000 16' 'follow base+0x2a8f0 0x8 0x10'
```

Each argument is one request, sent in order on one connection; each prints one line, `ok ...` or `err ...`. The script
exits 0 when every answer is `ok`, 1 when one is `err`, and 2 when there is no inspector to reach (not running, not
opted in, a Release build, or another client connected: it serves one at a time).

## Commands

`<addr>` is `0x` and hex digits, or `base+0x` and hex digits for an address in the game's exe. `<offset>` is `0x` and
hex digits, with an optional leading `-`. `<count>` is decimal or `0x` hex. An unknown command is refused with
`err unknown command; try help`, bad arguments with `err usage: ...`, a line with bytes outside printable ASCII with
`err not printable ASCII`, and a line over 512 bytes with `err line too long`, which also ends the connection. A
response never spans lines: a control character in it, such as one in a class name read from memory, comes back as
`?`.

| Request | Answer |
|---|---|
| `help` | the commands |
| `base` | `ok <exe base> <exe size>` |
| `read <addr> <count>` | `ok <hex bytes>`, up to 4096 bytes; `err unreadable` when any byte can't be read, or lies on a page that isn't committed, readable and free of a guard (the mod checks with `VirtualQuery` first, so a guard page is left as it was) |
| `follow <addr> <offset>...` | `ok <addr>`: for each offset, reads the pointer at the current address and adds the offset, so `follow A 0x8 0x10` is `[[A]+0x8]+0x10`; up to 16 offsets; `err unreadable at step <n> (<addr>)` |
| `rtti <addr>` | `ok <class>`: the class of the object at `<addr>` from its vtable's run-time type information, as [`RttiName`](../../../src/Metaphor.Core/RttiName.cs) reads it; `err no class` |
| `watch <addr> <count>` | `ok <id>`: remembers up to 256 bytes at `<addr>`; up to 64 watches |
| `watches` | `ok <id>=<addr>+<count> ...` |
| `changes` | `ok <id>=<hex bytes or unreadable> ...`: each watch whose bytes changed since it was set or last reported |
| `unwatch <id>` | `ok`, or `err no watch <id>` |

Watches stay in the mod until the game exits, so `watch` in one run of the script and `changes` in a later one report
what changed in between.

## What it doesn't do

- It doesn't log or keep what it reads; what you print is yours to keep out of commits, like any capture
  ([ground rule 4](../../../AGENTS.md#ground-rules)). Game text read with `read` is game content.
- It doesn't stop the game or wait for a frame: two reads see memory at two moments.
- A client that connects and sends nothing holds the pipe until it closes. Only your own user can connect.
