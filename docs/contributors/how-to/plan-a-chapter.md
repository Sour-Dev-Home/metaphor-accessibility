# Plan a chapter

The routine for one chapter under [ADR 0016](../../adr/0016-release-by-chapter.md): learn what the chapter holds, prepare,
play it with the inspector, build only what it needs, and release. The chapter list and its systems come from
[the whole-game map](../../raw-sources/metaphor-whole-game-map-2026-11-20.md).

1. **List the systems and screens.** From the whole-game map, write the chapter's dungeons, systems, menus and missable
   items into its milestone's tracking issue, each linked to its existing issue. A system with no issue gets one.
2. **Scrub a public video of just that chapter.** Find a public playthrough of only this chapter and skim it for UI and
   interaction types: new menus, timed choices, minigames, spatial puzzles. Take notes only; capture and commit nothing
   (AGENTS.md rule 4). Save the notes as a raw source that cites each video's URL, channel and date
   ([write a finding or save a raw source](write-a-finding.md)); mark what you couldn't tell
   `[NEEDS VERIFICATION]`. The `/watch` skill reads a video locally. The chapter 1 example is
   [the pilot interaction inventory](../../raw-sources/chapter1-interaction-inventory-2026-11-20.md).
3. **Static prep for the unknowns.** For each screen or state nothing yet locates, run a Ghidra pass
   ([query the Ghidra project](query-the-ghidra-project.md)) before anyone plays, so the session has inspector
   commands ready.
4. **Game sessions along the play route.** Play the chapter's route in the order the game does, with inspector batch
   files per game state ([run a game session](run-a-game-session.md),
   [inspect the running game](inspect-the-running-game.md)). Pin findings to the game build
   ([write a finding](write-a-finding.md)).
5. **Build only as deep as the chapter needs.** A system the chapter doesn't reach stays as an issue; build the minimum
   that makes this chapter's screens speak and its actions possible.
6. **Done means played through.** Play the chapter start to finish with a screen reader (ideally a blind tester) and
   fix every blocker. Anything left goes to the next chapter's list as an issue.
7. **Release.** Add a CHANGELOG entry and follow [cut a release](cut-a-release.md).
