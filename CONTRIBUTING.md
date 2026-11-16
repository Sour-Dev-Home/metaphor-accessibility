# Contributing

This guide takes you from a fresh clone to passing checks in about 30 minutes, most of it installing the .NET SDK. You
don't need the game for this.

Read the ground rules in [`AGENTS.md`](AGENTS.md) first. They apply to people and agents alike, and the checks enforce
the mechanical ones: no game content, no personal information, and a header on every raw source and finding.

## 1. Install the tools

On Windows:

- [Git for Windows](https://git-scm.com/download/win), which includes Git Bash. Every script runs in Git Bash.
- The .NET SDK version pinned in [`global.json`](global.json), from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet).
  Roll-forward is off, so a newer SDK won't do. `dotnet --list-sdks` shows what you have.
- Optional: the [GitHub CLI](https://cli.github.com/) (`gh`) for issues and pull requests.

## 2. Clone and set up

In Git Bash:

```sh
git clone https://github.com/Sour-Dev-Home/metaphor-accessibility.git
cd metaphor-accessibility
git config core.hooksPath .githooks
cp .claude/pii-patterns.example.txt .claude/pii-patterns.txt
```

The hooks check each commit and push, running only what the change needs:

- **Before a commit:** the repo rules on staged files, plus formatting, docs and hook signatures when those files are
  staged. Signatures need `METAPHOR_GAME_EXE` set to the game's exe; without it the hook says how to run them.
- **On the commit message:** its shape, and the security-review trailer when the commit touches hook or deploy code
  ([below](#security-review-and-deny-rules)).
- **Before a push:** the Release build and the `Unit` and `RoundTrip` tests, when the pushed commits change code. The
  first push after a pull builds from cold and takes longer.

CI runs everything again, so it stays the authority. In `.claude/pii-patterns.txt`, replace
the two example lines with your own private strings, such as your name and email, one per line. It's git-ignored, and
the checks block any commit that contains one. Don't leave the example lines in: they match the example file itself, so
the checks would fail.

## 3. Run the checks

```sh
bash scripts/check.sh fast
bash scripts/check.sh full
```

`fast` checks the staged files, so with nothing staged it passes at once. `full` checks every tracked file, then builds,
runs the `Unit` and `RoundTrip` tests and checks formatting. Both end with exit code 0 when everything passes.
[README.md](README.md#checks) lists every stage.

CI runs `full` on Linux for a pull request (faster; tests marked `WindowsOnly` and the Pester script tests are left to
Windows, and a docs-only change skips the .NET work), and the whole Windows gate in the merge queue. A pull request that
passes can still fail in the queue; run `full` on Windows locally before you push a change near Windows behaviour. Mark a
test that only holds on Windows with `[Trait("Category", "WindowsOnly")]` instead of `Unit`.

`RoundTrip` tests check our format code against real game files, so they need game data and are skipped without it (CI
never has any, per [ADR 0001](docs/adr/0001-ci-never-sees-game-content.md)). To run them, point `METAPHOR_GAME_DATA` at
your extracted files, or put them in the git-ignored `extracted/` folder. If the variable is set, `extracted/` isn't
checked. Put the [game build](GLOSSARY.md) on the first line of `game-build.txt` in that folder; the tests show it in
their names.

If you change docs, also install the docs tools (Node.js is needed for the Markdown lint) and run their stage:

```sh
bash scripts/install-docs-tools.sh
bash scripts/check.sh docs
```

A broken link fails it; everything else is a warning. The check-script tests (`tests/check/test-check.sh`) need these
tools too. To flag another filler word, add it to
[`.vale/styles/Project/Filler.yml`](.vale/styles/Project/Filler.yml).

## 4. Make a change

1. Pick an issue, or open one with a template. Issues labelled `ready-for-agent` or `ready-for-human` are ready to start.
2. Work on a branch. `main` only takes pull requests that pass CI.
3. Follow the [coding standards](docs/coding-standards.md).
4. Commit with a typed subject such as `feat: ...`, and a short body that says why. The commit-msg hook checks the
   shape; the rules are in the header of [`scripts/check.sh`](scripts/check.sh).
5. Open a pull request that says what changed and why. A PR that touches hooking, loading or injection includes
   in-game evidence on a named game build.

## Security review and deny rules

**Must-run review.** The security-review scope is `src/Metaphor.Hooks/` plus the five scripts listed in
[`scripts/security-stamp.sh`](scripts/security-stamp.sh), which deploy, launch, package or fetch native code. A commit
touching it needs a review stamp:

1. Stage the change, then run the `security-reviewer` agent. When it finds nothing critical or important, its last step
   runs `scripts/security-stamp.sh`, which hashes the staged scope into a stamp under `.git/`.
2. Commit. The pre-commit hook refuses the commit unless the stamp matches the staged scope, so any edit after the
   review needs a new one. The commit-msg hook appends `Security-Review: <hash>`.
3. CI fails a pull request commit that touches the scope without a `Security-Review` trailer matching its content.

**Override (maintainer only).** `SECURITY_REVIEW_SKIP="<reason>" git commit ...` lets the commit through and records
`Security-Review-Skipped: <reason>`, which CI accepts. Agents are denied it.

**Deny rules.** [`.claude/settings.json`](.claude/settings.json) denies Claude Code every force push, printing
`.claude/pii-patterns.txt` (the Read tool, `cat`, `type`, `Get-Content`, `head`, `tail`, `less`), setting
`SECURITY_REVIEW_SKIP`, and skipping the git hooks with `--no-verify` (or `git commit -n`). `scripts/check.sh` and a `grep -F -f` over the patterns file stay allowed. The maintainer's
private `~/.claude-metaphor/settings.json` also denies edits and common shell writes inside the game's install folder
and its save folder.

These rules match the command text Claude Code sees, so another spelling of the same command can slip past them. The
stamp guards against a forgotten review, not a determined one: anyone can run the stamp script, and CI checks the
trailer, not that a review happened. They are a strong guard, not a guarantee.

## Where things go

- Notes on what the game does: [`docs/findings/`](docs/findings/README.md), citing sources saved in
  [`docs/raw-sources/`](docs/raw-sources/README.md).
- Decisions: [`docs/adr/`](docs/adr/). Terms: [`GLOSSARY.md`](GLOSSARY.md).
- Docs for players and contributors: [`docs/`](docs/README.md).

Report security problems privately, as [SECURITY.md](SECURITY.md) explains. Everyone here follows the
[code of conduct](CODE_OF_CONDUCT.md).

By contributing, you agree that your work is licensed as [README.md](README.md#license) describes.
