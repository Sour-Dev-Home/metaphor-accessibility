# Turn on the dialogue window hooks

The dialogue controls (#58: repeat the last line, turn the reader off or on) work without any hooks in the exe's
`.text1` section, which stay opt-in in every build (maintainer, 2026-11-16). The two hooks that tell them when the
dialogue window opens and closes, `DialogueWindowOpenHook` on `MsgOpenBody` and `DialogueWindowCloseHook` on
`MsgCloseBody` ([`DialogueWindowHooks.cs`](../../../src/Metaphor.Hooks/DialogueWindowHooks.cs)), are both in `.text1`
(`docs/raw-sources/msg-window-functions-2026-11-16.md`).

| | Without the file (default) | With `opt-in-dialogue-window.txt` |
|---|---|---|
| Hooks in `.text1` | none | the two window hooks |
| When R, T, X and LB + RB act | at any time | only while a dialogue window is open |
| Does the game see them | always | not while a dialogue window is open |

## Turn them on

1. Deploy the mod as usual ([deploy a dev build](deploy-a-dev-build.md)); a deploy replaces the mod's folder.
2. Create an empty file named `opt-in-dialogue-window.txt` in the deployed mod's folder (`<mods>\metaphor.accessibility`).
   Only its presence counts. It works in a Release build too, not only a Debug one.
3. Start the game from Reloaded-II. Both hooks still go through the build check and signature scan, and count in
   `hooks n/m`.

Without the file, Reloaded-II's log says `hook DialogueWindowOpenHook skipped: opt-in, and opt-in-dialogue-window.txt
isn't in the mod's folder` (and the same for the close hook). To turn them off, delete the file and restart the game.

A route that doesn't hook `.text1`, reading a window flag from memory, is being researched separately and may replace
these hooks.
