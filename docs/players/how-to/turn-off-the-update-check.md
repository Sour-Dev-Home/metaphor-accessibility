# Turn off the update check

Each time the game starts, the mod asks GitHub once whether a newer release of the mod exists. If one does, the title
screen says "A newer version of the mod is available:" and the version, once, after the title's own speech. If it
doesn't, or you're offline, or GitHub doesn't answer within 5 seconds, the mod says nothing. `[NEEDS VERIFICATION by ear]`

The request goes only to GitHub, for this mod's releases. It sends nothing about you or your computer: no account,
no identifier and no usage data. GitHub still sees your IP address, as for any web page. Why and what exactly:
[ADR 0018](../../adr/0018-one-update-check-per-launch.md).

## Steps

1. Find the mod's settings folder: `User\Mods\metaphor.accessibility` inside your Reloaded-II folder.
   `[NEEDS VERIFICATION]` The folder is checked in the game later.
2. In it, create or open the text file `settings.json`.
3. Add `"checkForUpdates": false`. With the voiced-lines setting too, the whole file reads:

   ```json
   { "voicedLines": "auto", "checkForUpdates": false }
   ```

4. Save. The mod says "Update check: off." From the next game start it makes no request. To turn it on again, change
   `false` to `true`, or remove the setting.

## If something is wrong

The value must be `true` or `false` with no quotes. Anything else, or a file that isn't valid JSON, leaves the check
on.
