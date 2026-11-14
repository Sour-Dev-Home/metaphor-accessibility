# Findings

One note per finding about the game or its engine: a file format, a struct layout, a function's behaviour. Claims follow
the [ground rules](../../AGENTS.md#ground-rules) (cite or flag, pin to a game build).

Each note's header, before its first `## ` heading, names the game build it was verified on by the executable's SHA-256
(see **Game build** in [`GLOSSARY.md`](../../GLOSSARY.md)):

```markdown
- **Game build:** <SHA-256 of the executable> (Steam build <id>, version <shown in game>)
```

`scripts/check.sh` fails a note without one.

## Template

Copy this into `docs/findings/<topic>.md`. The method names a row of the table in
[ADR 0002](../adr/0002-analysis-methods-under-denuvo.md), so a reviewer can check that the method was allowed.

```markdown
# <What was found, as a statement>

- **Game build:** <SHA-256 of the executable> (Steam build <id>, version <shown in game>)
- **Method:** <the ADR 0002 row it falls under>
- **Sources:** <files in docs/raw-sources/, or "none, see Steps">

<The claim in one or two sentences.>

## Evidence

<What shows it's true: a short quoted snippet, a round-trip result, a hook's output. Mark anything unchecked
[NEEDS VERIFICATION].>

## Steps

<How to reproduce it: the tool and its version, and what you did.>
```
