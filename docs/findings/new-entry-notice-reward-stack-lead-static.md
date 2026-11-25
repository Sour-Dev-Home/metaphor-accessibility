# The "new entry" notices most likely belong to the field reward stack, whose animations no Release hook sees

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do
- **Sources:** none, see Steps; follows [`field-notices-and-map-travel-static.md`](field-notices-and-map-travel-static.md)
  and [`item-get-banner-two-routes.md`](item-get-banner-two-routes.md)
- **Functions:**
  - UUIC_Field_Reward::vf5 | 0xfcb860 | .ecode | Field | known only | The field reward component's request handler; hands the request on to an object a `.text1` thunk returns. | finding
  - QuestNotice::vf20 | 0xc7d8e0 | .ecode | Field | known only | Opens a reward-stack quest notice with animation `0xc2b` through `.text1`. | finding

For #544. **Not found, with a lead.** The left-side notice that appears and vanishes ("New: Memorandum", and the
small box after a pickup) left no record in any hook: no `resolve`, no `ui-anim`, no text pane. The best candidate is
the field reward stack, `FieldRewardLayout` and its `layout::field::reward` parts (`StackLine`, `Statck`, `Fix`,
`QuestNotice`, `ClassLevel`), fed by `UUIC_Field_Reward`: its parts start their animations through `.text1`, which
would explain the silence. The Memorandum label `System_Memorandum_GET` still has no reader in analysed code. What
settles it is one Debug probe in a session.

## Evidence

- **What the logs hold.** In the maintainer's local logs (grep only), serial 1825 `System_Memorandum_GET` never
  resolves; Memorandum serials 1827–1832 resolve only when the camp menu's Memorandum list opens (its question "Which
  entry?", serial 1832, three times in the 2026-11-21 22.50.55 log and once in 2026-11-23 19.22.38). No `ui-anim` record names `FieldRewardLayout`, a `reward` part, `QuestNoticeLayout`
  or `RankupNoticeLayout` in any of the five logs with `ui-anim` records. No session log with `ui-anim` records has a
  time mark at a Memorandum notice, so its moment can't be matched to other records yet.
- **The reward stack plays outside the shared animation.** `StackLine::vf20` (`0xc7d9d0`) picks an animation by a kind
  at `+0x6bf8` (`0xc1b` or `0xc23`, then `0xc27` through `0xa700a0`) and starts it through thunk `0xf0052a0`, in
  `.text1`; `QuestNotice::vf20` (`0xc7d8e0`) starts `0xc2b` the same way. `0xa700a0` itself calls that thunk. None
  of them reaches `LayoutAnimationPlay` (`0xa70010`). `FieldRewardLayout::vf20` (`0xc7d4d0`) opens its parts at
  `+0x30`, `+0x22580` and `+0x2c350` and a layout looked up by id `0xc4`.
- **The component's request.** `UUIC_Field_Reward::vf5` (`0xfcb860`) is a short `.ecode` function: it gets an object
  from thunk `0x43b26b0` (`.text1`) and calls that object's slot 5 with the request, unread. A Release observer at
  `0xfcb860`'s entry would see every request; what a request holds is unknown.
- **Other notice layouts.** `QuestNoticeLayout` (`0x21bf748`) and `RankupNoticeLayout` (`0x21bdf30`) don't override
  slot 21 and never appear in the `ui-anim` logs; `TitleAutoSaveNoticeLayout` and `NetworkAccessNoticeLayout` are
  named in the exe but are title and online notices, not checked here.
- `app::dat::Memorandom` (vtable `0x17e7190`) is the Memorandum data's singleton; its slots weren't followed.

## Steps

1. Ghidra 12.2 project of this build (`scripts/dev/ghidra.ps1 batch`): `vtable` of `UUIC_Field_Reward`,
   `FieldRewardLayout`, each `layout::field::reward` class, `QuestNoticeLayout`, `RankupNoticeLayout`,
   `app::dat::Memorandom`; `decompile` of `0xfcb860`, `0xc7d4d0`, `0xc7d9d0`, `0xc7d8e0`, `0xa700a0`.
2. Class names: `grep -ao` over the exe for `Notice`, `Reward` and `Memorand`.
3. Logs: `grep` over every local `message-log.txt` for the layout names above and for `System_Memorandum`.

## Live-session checklist

Debug build, the `ui-anim` log on, F8 the moment each notice appears:

1. Add the development-only probe of [`item-get-banner-two-routes.md`](item-get-banner-two-routes.md)'s checklist,
   step 1 (`UUIC_Field_Reward::vf5`: has-value byte, type name, first `0x40` bytes).
2. Start a new game and walk to the first desert Memorandum entry (own-capture inventory row 21); then pick up an item
   and wait for the small box after it. Note how long each shows.
3. **Settles:** a probe record at each F8 says the stack shows them; its type name and bytes say whether the request
   names the entry (a Memorandum id would index `Memorandum_Title.msg`, 273 titles, compiled at boot). Then the build is
   a Release read-only observer at `0xfcb860` plus a table of request kinds to words. No probe record means the notice
   comes from elsewhere, and the next lead is `app::dat::Memorandom`'s slots and who sets a new-entry flag.
