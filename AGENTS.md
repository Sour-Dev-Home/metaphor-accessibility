# metaphor-accessibility

Modding work on Metaphor: ReFantazio (PC). The game runs on a proprietary engine, so most of this project is reverse
engineering: working out file formats, engine structures and functions, then writing mods and tools on top of them.
This is a public repository.

## Ground rules

1. **Cite or flag.** Every claim about the game or its engine (a file format, a struct layout, an offset, a function's
   behaviour, a signature) cites its evidence:
   - a saved source under `docs/raw-sources/`, such as a community write-up or a tool's documentation; or
   - a reproducible note naming the tool, its version, the game build and the steps.

   If neither exists, write `[NEEDS VERIFICATION]`. Never fill in engine details from memory: guessed offsets and formats
   look plausible and waste days. The checks fail a raw source or finding without its header.
2. **Pin everything to a game build.** Offsets, signatures and struct layouts change between patches. Every note and
   every hook names the game build it was verified on. Code checks the build or the signature before it patches, and
   does nothing if it doesn't match. The checks fail a finding without a build, and architecture rules 6 and 8
   (`docs/coding-standards.md`) route every patch through the hook loader's build check.
3. **Use what exists first.** Check the community's existing loaders, formats and tools before writing new ones. Record
   what was checked in `docs/raw-sources/`.
4. **Never commit game content.** That covers executables, DLLs, archives, extracted assets, decompiled output and
   disassembler databases. They live in the git-ignored game folders, and the checks block them by folder, extension,
   size and known hash. A short snippet quoted in a note to explain a finding is fine. Releases ship our code, our data,
   and third-party libraries under GPL-3.0-compatible licences, each with its licence and a pinned version (ADR 0005);
   never game content.
5. **Single-player modding only.** Don't circumvent DRM or anti-tamper protection, and don't touch online features or
   other players. If a protection blocks a technique, stop, note it, and look for another approach.
6. **No personal information or local paths in committed files.** That means no real name, email or phone number, and no
   absolute path like `C:\Users\...`. Use paths relative to the repository or environment variables. The checks scan
   for both, with your private patterns set up as in `README.md#checks`.

## Layout

- `docs/raw-sources/`: saved copies of the sources the notes rely on. Add to it; never edit what's there.
- `docs/findings/`: one note per finding, from the template in its README.
- `docs/players/` and `docs/contributors/`: docs by audience, then by Diátaxis type (`docs/README.md`).
- `docs/coding-standards.md`: the judgement and architecture rules reviews check.
- `.github/CONTRIBUTING.md`: setup and the change workflow. `README.md#checks`: the check command's stages.
- This file holds every rule agents follow; `CLAUDE.md` only imports it. Put new rules here. Agent definitions
  (`.claude/agents/`) and skill settings (`docs/agents/`) live where their tools look for them.

## Claude Code setup

This project runs with its own config directory, so the owner's other projects' settings, agents and plugins don't load
here. Start it with `.\scripts\claude.ps1` from the repository root, not `claude`. The only plugin is Matt Pocock's skills
(`mattpocock-skills`). The one custom agent is `security-reviewer` (`.claude/agents/`): run it before a release, and on
staged changes before any commit that touches hooking, injection, loading, install or update code. For the hook layer
and its deploy scripts the pre-commit hook enforces this through the reviewer's stamp, and CI checks the trailer
([`CONTRIBUTING.md`](.github/CONTRIBUTING.md#security-review-and-deny-rules)). Never set `SECURITY_REVIEW_SKIP`.

## Agent skills

### Issue tracker

Issues live in GitHub Issues on `Sour-Dev-Home/metaphor-accessibility`, managed with the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

The five default labels. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `GLOSSARY.md` and `docs/adr/` at the repo root, created by `/domain-modeling` when first needed.
See `docs/agents/domain.md`.
