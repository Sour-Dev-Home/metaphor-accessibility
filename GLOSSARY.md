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

**Game content**:
Anything that comes from the game: its executable, DLLs, archives, extracted assets, and decompiled or disassembled
output. It never enters the repository or CI.
_Avoid_: game files, assets (when used to mean only part of this)
