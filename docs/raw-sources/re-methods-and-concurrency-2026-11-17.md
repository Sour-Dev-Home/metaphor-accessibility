# Other function-level RE methods, and how RE work could run in parallel

- **Source:** web reading on 2026-11-17 of the primary sources linked inline (tool repositories, official Ghidra and
  Microsoft documentation, arXiv abstracts), plus `gh api` reads of `rirurin/opengfd` at `1677394`. Game facts come
  from the notes they cite; nothing new was observed in the game, which was not launched, opened or read for this note.
  Where a source couldn't be read, the claim says `[NEEDS VERIFICATION]`.
- **Saved:** 2026-11-17.
- **Game build:** none newly read. Game facts quoted from other notes apply to their build,
  `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

The method this compares against: spike ticket, static reading of a local headless Ghidra database (our signatures
imported, RTTI class recovery), a Debug-only opt-in listener hook (ADR 0010), then a live capture evening
(`.claude/skills/capture/SKILL.md`) that agents line up with the log and video. The capture is the serial step: one
person, one game session. Rule 5 is read through [ADR 0002](../adr/0002-analysis-methods-under-denuvo.md): our own mod
reading memory and hooking is allowed, the exe as-is in a disassembler is allowed, a debugger only "if it works with no
help", and "dumping or unpacking protected code" is not.

## Static

### Library identification: Function ID and BSim

- Function ID "computes a hash of its body and uses this as a key to look up matching functions"; Ghidra "ships with
  databases that search for statically linked libraries from Microsoft Visual Studio", and the Function ID plug-in
  "allows users to create and populate their own databases"
  ([FunctionID.html](https://github.com/NationalSecurityAgency/ghidra/blob/master/Ghidra/Features/FunctionID/src/main/help/help/topics/FunctionID/FunctionID.html)).
- The shipped databases were built from Visual Studio 1998 through 2015 libraries, Debug and Release
  ([building_fid.txt](https://github.com/NationalSecurityAgency/ghidra/blob/master/Ghidra/Features/FunctionID/data/building_fid.txt)).
  Which MSVC toolset built the game: `[NEEDS VERIFICATION]`. If newer than 2015, the shipped sets miss its CRT and STL,
  and a database built from the matching toolset's static libraries (our own install, not game content) would label them.
- BSim "is a Ghidra plugin for finding structurally similar functions in (potentially large) collections of binaries",
  based on the decompiler, matching "across compilers, architectures, and/or small changes to source code"
  ([BSim README](https://github.com/NationalSecurityAgency/ghidra/blob/master/GhidraDocs/GhidraClass/BSim/README.md)).
  Backends: a local H2 file for one user, or PostgreSQL or Elasticsearch servers that "support multiple users and allow
  multiple simultaneous connections"
  ([DatabaseConfiguration](https://github.com/NationalSecurityAgency/ghidra/blob/master/Ghidra/Features/BSim/src/main/help/help/topics/BSim/DatabaseConfiguration.html)).
- Open-source code the game is known to carry, so we can compile it ourselves and match it: Squirrel ("likely 3.2",
  [NutCracker-Metaphor](https://github.com/RyoTune/NutCracker-Metaphor), via `modding-tools-survey-2026-11-13.md`).
  Squirrel 3.2 is MIT-licensed and builds on Windows amd64 ([squirrel](https://github.com/albertodemichelis/squirrel)).
  The game's build options for it (`SQ64` per rirurin's `sqcrab`, `arcana-research-credits-and-pace-2026-11-16.md`)
  matter for a hash match; BSim's fuzzy match tolerates more. CRI middleware is closed: no source to build from.
- Payoff: library functions get names, so static reading and LLM passes skip them. Covers `.ecode` only where the bytes
  are readable as-is; `.text1` stays unread (ADR 0002).

### Carrying findings across game builds: diffing

- ghidriff (GPL-3.0) uses Ghidra headless to "find the _added_, _deleted_, and _modified_ functions of two arbitrary
  binaries", with a Version Tracking engine of several correlators; output is JSON and Markdown
  ([ghidriff](https://github.com/clearbluejar/ghidriff)).
- BinDiff (Apache-2.0) can "Port function names, comments and local names from one disassembly to the other", and
  "ships with support for IDA Pro, Binary Ninja and Ghidra" ([bindiff](https://github.com/google/bindiff)); the Ghidra
  side is the BinExport extension, with headless use covered ([binexport](https://github.com/google/binexport)).
- Diaphora (AGPL-3.0) ports names, comments and types, but is an IDA plugin only
  ([diaphora](https://github.com/joxeankoret/diaphora)). We don't use IDA: not a fit.
- Payoff: when a patch lands, names and the matched functions behind our 24+ byte signatures carry over in one run
  instead of a manual re-hunt. It compares the exe as-is (ADR 0002 row 3).

### LLM-assisted naming through Ghidra MCP servers

- GhidraMCP (Apache-2.0): an MCP server to "Decompile and analyze binaries", "Automatically rename methods and data",
  list classes, imports and exports; a headless mode imports and analyses "with no Ghidra GUI running", against Ghidra
  12.1.4 ([GhidraMCP](https://github.com/LaurieWired/GhidraMCP)). Compatibility with our Ghidra 12.2:
  `[NEEDS VERIFICATION]`.
- ReVa (Apache-2.0): "a variety of small tools" with schemas; decompilation output carries namespace and
  cross-reference context; it "can run in headless Ghidra mode without the GUI", with "ephemeral (session-scoped)"
  projects ([reverse-engineering-assistant](https://github.com/cyberkaida/reverse-engineering-assistant)).
- Evaluations: Shang et al. benchmark function name recovery and summarisation on decompiled code and conclude only
  that "existing LLMs can understand binary code to a certain extent" ([arXiv 2404.09836](https://arxiv.org/abs/2404.09836)).
  REFORGE (2026) argues the main obstacle to fair evaluation is ground-truth reliability; its yield of evaluable
  functions drops "from 87.2% to 65.9% across optimization levels" ([arXiv 2607.07738](https://arxiv.org/abs/2607.07738)).
  Neither gives an accuracy we could rely on for an optimised game exe.
- Payoff: fast first-pass triage of `.ecode` functions around RTTI vtables. Every LLM name stays a lead,
  `[NEEDS VERIFICATION]` under rule 1, until a capture or a cross-check confirms it.

### Type and class recovery

- Ghidra's `RecoverClassesFromRTTIScript` recovers class hierarchy, inheritance, constructors, destructors, vftables
  and class data types from RTTI; "Windows class recovery is more complete and tested than Gcc class recovery"
  ([script](https://github.com/NationalSecurityAgency/ghidra/blob/master/Ghidra/Features/Decompiler/ghidra_scripts/RecoverClassesFromRTTIScript.java)).
  We already use RTTI recovery.
- OOAnalyzer (CMU SEI Pharos): "only supports analysis of 32-bit x86 executables compiled by Microsoft Visual C++"
  ([pharos](https://github.com/cmu-sei/pharos)). Not usable on our x64 exe.

## Related games: the GFD engine family

- OpenGFD's README names "Atlus' GFD Engine, used in Persona 5 (Royal), Metaphor: Refantazio, Catherine Full Body"
  (via `modding-tools-survey-2026-11-13.md`). At `1677394` the repo has separate `buildscript/opengfd.BuildMetaphor` and
  `opengfd.BuildP5R`, per-build globals (`opengfd-globals/src/globals_xrd744.rs`, `globals_xrd759.rs`), engine modules
  (`opengfd/src/anim`, `graphics`, `object`, `kernel`, ...) and an inspector with `scene_graph.rs` and `scheduler.rs`
  panels (read with `gh api repos/rirurin/opengfd/git/trees/HEAD?recursive=1`). Its modules are engine layers; no
  battle or menu module by name. Which `xrd` number is Metaphor: `[NEEDS VERIFICATION]`. The scene graph is a
  candidate source for #178 (nearby things), unread.
- rirurin's `p5-exe-structures-docs` (P5R) and `ghidra-scripts` (a Switch 2 vtable labeller and an old
  `CreateStructuresForGFD.java`) are already recorded in `arcana-research-credits-and-pace-2026-11-16.md`; whether they apply to the PC
  exe is `[NEEDS VERIFICATION]` there.
- Metaphor-specific community tools are surveyed in `modding-tools-survey-2026-11-13.md`; nothing new here.
- Persona 3 Reload's engine: not checked `[NEEDS VERIFICATION]`; `riri.flowscriptframework` targets it only.

## Dynamic, without a human playing

### Scripted input

- The game reads the pad itself through `PadRead` in `.ecode`, which calls `XInputGetState` and builds a 16-bit mask
  (`pad-and-keyboard-state-2026-11-16.md`, `input-route-2026-11-16.md`). `XInputGetState` fills an `XINPUT_STATE`
  for controller 0–3 ([Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/xinput/nf-xinput-xinputgetstate)).
- So a Debug-only hook on `PadRead` (or on the XInput import) can write a scripted mask frame by frame. That changes
  behaviour, so it falls under [ADR 0007](../adr/0007-behaviour-changing-hooks.md): written outside the pass-through
  adapters, its own security review. It is our mod hooking `.ecode`: ADR 0002 row 2, inside rule 5.
- Alternatives: `SendInput` synthesises keyboard and mouse events and "is subject to UIPI"
  ([Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)); it doesn't
  cover the pad. ViGEmBus, a virtual controller driver, is retired and archived (2023-11-02)
  ([ViGEmBus](https://github.com/nefarius/ViGEmBus)). The in-mod hook needs no driver and is frame-exact.
- Starts: the game's own save slots, one per scenario (title, field near a shop, a battle trigger). Where saves live and
  whether Steam Cloud syncs them: `[NEEDS VERIFICATION]`. No process snapshotting.

### Memory diffing

- Cheat Engine-style scanning reads memory from outside; its "Find out what writes to this address" is listed under
  Debugging in its help file and attaches the debugger
  ([help file copy](https://www.scribd.com/document/902861753/Cheat-Engine-Help-File-Cheat-Engine)); one write-up
  shows it uses hardware breakpoints ([lonami.dev](https://lonami.dev/blog/woce-5)). Scanning: allowed but redundant
  with our mod's reads. The debugger feature: ADR 0002 row 4 at best, and only "if it works with no help".
- In-rule equivalent: the listener snapshots known objects (`Package@btl`, party units, as in
  `battle-state-session6-2026-11-17.md`) before and after each scripted input and logs the changed offsets.

### Excluded under rule 5

- **Time Travel Debugging.** TTD "captures a trace of your process as it executes" and lets you "read memory" in replay
  ([TTD overview](https://learn.microsoft.com/en-us/windows-hardware/drivers/debuggercmds/time-travel-debugging-overview)).
  On this game that records the decrypted `.text1` code and its state: dumping protected code (ADR 0002 row 5). It also
  injects into the process like a debugger. Excluded.
- **DynamoRIO drcov and Frida Stalker.** drcov records executed basic blocks per process
  ([drcov](https://dynamorio.org/page_drcov.html)); Stalker copies each block into new memory, instruments the copy and
  runs that, and notes self-modifying code is "quite often used as an anti-debugging/anti-disassembly technique"
  ([Stalker](https://frida.re/docs/stalker/)). Whole-process instrumentation rewrites `.text1` execution and works
  against its integrity checks. Excluded. Frida's plain injection (`Interceptor` on `.ecode`) adds nothing over
  Reloaded-II ([modes](https://frida.re/docs/modes/)).
- **Intel PT.** WinIPT uses the inbox `ipt.sys` driver on Windows 10 1809+ ([winipt](https://github.com/ionescu007/winipt)),
  but decoding "needs to know the memory image for which Intel PT has been recorded"
  ([libipt howto](https://github.com/intel/libipt/blob/master/doc/howto_libipt.md)): for `.text1` that image is the
  protected code. Excluded for `.text1`; `.ecode`-only coverage is not worth the setup next to hook counters.
- **Debugger-based watchpoints** (Cheat Engine's write finder, x64dbg) with anti-debug hiding: ADR 0002 row 5. Excluded.

In-rule coverage instead: hit counters on many `.ecode` function entries through our own signature-pinned hooks, diffed
between two scripted runs that differ in one input. That is a design proposal, not tested.

## Concurrency

- **Shared Ghidra Server.** Per-user, per-repository access with read-only, write or admin rights, and local password,
  Kerberos, PKI or JAAS authentication ([svrREADME](https://github.com/NationalSecurityAgency/ghidra/blob/master/Ghidra/RuntimeScripts/server/svrREADME.md)).
  `analyzeHeadless` accepts "a Ghidra Server repository URL" (`ghidra://<server>[:<port>]/<repository_name>`) instead of
  a local project; `-readOnly` discards changes; it "may not run if the specified project is already open in Ghidra"
  ([analyzeHeadlessREADME](https://github.com/NationalSecurityAgency/ghidra/blob/master/Ghidra/RuntimeScripts/support/analyzeHeadlessREADME.md)).
  Running many headless instances against one server at once: not documented there, `[NEEDS VERIFICATION]`. Today
  each agent sharing one local project serialises on its lock; a server lets agents read in parallel and check names in.
  The repository holds game content (rule 4): local or private only, never public.
- **Split by subsystem.** RTTI namespaces already partition the work (`app::ui::ope`, `btl`, `fld*` in
  `ui-focus-candidates-2026-11-16.md`, `field-actors-2026-11-16.md`): one agent per namespace, each writing its own
  raw source, as the spike tickets already do.
- **Many short automated captures instead of one long human one.** With scripted input and save-slot starts, an evening
  becomes scenario runs that need no narration: the input script is the ground truth the video gives today. Runs on one
  machine stay serial (one game instance `[NEEDS VERIFICATION]`), but they run unattended and repeat identically, and
  the human session shrinks to what needs ears or eyes (speech checks, cutscenes).
- **Shared BSim database** (PostgreSQL or Elasticsearch, above) lets several agents query similarity at once.

## Ranked shortlist

| # | Method | Cost | Risk (rule 5 and other) | Unblocks |
|---|---|---|---|---|
| 1 | Scripted input through a Debug-only `PadRead` hook, save-slot starts, before/after object snapshots in the log | Medium: one behaviour-changing hook (ADR 0007, own security review), a scenario format, saves | Inside rule 5 (our hook, `.ecode`). Desync if timing drifts; Steam Input interplay untested | #287 turns and targets (scripted Attack/Guard/Pass with known inputs); #210 System submenu (deterministic menu walk); #178 partly (walk to known spots) |
| 2 | Function ID and BSim databases from our own builds of the matching MSVC libraries and Squirrel 3.2 | Low to medium: build libraries, populate `.fidb`/H2 | None: static, exe as-is. Toolset version unknown | Every static spike: library code labelled and skipped; Squirrel VM functions for script-driven menus (#210) |
| 3 | Shared Ghidra Server (private) with parallel `analyzeHeadless` scripts, work split by RTTI namespace | Medium: one-off server setup, scripts | None for rule 5; rule 4: server data is game content, keep private | Parallel static work on #287, #178, #210 at once instead of one lock-holder |
| 4 | ReVa or GhidraMCP headless for first-pass naming of `.ecode` functions around known vtables | Low | None for rule 5. Accuracy unproven on optimised code: every name is a lead until confirmed | Faster triage of `btl` (#287) and `fld*` (#178) classes |
| 5 | Build diffing with ghidriff (or BinDiff via BinExport) on each game patch | Low, per patch | None: compares exes as-is | Keeps signatures and names alive across patches; no ticket today, protects all of them |
