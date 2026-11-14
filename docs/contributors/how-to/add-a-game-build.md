# Add a game build

When the game updates, add the new **game build** (see [`GLOSSARY.md`](../../../GLOSSARY.md)) to
[`src/Metaphor.Core/known-game-builds.json`](../../../src/Metaphor.Core/known-game-builds.json). That file is the only
change; a hook can name the build once it has been checked on it.

1. Hash the executable. In Git Bash, from the game's install folder:

   ```sh
   sha256sum METAPHOR.exe
   ```

2. Find Steam's build ID: the `buildid` value in `steamapps/appmanifest_2679460.acf` in the Steam library that holds the
   game.
3. Note the version the game shows, if you can see it. Leave `gameVersion` out when you can't.
4. Add an entry to the list:

   ```json
   {
     "sha256": "<the hash, lowercase>",
     "steamBuildId": <buildid>,
     "gameVersion": "<version shown in game>",
     "evidence": "<date>: <tool and version> of METAPHOR.exe, <size> bytes; buildid from appmanifest_2679460.acf"
   }
   ```

5. Run `bash scripts/check.sh full`. `KnownGameBuildsTests` checks the entry.

A hook supports a build only after someone has checked it on that build. Add the hash to the hook's
`[GameHook(...)]` attribute then, not when the build is added here
([architecture rule 6](../../coding-standards.md#architecture-rules)).
