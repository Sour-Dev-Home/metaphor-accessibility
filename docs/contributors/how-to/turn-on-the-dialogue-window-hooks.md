# Turn on the dialogue window hooks

The dialogue controls (#58: repeat the last line, turn the reader off or on) know when a dialogue window is open by
reading the window's message pointer, read-only and with no hook of its own
([`DialogueWindowPointer.cs`](../../../src/Metaphor.Hooks/DialogueWindowPointer.cs), #232). The two `.text1` hooks
that once told them, `DialogueWindowOpenHook` on `MsgOpenBody` and `DialogueWindowCloseHook` on `MsgCloseBody`
([`DialogueWindowHooks.cs`](../../../src/Metaphor.Hooks/DialogueWindowHooks.cs)), follow message handles rather than
the window (`docs/raw-sources/dialogue-window-flag-session5-2026-11-17.md`). They are kept as Debug evidence only: they
log `msgwindow-hook open` and `close` beside the pointer's `msgwindow` records, and change nothing.

| | Without the file (default) | With `opt-in-dialogue-window.txt` |
|---|---|---|
| Hooks in `.text1` | none | the two window hooks, in a Debug build only |
| When R, T, X and LB + RB act | only while a dialogue window is open | the same |

## Turn them on

1. Deploy a Debug build ([deploy a dev build](deploy-a-dev-build.md)); a deploy replaces the mod's folder.
2. Create an empty file named `opt-in-dialogue-window.txt` in the deployed mod's folder (`<mods>\metaphor.accessibility`).
   Only its presence counts. A Release build ignores it.
3. Start the game from Reloaded-II. Both hooks still go through the build check and signature scan, and count in
   `hooks n/m`.

Without the file, Reloaded-II's log says `hook DialogueWindowOpenHook skipped: opt-in, and opt-in-dialogue-window.txt
isn't in the mod's folder` (and the same for the close hook). To turn them off, delete the file and restart the game.
