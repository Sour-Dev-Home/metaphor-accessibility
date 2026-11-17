---
status: accepted
---

# Borrowed ideas and code are credited by name, repository, licence, commit and file

Decided by the maintainer on 2026-11-16. Any idea or code taken from another project is credited in two places: the
code comment where it is used, and `CREDITS.md`. Each credit gives the project's name, repository, licence, the commit
read and the file. Copied code keeps its licence notice and must be GPL-3.0-compatible (ground rule 4, ADR 0005).
Arcana (ChipsAhoiMcCoy) is named specifically by the maintainer. Credits for it landed in
[#130](https://github.com/Sour-Dev-Home/metaphor-refantazio/pull/130), from
`docs/raw-sources/arcana-research-credits-and-pace-2026-11-16.md`.

## Considered options

- **Credit only in `CREDITS.md`.** Someone reading the code never learns where an idea came from.
- **Credit only code that was copied.** Ideas matter too, and the line between them is hard to draw.

## Consequences

Reading another project for an idea means recording its commit and file at once. A reviewer checks that a borrowed
idea has both credits.
