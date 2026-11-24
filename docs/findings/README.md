# Findings

One note per finding about the game or its engine: a file format, a struct layout, a function's behaviour. Claims follow
the [ground rules](../../AGENTS.md#ground-rules) (cite or flag, pin to a game build).

Each note's header, the part before its first second-level heading, names the game build it was verified on by the
executable's SHA-256 (see **Game build** in [`GLOSSARY.md`](../../GLOSSARY.md)):

```markdown
- **Game build:** <SHA-256 of the executable> (Steam build <id>, version <shown in game>)
```

`scripts/check.sh` fails a note without one.

To read a file out of the game's archives for a finding, use the committed tool, not a throwaway program:
[read game data for findings](../contributors/how-to/read-game-data-for-findings.md).

## Template

Copy this into `docs/findings/<topic>.md`. The method names a row of the table in
[ADR 0002](../adr/0002-analysis-methods-under-denuvo.md), so a reviewer can check that the method was allowed.

```markdown
# <What was found, as a statement>

- **Game build:** <SHA-256 of the executable> (Steam build <id>, version <shown in game>)
- **Method:** <the ADR 0002 row it falls under>
- **Sources:** <files in docs/raw-sources/, or "none, see Steps">
- **Functions:** (or none, see below)
  - <name> | <address> | <section> | <area> | <use> | <one-line summary>

<The claim in one or two sentences.>

## Evidence

<What shows it's true: a short quoted snippet, a round-trip result, a hook's output. Mark anything unchecked
[NEEDS VERIFICATION].>

## Steps

<How to reproduce it: the tool and its version, and what you did.>
```

## The `functions:` block

Every finding has the line. A finding that pins functions or globals lists them in its header, one entry per line, so a
generator can collect them into a local known-functions reference ([#716](https://github.com/Sour-Dev-Home/metaphor-accessibility/issues/716)).
A finding that pins no function or global writes `- **Functions:** none`, so the generator can tell it from one not done
yet. Each entry has six fields, separated by ` | `:

- **name:** the function or global's name, ours or the game's own.
- **address:** its address on the game build in the header.
- **section:** the executable section it lives in, such as `.text` or `.ecode`.
- **area:** one of Dialogue, Menus, Settings, Field, Battle, Speech, Install and Release, Messages and Text.
- **use:** `hooked` (the mod patches it), `read` (the mod reads it) or `known only` (understood, not used).
- **summary:** one line on what it does. No decompiled code (rule 4).

Example, with a made-up function:

```markdown
- **Functions:**
  - ExampleOpenPanel | 0x140123456 | .text | Menus | known only | Opens the example panel and returns its handle.
```
