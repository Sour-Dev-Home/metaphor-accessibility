# Animated cutscenes draw their subtitles without the dialogue hooks, so chapter 1's are silent

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do (the Debug message log); and opening the shipped executable in a disassembler as-is, accepting that protected
  parts may be unreadable
- **Sources:** none, see Steps; builds on
  [`event-scene-lines-skip-msgrequest.md`](event-scene-lines-skip-msgrequest.md) and the scene list in
  [`chapter1-interaction-inventory-2026-11-20.md`](../raw-sources/chapter1-interaction-inventory-2026-11-20.md)
- **Functions:**
  - AnimeMovieSubtitleMessageLayout vtable | 0x1421bab20 | .rdata | Dialogue | known only | The animated cutscenes' subtitle layout; its own slots jump into `.text1`. | finding

For #659. The animated (anime) cutscenes show their subtitles in `app::ui::AnimeMovieSubtitleMessageLayout`, from an
event message file compiled before the movie, and request none of their lines through `MsgRequestByName`,
`MsgWindowShowLine` or `GetMessageBySerial`. So the mod says nothing for them. Event scenes drawn in a message window
speak. The lead for the silent ones: speak the subtitle file's lines in order, one per line the layout shows, timed by
its `ui-anim` record.

## Evidence

Four Debug message logs, 2026-11-22 and 2026-11-23, on this build:

| Scene | Subtitle file | What the log shows | Spoken |
|---|---|---|---|
| The opening movie at launch, before "PRESS ANY BUTTON" (the inventory's scene 1: Louis and the aged lord) | `e01_001_001.msg`, 25 lines, no `<VOICE>` tags | Compiled at 25–40 s in every session. One session let it play: 24 `ui-anim class=AnimeMovieSubtitleMessageLayout anim=1032` records from 35 s to 176 s, and no request, lookup or resolve of any `MES_e01_001_001` label | No: the transcript's next line is "PRESS ANY BUTTON" |
| The carriage movie after the difficulty choice (Gallica, the scout) | `e01_002_001.msg`, no `<VOICE>` tags | Compiled at 197 s; the movie ran to 258 s, with the skip question opened three times and one `anim=1032` at 237 s; no request of its labels | No: only the skip question, 207–257 s |
| Strohl's dungeon reveal ("That's a... human.") | `e03_004_001.msg` | Compiled at 550 s; one `anim=1032` at 575 s; no request of its labels | No |
| Gallica after the carriage movie | `e01_004_001.msg`, voiced | `MsgWindowShowLine` per line, with the window pointer set | Yes ("Gallica", as #57 says for a voiced line) |
| More's prologue | `e00_002_001.msg`, voiced | `MsgRequestByName` and `MsgWindowShowLine` per line | Yes |

- **Across all saved logs**, every `MES_e01_001_001`, `MES_e01_002_001` and `MES_e03_004_001` label appears only inside
  its file's `compile` record.
- **The layout's lines.** While the opening movie ran, two text panes (class `0x2205688`, one drawn 4 pixels off the
  other, a drop shadow, near the bottom of the screen) ramped their alpha from 0 to `0xff` as a line came in, and back
  to 0 after each `anim=1032`. So `anim=1032` is a line fading out: 24 for 25 lines, the last ending with the movie
  `[NEEDS VERIFICATION]`. The panes' text didn't decode (`text=elem+0xd8` gave bytes, not words).
- **The layout's code.** `vtable app::ui::AnimeMovieSubtitleMessageLayout` is at `0x21bab20`; its own slots 17, 36 and
  39 (`0xd09190`, `0xd0c770`, `0xd10ca0`) are each a single `JMP` into `.text1`, and slot 16 calls only a shared
  helper. Its own code is protected, so it wasn't read further (ADR 0010).
- **The inventory's other cutscenes** (scenes 15, 16, 43 and 48) aren't in these logs `[NEEDS VERIFICATION]`. By this
  pattern, an animated one is silent and one drawn in a message window speaks.

The lead uses only what the mod already observes: the compiled file (its lines, in order, by label) and the
`ui-anim` record, from the `LayoutAnimationPlay` hook the mod already installs (`.ecode`). Open: whether the layout ever skips or repeats a line, which file a
movie reads when several have compiled (the opening's compiles at launch), and how a line lines up with the movie's
voice `[NEEDS VERIFICATION]`.

## Steps

The mod's Debug build (`ui-anim`, `compile`, `request`, `resolve` and `pane` records in `message-log.txt`; the spoken
lines in `transcript.txt`), sessions of 2026-11-22 and 2026-11-23, local, never committed:

1. `grep -n "AnimeMovieSubtitleMessageLayout" message-log.txt`, and `grep -n "ms compile .*event/"` for the files.
2. For a scene's window, list every record that isn't a pane, input or script `register` line, with `awk` on the
   leading millisecond count; compare with `transcript.txt` over the same window.
3. `grep -h -o "MES_e01_00[0-9]_00[0-9]" *.txt | sort | uniq -c`, and the same for each label outside `compile` bodies.
4. Static: `pwsh scripts/dev/ghidra.ps1 vtable app::ui::AnimeMovieSubtitleMessageLayout`, then `callees` and `disasm`
   on its own slots. Nothing in `.text1`, `.xcode` or `.xtext` was read.
