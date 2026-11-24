# Read game data for findings

Read a file out of the game's own CPK archive (such as `base.cpk`) so a [finding](write-a-finding.md) can describe it.
The tool is `tools/Metaphor.CpkExtract`, on `Metaphor.Core`'s `CpkArchive` reader. It is allowed by
[ADR 0002](../../adr/0002-analysis-methods-under-denuvo.md): offline, read-only, no protection touched.

**Extracted files are game content ([rule 4](../../../AGENTS.md#ground-rules)). They stay in the git-ignored
`extracted/` folder at the repository root and are never committed.** The checks block anything under `extracted/`.
A short quoted snippet in a note is fine; a file or a dump is not.

## Before you start

Set `METAPHOR_GAME_EXE` to the game exe, as for the other dev scripts. The archive is named by file name only
(`base.cpk`) and is found beside that exe, so you never type or commit an absolute path.

## List what an archive holds

```sh
dotnet run --project tools/Metaphor.CpkExtract -c Release -v q -- list base.cpk
dotnet run --project tools/Metaphor.CpkExtract -c Release -v q -- list base.cpk navi
```

One line per file: its stored size in bytes, a tab, its `DIR/NAME` path. The optional filter keeps paths containing it,
ignoring case. A compressed file (`CRILAYLA`) is listed and marked; the reader doesn't unpack it.

## Extract files

```sh
dotnet run --project tools/Metaphor.CpkExtract -c Release -v q -- extract base.cpk COMMON/init/data/MapData.bin --out extracted/mapdata
```

Each file lands under `--out` at its archive path (`extracted/mapdata/COMMON/init/data/MapData.bin`). Run it from inside
the repository. A file already there is left alone and the run refused; add `--force` to overwrite it.

## What it refuses

- An `--out` that isn't inside `extracted/`: `..`, another folder, another drive, the repository root.
- A symlink or junction on the way to the output, so a link can't lead out of `extracted/`.
- An archive path that isn't plain relative `DIR/NAME` (`..`, a drive, a `:` stream, an empty part), names a Windows
  device (`NUL`, `CON`, `COM1`, …) or holds a character Windows can't put in a name (`<>"|?*`, a control character).
- An archive named with a path, or anything but `.cpk`.

Refusals exit 2 before anything is opened or written. The archive is only ever opened for reading, and the tool uses no
network. Files already in `extracted/` at the same path are overwritten.

## Record how you got it

Cite the tool, the game build and the command in the finding's **Steps** ([write a finding](write-a-finding.md)).
