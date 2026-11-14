# metaphor-refantazio

Modding work on Metaphor: ReFantazio (PC). The game runs on a proprietary engine, so most of this project is reverse
engineering: working out file formats, engine structures and functions, then writing mods and tools on top of them.
This is a public repository.

## Ground rules

1. **Cite or flag.** Every claim about the game or its engine (a file format, a struct layout, an offset, a function's
   behaviour, a signature) cites its evidence:
   - a saved source under `docs/raw-sources/`, such as a community write-up or a tool's documentation; or
   - a reproducible note naming the tool, its version, the game build and the steps.

   If neither exists, write `[NEEDS VERIFICATION]`. Never fill in engine details from memory: guessed offsets and formats
   look plausible and waste days.
2. **Pin everything to a game build.** Offsets, signatures and struct layouts change between patches. Every note and
   every hook names the game build it was verified on. Code checks the build or the signature before it patches, and
   does nothing if it doesn't match.
3. **Use what exists first.** Check the community's existing loaders, formats and tools before writing new ones. Record
   what was checked in `docs/raw-sources/`.
4. **Never commit game content.** That covers executables, DLLs, archives, extracted assets, decompiled output and
   disassembler databases. They live in the git-ignored folders listed in `.gitignore` (`game/`, `extracted/`,
   `dumps/`, and so on). A short snippet quoted in a note to explain a finding is fine. Releases ship only our code and
   our data.
5. **Single-player modding only.** Don't circumvent DRM or anti-tamper protection, and don't touch online features or
   other players. If a protection blocks a technique, stop, note it, and look for another approach.
6. **No personal information or local paths in committed files.** That means no real name, email or phone number, and no
   absolute path like `C:\Users\...`. Use paths relative to the repository or environment variables. The private
   patterns to scan for live in `.claude/pii-patterns.txt`, which is git-ignored (see `.claude/pii-patterns.example.txt`).

## Layout

- `docs/raw-sources/`: saved copies of the sources the notes rely on. Add to it; never edit what's there.
- `docs/agents/`: how the agent skills find issues, triage labels and domain docs (see `## Agent skills` below).
- `GLOSSARY.md` and `docs/adr/`: domain glossary and decision records, created by `/domain-modeling` when first needed.

## Claude Code setup

This project runs with its own config directory, so the owner's other projects' settings, agents and plugins don't load
here. Start it with `.\claude.ps1` from the repository root, not `claude`. The only plugin is Matt Pocock's skills
(`mattpocock-skills`). The one custom agent is `security-reviewer` (`.claude/agents/`): run it before a release, and before
any commit that touches hooking, injection, loading, install or update code.

## Agent skills

### Issue tracker

Issues live in GitHub Issues on `Sour-Dev-Home/metaphor-refantazio`, managed with the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Uses the five default triage labels: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
