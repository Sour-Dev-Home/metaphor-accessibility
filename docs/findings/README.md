# Findings

One note per finding about the game or its engine: a file format, a struct layout, a function's behaviour. Each claim
cites a saved source in [`../raw-sources/`](../raw-sources/README.md) or a reproducible method, or is flagged
`[NEEDS VERIFICATION]`.

Each note's header, before its first `## ` heading, names the game build it was verified on by the executable's SHA-256
(see **Game build** in [`GLOSSARY.md`](../../GLOSSARY.md)):

```markdown
- **Game build:** <SHA-256 of the executable> (Steam build <id>, version <shown in game>)
```

`scripts/check.sh` fails a note without one.
