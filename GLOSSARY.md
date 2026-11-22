# Metaphor: ReFantazio modding

Reverse engineering and modding of Metaphor: ReFantazio on PC: the game's file formats and engine, and the mods and tools
built on them.

## Language

**Game build**:
One exact release of the game's PC executable, identified by the SHA-256 of that executable. The Steam build ID and the
version shown in the game are recorded alongside it as readable labels, never as the identifier.
_Avoid_: version, patch (when used to mean the identifier)

**Round trip**:
Reading a game file into our own model and writing it back out. A format counts as understood when the round trip
reproduces the original file byte for byte, unless that format has a written exemption.
_Avoid_: re-pack, rebuild (when used to mean this check)

**Announcement**:
A piece of text a feature asks the screen reader to speak, such as a menu item's name when it gets focus. Sounds that
carry meaning without words are sound cues, not announcements.
_Avoid_: message, speech, TTS (when used to mean the text)

**Sound cue**:
A short tone the mod plays to say what happened without words, such as an ally being hit (#636, ADR 0019). Each is
named in the cue catalogue (`Cue`) and explained for players in `docs/players/reference/sound-cues.md`.
_Avoid_: earcon, audio cue, sound effect (the game's own sounds)

**Held tone**:
A continuous tone a feature starts, changes in pitch, pan and volume, and silences, on its own tone channel
(`ToneChannel`), such as guidance toward a target (#183, #637, ADR 0019). Unlike a sound cue, it lasts until silenced.
_Avoid_: loop, beacon (when used to mean this tone)

**Transcript**:
The ordered record of every announcement in one run of the game, kept in dev builds so what was said can be checked
without listening. Each line is `<ms> ms <text>`, the milliseconds since the game started, the report log's clock.
_Avoid_: speech log

**Announcer**:
The one interface every feature hands its announcements to (ADR 0004). Behind it sit the speech backend and, in dev
builds, the transcript.
_Avoid_: speaker, TTS engine (when used to mean the interface)

**Text cleaner**:
The step that turns text as the game stores it into what an announcement says: it strips formatting tags, fills
placeholders with what the screen shows, and says known button icons as words.
_Avoid_: sanitizer, parser (when used to mean this step)

**Game content**:
Anything that comes from the game: its executable, DLLs, archives, extracted assets, and decompiled or disassembled
output. It never enters the repository or CI.
_Avoid_: game files, assets (when used to mean only part of this)

### Menus

**Screen**:
One menu view the player moves through with a cursor, such as Equipment or the title menu. A submenu opened from a
screen is a screen of its own.
_Avoid_: page, panel, window, menu (when used to mean one view)

**Screen class**:
The game's own type for a screen, which tells one screen from another. Not the cursor's type, which nearly every
screen shares.
_Avoid_: menu class, cursor class (when used to mean this)

**Focus**:
The item on a screen the cursor is on, with its position in the list.
_Avoid_: selection, highlight, hover

**Details key**:
The one key, the same on every screen, that reads the rest of the focused item's panel beyond what a focus move says.
_Avoid_: info key, read-more key

### Review and CI

**Security-review scope**:
The files whose changes must be reviewed by the `security-reviewer` agent before they are committed;
[AGENTS.md](AGENTS.md#claude-code-setup) names them. The list, by directory, lives in `scripts/ci/security-stamp.sh`.
_Avoid_: review list, protected files

**Stamp**:
The record that the `security-reviewer` agent found nothing critical in the staged security-review scope. It is a hash of
that scope, so any edit after the review voids it, and a commit uses it once.
_Avoid_: approval, sign-off, review token

**Review trailer**:
The `Security-Review` line a commit message carries to show its security-review scope was reviewed. CI accepts it when it
matches that commit's scope.
_Avoid_: review tag, review footer

**Clean merge**:
A merge from `main` whose second parent is already on `main` and whose every security-review scope file is the first
parent's where `main` left it unchanged, or the second's where the branch did (ADR 0012). It needs no stamp; a conflict
resolution or one side kept over the other's change is not clean.
_Avoid_: trivial merge, fast merge

**Carried review**:
The way a commit whose security-review scope is identical to each parent's needs no trailer of its own: its first
parent's review carries over to it (ADR 0012).
_Avoid_: pass-through, skip, inherited review

**Pass-through**:
A hook adapter that always calls the original game function and never changes what the game does. A hook that does
change it is written outside these adapters (ADR 0007).
_Avoid_: forwarder, wrapper

**Review gate**:
The CI check that a pull request from anyone but the maintainer, if it changes anything outside `docs/`, has been
reviewed and carries the `security-reviewed` label added by an admin after its latest push.
_Avoid_: approval check, label check

**Recorded pass**:
The note a passing `check.sh full` keeps, per clone, for one exact tree. A later `full` or `pre-push` on that same tree
reuses it instead of running the checks again. A merge's pass can rest on its parents' passes, naming them (ADR 0021).
_Avoid_: cache, check result

**Attestation**:
A record, signed by GitHub, of which repository, workflow and branch or tag built a release zip's exact bytes. A player
verifies it before installing.
_Avoid_: signature, checksum, provenance file

### Working and testing

**Lane**:
One agent session's assigned slice of work: a single issue, built on its own branch and handed back for review. Parallel
lanes touch different files.
_Avoid_: track, worker, thread

**Stand-in game**:
A small program that plays the game and the mod loader's launcher in the scripts' own tests, so they run with no game
installed. It is never game content.
_Avoid_: mock game, fake game, dummy game
