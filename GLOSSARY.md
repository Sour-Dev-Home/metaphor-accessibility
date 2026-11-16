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
carry meaning without words are audio cues, not announcements.
_Avoid_: message, speech, TTS (when used to mean the text)

**Transcript**:
The ordered record of every announcement in one run of the game, kept in dev builds so what was said can be checked
without listening.
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
