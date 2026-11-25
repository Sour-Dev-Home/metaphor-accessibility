# The message log is `UUIC_Common_MessageLog`; its rows' words are font strings not yet decoded

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod's Debug message log from the maintainer's sessions (kept local, read with grep)
- **Sources:** none, see Steps
- **Functions:**
  - UUIC_Common_MessageLog::vf13 | 0x140f6e300 | .ecode | Dialogue | hooked | The log's update: opens, runs and closes it as a four-state machine at `+0x18`. | finding
  - UUIC_Common_MessageLog row build | 0x140f6da40 | .ecode | Dialogue | known only | Builds the log's 30 rows from the game's log entries and hands them to the layout. | finding
  - MessageLogLayout row copy | 0x140ad68c0 | .ecode | Dialogue | known only | Copies the 30 rows into the layout at `+0x28e20`. | finding
  - Message log entry getter | 0x14a644840 | .text1 | Dialogue | known only | Returns log entry `n` (0 to 29); its body is in `.text1`, so unread. | finding

The game's message log (the backlog, #685) is the class `app::ui::UUIC_Common_MessageLog` (vtable `0x2253e08`,
type `.?AVUUIC_Common_MessageLog@ui@app@@`). It moves the shared `CursorManager` whose pointer is at its `+0x30`, so
recording the log as that manager's owner from its update names the cursor's screen. Each row's words are
`fw::font::StringData` objects, whose layout is `[NEEDS VERIFICATION]`.

## Evidence

- **The cursor.** In the row build `0xf6da40` and the per-frame input `0xf6d550`, the cursor index read and written is
  `[[this+0x30]+0x60]`, the `CursorManager` field the menu cursor hook reads as the index
  ([`difficulty-select-shared-cursor.md`](difficulty-select-shared-cursor.md)). `+0x28` is a wrapper whose `+8` is the
  same pointer: its helpers (`0xe89d40`, `0xe89d50`, `0xe89da0`) each pass `[arg+8]` on. That this manager is the one
  #685's session logged (class `0x22045e8`, count 29, index 28 on opening) is `[NEEDS VERIFICATION]`; the next Debug
  session's `cursor` records should read `screen=UUIC_Common_MessageLog@ui@app`.
- **The entries.** The row build walks entries 29 down to 0 through the getter `0x14a644840` (in `.text1`) to find the
  newest whose byte at `+0x188` is set, storing one more than its number at `this+0x58`. Each entry starts with a
  `shared_ptr` and, at `+0x10`, a list of up to 15 `shared_ptr<fw::font::StringData>` (`0x18` apart), has a
  `std::string` at `+0x190` and an `int` at `+0x1b0`.
- **The rows.** It fills a `fw::StaticArray<app::ui::MessageLogLayout::ListInfoItem,30>` (rows of `0x120` bytes, each
  starting with a `StaticArray<shared_ptr<fw::font::StringData>,15>`, then a `shared_ptr` at `+0x100` with a flag at
  `+0x110`, an `int` at `+0x118` and bytes at `+0x11c`) and hands it to `0xad68c0` on `[this+0x20]`, which copies it to
  that object's `+0x28e20`. Which `StringData` holds the speaker and which the line, and how its characters are
  stored, are `[NEEDS VERIFICATION]`.
- **Live.** In the maintainer's Debug session of 2026-11-21 (Steam build 18330018), opening the log compiled the two
  scene files its lines came from (`e03_003_002.msg` and `e03_003_001.msg`) and logged no `resolve`, `request` or line
  record, then a cursor input at index 28 of 29 (#685's comment). So the words don't pass a hooked text function.

## Steps

Ghidra 12.2 on the shared project (`scripts/dev/ghidra.ps1`,
[query the Ghidra project](../contributors/how-to/query-the-ghidra-project.md)): `vtable app::ui::UUIC_Common_MessageLog`,
then `decompile` of `0xf6e300`, `0xf6da40`, `0xf6d550`, `0xad68c0`, `0xe89d40`, `0xe89d50` and `0xe89da0`. The class
name came from a grep for `MessageLog` over the project's earlier answers. The sections are from the exe's section
table. `scripts/check.sh signatures` finds the update's signature (`MessageLogUpdate`) once, at rva `0xf6e300`.

Next session: a Debug build with `messageLog` on. Open the log and move through it. Each move logs a `menu messagelog`
record with the focused row's address and a hex dump of its first `StringData` and its `+0x100` one, to decode them
against the lines on screen.
