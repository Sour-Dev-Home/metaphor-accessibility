# Settings: which message labels the category list and its pages use, from game session 1 (#559)

- **Source:** game session 1's main-run `message-log.txt` (Debug mod build `e77eeab`, the run in
  [`game-session-1-2026-11-20.md`](game-session-1-2026-11-20.md)), re-read with grep and awk on the maintainer's
  machine. The log holds game text, so it stays local; only labels, class names, indices, times and the five category
  words below are kept.
- **Saved:** 2026-11-20.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. `grep -a " compile "` for the system message files.
2. The compiled source of `System_Message_PC.msg` as logged after its `compile` record: the `@System_category_name_PC_*`
   entries.
3. The `resolve` records just before the category list's first `cursor` record (146404 ms).
4. Every `cursor` record whose `screen=` names `ConfigTopChoicePc` or `ConfigMenuChoicePcT`, kept where the screen,
   `index` or `count` changes.

## What it shows

- **Both system files compile at start-up:** `21387 ms compile id=6 path=EN/message/system/System_Message.msg` and
  `21439 ms compile id=48 path=EN/message/system/System_Message_PC.msg`, long before Settings opens. So the mod's message
  library holds their labels whenever Settings is open.
- **The category labels.** `System_Message_PC.msg` has, in order, `System_category_name_PC_01` "System Settings",
  `_02` "Sound Settings", `_03` "Graphics Settings", `_04` "Display Settings", `_05` "Key Configuration" and `_06`
  "Controller Settings". Each body is the one line, then `<WAIT>`.
- **The list resolves the first five, in order, as it opens:** serials 11469 to 11473 at 146383 to 146384 ms (`resolve
  via=GetMessageBySerial ... flags=9`), then the list's first cursor at 146404 ms
  (`screen=?$ConfigTopChoicePc@VConfigTopChoicePcLayoutInit@ui@app@@@ope@ui@app ... count=5`). The sixth (11474) is not
  resolved. No other `System_Message_PC.msg` serial is resolved there, so no list heading is drawn from that file.
- **Which row opens which page.** Each page's first cursor follows the category list's last settled index:

  | List index | Page screen class (as logged) | Rows |
  |---|---|---|
  | 0 | `?$ConfigMenuChoicePcT@$0HG@@ope@ui@app` | 20 |
  | 1 | `?$ConfigMenuChoicePcT@$0HH@@ope@ui@app` | 6 |
  | 2 | `?$ConfigMenuChoicePcT@$0HI@@ope@ui@app` | 7 |
  | 3 | `?$ConfigMenuChoicePcT@$0HJ@@ope@ui@app` | 4 |

  Times: index 0 then `$0HG` at 147740 ms and again at 373565 ms; index 1 then `$0HH` at 472242 ms; index 2 then `$0HI`
  at 149440 ms; index 3 then `$0HJ` at 153076 ms. Index 4 (565466 ms) opens `ConfigMenuChoiceKeyConfig@ope@ui@app`
  (565839 ms).
- **Back on the list, nothing is logged until the cursor moves.** The list's manager `0x3ca2b040` settled at index 0
  (147323 ms) before System opened (147740 ms); after backing out, its next record is a move to index 1 at 148601 ms.
- **The in-play list** has its own class, `?$ConfigTopChoicePc@VConfigTopChoicePcLayout@ui@app@@@ope@ui@app`, five rows
  (`tests/fixtures/sessions/title-file-during-play.txt`, 75404 ms).
