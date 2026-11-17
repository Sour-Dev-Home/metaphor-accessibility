# Change how voiced lines are read

Choose whether the screen reader reads a line the actors speak aloud, so it doesn't talk over them. Lines with no voice
acting are always read in full.

## Steps

1. Find the mod's settings folder: `User\Mods\metaphor.accessibility` inside your Reloaded-II folder.
   `[NEEDS VERIFICATION]` The folder is checked in the game later.
2. In it, create or open the text file `settings.json`.
3. Put in it:

   ```json
   { "voicedLines": "auto" }
   ```

4. Replace `auto` with the mode you want (below), and save. The change applies at once, and the mod says
   "Voiced lines:" and the new mode. You don't need to restart the game.

## The modes

| Value | What is read for a voiced line |
|---|---|
| `auto` (default) | The speaker's name for English voice; the full line for Japanese voice. Until the mod can tell the voice language, the full line. |
| `nameOnly` | The speaker's name only. A voiced line with no speaker is silent. |
| `fullLine` | The speaker and the line. |
| `nothing` | Nothing. |

Capital letters don't matter.

## If something is wrong

If the file is missing, isn't valid JSON, or the value isn't one of the four, the mod uses `auto`. Other keys in the
file are ignored.
