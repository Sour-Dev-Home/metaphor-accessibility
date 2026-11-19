# Write a finding

A finding is one note about the game or its engine (a format, a struct layout, a function's behaviour) in
`docs/findings/`. It cites a raw source saved in `docs/raw-sources/`, or says how to reproduce it. The rules behind
this are [ground rules 1 to 3](../../../AGENTS.md#ground-rules); this page is the procedure.

## Save a raw source

A raw source is a saved copy of what a note relies on: a community write-up, a tool's documentation, or a capture of
how the game behaves. It is never edited afterwards; a newer version is a new file.

1. Create `docs/raw-sources/<topic>-<YYYY-MM-DD>.md`.
2. Start it with the header from [`raw-sources/README.md`](../../raw-sources/README.md), each as a
   `- **Label:** value` line before the first `##` heading:

   ```markdown
   - **Source:** <a URL, or the tool, its version and the steps>
   - **Saved:** <YYYY-MM-DD>
   - **Game build:** <the executable's SHA-256, or "none" if it doesn't apply>
   ```

3. Paste only what you need, with no game text, no local path and no personal information
   ([rules 4 and 6](../../../AGENTS.md#ground-rules)). A short quoted snippet to explain a point is fine; a dump is not.
4. Borrowed from another project? Credit it ([rule 7](../../../AGENTS.md#ground-rules)).

The checks fail a raw source with no `Source` line, no `Saved` line with a date, or no `Game build` line.

## Write the finding

1. Copy the [template in `findings/README.md`](../../findings/README.md#template) to `docs/findings/<topic>.md`.
2. Fill in the header: the **Game build** as the executable's full 64-character SHA-256 (Steam build and version after
   it), the **Method** as the row of [ADR 0002](../../adr/0002-analysis-methods-under-denuvo.md) that allowed it, and
   the **Sources**, the raw-source files.
   Get the hash from the [known-builds list](../../../src/Metaphor.Core/known-game-builds.json) or the output of
   `bash scripts/check.sh signatures`. The checks fail a finding whose header has no such SHA-256.
3. State the claim in one or two sentences, then the evidence and the steps to reproduce it.
4. **Cite or flag.** Every claim about the game cites a raw source or a reproducible note (tool, version, build,
   steps). Anything you haven't checked is marked `[NEEDS VERIFICATION]`. Don't fill in an offset or a format from
   memory.
5. Commit and push as usual; the pre-commit hook checks the headers and, with Markdown staged, the links
   ([checks](../reference/checks.md)).

A finding that a hook depends on also names its evidence in the hook's signature entry
([check hook signatures](check-hook-signatures.md#add-a-signature)).
