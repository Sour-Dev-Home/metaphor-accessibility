# Items reach the player through two banners: the event item-get window, and a field pickup banner no hook sees yet

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do
- **Sources:** none, see Steps; follows [`field-notices-and-map-travel-static.md`](field-notices-and-map-travel-static.md),
  [`ui-component-open-points-static.md`](ui-component-open-points-static.md) and
  [`item-skill-message-serials-static.md`](item-skill-message-serials-static.md)
- **Functions:**
  - EventItemGetOpen | 0xf560d0 | .ecode | Field | known only | Opens the item-get window: copies up to three item id and count pairs into the `ItemGetNoticeLayout` list.
  - ItemGetNoticeLayout::vf20 | 0xd212f0 | .ecode | Field | known only | Opens the item-get layout with animation 818 through the shared start-an-animation wrapper.
  - UUIC_Field_Reward::vf5 | 0xfcb860 | .ecode | Field | known only | The field reward component's request handler; hands the request on to an object a `.text1` thunk returns.

For #542. **Two routes, one of them buildable.** The script and post-battle item window (`UUIC_Common_EventItemGet`)
opens through `EventItemGetOpen` (`0xf560d0`, `.ecode`, today a development-only hook), and the item's name and
description are fetched by serial in the same millisecond. A field pickup, the issue's first map item, never calls it:
its name and description are fetched the same way, but no component open or layout animation goes with them. The
lead for that banner is the field reward stack (`UUIC_Field_Reward`, `FieldRewardLayout`), whose animations run in
`.text1` `[NEEDS VERIFICATION]`. Every timing below is from one to three samples.

| Route | Component and layout | Signal a Release build could see | Name |
|---|---|---|---|
| Event or post-battle item window | `UUIC_Common_EventItemGet`, `ItemGetNoticeLayout` (vtable `0x21be080`) | `EventItemGetOpen` (promote from `DevOnly`); `ItemGetNoticeLayout` animation 818 through `LayoutAnimationPlay` | `resolve` flags 8 in `Item_Message_1` (5143–5915), same ms as the open |
| Field pickup | Unknown; lead `UUIC_Field_Reward` (vtable `0x22595f8`) and `FieldRewardLayout` (`0x21a8600`) | None proven; `ActionGuideLayout` animation 1810 comes 13–16 ms before the name in all three samples | Same: flags 8 name, then its flags 7 description |

## Evidence

### The event item window

- `EventItemGetOpen` (`0xf560d0`) takes the component and a `StaticArray<UUIC_Common_EventItemGet::Arg::GetItem,3>`:
  a vtable at `+0x0`, a count at `+0x8`, and 8-byte entries of `int id, int count` reached through the array's slot 1.
  It skips id 0, copies each pair into a `StaticArray<ItemGetNotice::ListInfoItem,5>`, and hands the list to
  `0xd21f50` with the layout at component `+0x20`. `0xd21f50`'s only analysed caller is this function.
- `ItemGetNoticeLayout::vf20` (`0xd212f0`) calls `0xa6ffd0` with the constant animation `0x332` (818), so its open is a
  `LayoutAnimationPlay` call; its `vf21` (`0xd21520`) closes through the layout's own slots, not the shared one.
- **Log, 2026-11-22 13.05.43** (the maintainer's local Debug session log, grep only): the post-battle reward at
  1879062 ms is `ui-open class=UUIC_Common_EventItemGet`, then in the same and next millisecond `resolve serial=5600
  flags=8` ("Superior Enigmatic Accessory") and `serial=6372 flags=7` (its description); at 1880435 ms a pad confirm
  (UI sound 1), then 1880436 ms `ui-anim class=ItemGetNoticeLayout anim=818`. That is the only `EventItemGet` open in
  all twelve local logs, and the only 818. Whether 818 is the window opening late or a second notice after it is
  `[NEEDS VERIFICATION]`.

### The field pickup

- **Log, same session:** three pickups of a Medicinal Herb (735393, 2767746 and 2771354 ms) each log
  `resolve serial=5143 flags=8` then `serial=5916 flags=7` within a millisecond, a UI sound (51 at 2767747 ms), and
  13–16 ms earlier `ui-anim class=ActionGuideLayout anim=1810`. No `ui-open`, no item-get layout, and the hook
  `EventItemGetOpenListener` was installed (its `ui-open` fired later in the run). An `AutoSavingLayout` 748 follows
  2.6 s later ([autosave note](autosave-notice-is-a-layout-animation.md)), then the field HUD's layouts reopen.
- **The heuristic's reach.** Over five logs with `ui-anim` records (2026-11-22 13.05.43, 16.44.23, 22.50.00, 2026-11-23
  13.52.36, 19.22.38), 58 `ActionGuideLayout` 1810 records and 224 item names with flags 8: exactly the three pickups
  have a name within 50 ms after a 1810. Shops, the analysis screen and result lists fetch flags 8 names in bursts,
  many with descriptions, so "a flags 8 name with its description" alone isn't a pickup.
- **The reward stack.** `UUIC_Field_Reward` is the only other component with a `GetItem` argument type
  (`StaticArray<UUIC_Field_Reward::Arg::GetItem,10>` in the exe's type names). Its `vf5` (`0xfcb860`, `.ecode`) gets an
  object from a thunk into `.text1` (`0x43b26b0`) and calls that object's slot 5 with the request. Its layout family,
  `FieldRewardLayout` with `StackLine`, `Statck`, `Fix`, `QuestNotice` and `ClassLevel` under `layout::field::reward`,
  plays its animations through `0xa700a0` or the `.text1` function behind thunk `0xf0052a0`, not through
  `0xa70010`: `StackLine::vf20` (`0xc7d9d0`) and `QuestNotice::vf20` (`0xc7d8e0`) do. That fits the log having no
  record of it. That this stack draws the pickup banner is `[NEEDS VERIFICATION]`.
- The script functions `ItemGet` (`0x90b300`) and `ItemGetTreasure` (`0x90b3c0`) are thunks into `.text1`
  ([earlier note](field-notices-and-map-travel-static.md)); they stay off-limits (ADR 0010).

## Steps

1. Ghidra 12.2 project of this build (`scripts/dev/ghidra.ps1 batch`): `vtable` of `UUIC_Common_EventItemGet`,
   `ItemGetNoticeLayout`, `layout::common::ItemGetNotice`, `UUIC_Field_Reward`, `FieldRewardLayout` and the
   `layout::field::reward` classes; `decompile` of `0xf560d0`, `0xd212f0`, `0xd21520`, `0xfcb860`, `0xc7d9d0`,
   `0xc7d8e0`, `0xa700a0`; `callers` of `0xd21f50`, `0xfcb860`, `GetMessageBySerial` (none from these layouts).
2. Class and type names: `grep -ao` over the exe for `Reward`, `ItemGet` and `GetItem`.
3. Logs: `grep` for `ui-open`, `ItemGetNoticeLayout`, and `resolve` serials 5143–5915 with flags 8; an awk pass pairing
   each flags 8 item name with the latest `ActionGuideLayout` 1810 within 50 ms.

## Live-session checklist

Debug build with `ui-text-panes` and the `ui-anim` log, F8 at each banner:

1. **Probe the reward stack.** Add a development-only observer on `UUIC_Field_Reward::vf5` (`0xfcb860`) that logs its
   request's has-value byte (`+0x40`), the type name behind `+0x38` (low two bits masked), and the first `0x40` bytes.
   Signature still to write.
2. **Pick up three different field items** (the first map item, a chest, a gather point). For each, note whether a
   `UUIC_Field_Reward` request logs, whether its bytes hold the item id (the `resolve` serial's file position names the
   item), and the `ActionGuideLayout` 1810 to name gap.
3. **Get one post-battle or event item window** and note when `ItemGetNoticeLayout` 818 plays against the window
   appearing and closing.
4. **Settles:** if step 2's request carries the items, the build observes `UUIC_Field_Reward::vf5` (Release, read-only)
   and speaks the item names from the serials fetched in the same frame. If not, the fallback is the 1810-then-name
   pairing above, checked against every pickup in the session. The event window needs only `EventItemGetOpen` made a
   Release observer that takes the next flags 8 `Item_Message_1` name within the same frame.
