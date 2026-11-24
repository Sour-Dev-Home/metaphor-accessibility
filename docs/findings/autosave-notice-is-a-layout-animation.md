# The field autosave notice shows as `AutoSavingLayout` animation 748, which the Release layout hook already sees

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do
- **Sources:** none, see Steps; follows [`field-notices-and-map-travel-static.md`](field-notices-and-map-travel-static.md)
  and [`ui-component-open-points-static.md`](ui-component-open-points-static.md)
- **Functions:**
  - UUIC_Common_Saving::vf5 | 0xf8a6f0 | .ecode | Field | known only | The saving component's request handler: closes or opens its layout through the layout's slots 21 and 20.
  - AutoSavingLayout::vf20 | 0xd1e250 | .ecode | Field | known only | Opens the autosave notice through the shared start-an-animation wrapper `0xa6ffd0`.

For #543. The bottom-left autosave notice has no text fetch any hook sees, but it opens through the shared
start-an-animation (`LayoutAnimationPlay`, `0xa70010`), a Release hook since #820: every field autosave in four Debug
session logs is one `AutoSavingLayout@ui@app` record with animation 748, and a manual save is `SystemSavingLayout` 758
instead. So the cue needs no new hook, only a feature that asks `LayoutAnimations` for that class. The words come from
the game's own `System_Save_11` "Autosave" (serial 1300) or our own table.

## Evidence

- **Static.** `AutoSavingLayout`'s vtable (`0x21bd260`) overrides slot 20 with `0xd1e250`, which calls `0xa6ffd0` (the
  wrapper into `0xa70010`) on `layout + 0x28` with an animation number computed from a global, so the number isn't
  readable statically. `UUIC_Common_Saving::vf5` (`0xf8a6f0`) reads its request with `0xf484a0` (a has-value byte at
  `+0x40` and a type at `+0x38`, as in the [component note](ui-component-open-points-static.md)) and calls its layout's
  slot 21 (`+0xa8`) and then slot 20 (`+0xa0`). No text call is in either function.
- **Logs.** The maintainer's local Debug session logs (grep only, not committed), `ui-anim` records:

  | Session (folder) | Mod build | `AutoSavingLayout` 748 | `SystemSavingLayout` 758 |
  |---|---|---|---|
  | 2026-11-22 22.50.00 | `618cf84` | 13 | 2 |
  | 2026-11-23 00.17.35 | Debug, commit not recorded | 1 | 0 |
  | 2026-11-23 13.52.36 | `0c122d3` | 12 | 0 |
  | 2026-11-23 19.22.38 | `4d936b2` | 9 | 0 |

  Every `AutoSavingLayout` record in them has animation 748, one layout address per session. In the 2026-11-22
  13.05.43 log, the two field pickups (`resolve` serial 5143 at 735393 ms and 2767746 ms) are each followed 2.6 s later
  by one 748 (738042 ms, 2770404 ms), matching the maintainer's report on #543 that an autosave follows each pickup. On
  2026-11-23 13.52.36 the 748 at 3183727 ms is the one the live notes put just before Junah's corner line (#587).
- **No text.** Serial 1300 resolves only on the save and options screens ([#543's 2026-11-22
  comment](https://github.com/Sour-Dev-Home/metaphor-accessibility/issues/543)), never with a 748.
- The hook's repeat gate drops the same animation on the same layout within a second, so two autosaves less than a
  second apart would log once; in a feature, `LayoutAnimations.Played` isn't gated (it runs for each call of an asked
  class, `src/Metaphor.Hooks/UiComponentListeners.cs`).

## Steps

1. Ghidra 12.2 project of this build (`scripts/dev/ghidra.ps1 batch`): `vtable app::ui::AutoSavingLayout`, `vtable
   app::ui::UUIC_Common_Saving`, `decompile 0xd1e250`, `decompile 0xf8a6f0`, `decompile 0xf484a0`.
2. `grep -o "ui-anim class=[^ ]*"` over each session's `message-log.txt`, counted with `sort | uniq -c`; the pickup
   times by `grep "serial=5143 flags=8"` and the `ui-anim` records within 10 s after.

## Build plan

1. A field feature (`Metaphor.Features.Field`, its own setting per rule 18, on by default as the issue asks) registers
   an `ILayoutAnimationObserver` for `AutoSavingLayout@ui@app` with `LayoutAnimations.Observe`, as `BattleStartLine`
   does for its layout (`src/Metaphor.Features.Battle/BattleLayoutLines.cs`).
2. On animation 748 it queues "Autosaving" (or the text of serial 1300 from `MessageSerials`, which keeps
   `System_Message.msg`), never interrupting, so dialogue isn't cut (#543's acceptance). A short cue instead of words
   is the same trigger.
3. Ignore `SystemSavingLayout` 758: the manual save already speaks through its pop-up (#622).
4. Tests: a fake `LayoutAnimation("AutoSavingLayout@ui@app", 748)` speaks once and queues; another class or animation
   number says nothing. By ear: enter a new area and pick up an item; one announcement each.
