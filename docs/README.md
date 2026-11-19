# Docs

Docs are sorted by who reads them, then by what the reader needs, following [Diátaxis](https://diataxis.fr/):

| Folder | Reader |
|---|---|
| [`players/`](players/) | people installing and using the mods |
| [`contributors/`](contributors/) | people working on the code, the notes or the docs |

Inside each, one folder per type:

| Type | Answers | Example |
|---|---|---|
| `tutorials/` | "teach me" | a first install, start to finish |
| `how-to/` | "help me do this" | updating the mod after a game patch |
| `reference/` | "what exactly is it" | the settings file's keys |
| `explanation/` | "why is it like this" | how announcements reach the screen reader |

A folder exists only while it holds a page. A page belongs to one type. If it's two, split it and link the halves.

These folders hold project records, and stay where the checks and skills expect them:

- [`findings/`](findings/README.md): what we found out about the game, each note pinned to a game build.
- [`raw-sources/`](raw-sources/README.md): saved copies of the sources the notes cite.
- [`adr/`](adr/): decision records.
- [`agents/`](agents/): how the agent skills use this repo.
- [`coding-standards.md`](coding-standards.md): the rules reviews check.
