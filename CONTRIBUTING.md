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
git clone https://github.com/Sour-Dev-Home/metaphor-refantazio.git
cd metaphor-refantazio
git config core.hooksPath .githooks
cp .claude/pii-patterns.example.txt .claude/pii-patterns.txt
```

The hooks run the fast checks before each commit and check each commit message. In `.claude/pii-patterns.txt`, replace
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

## Where things go

- Notes on what the game does: [`docs/findings/`](docs/findings/README.md), citing sources saved in
  [`docs/raw-sources/`](docs/raw-sources/README.md).
- Decisions: [`docs/adr/`](docs/adr/). Terms: [`GLOSSARY.md`](GLOSSARY.md).
- Docs for players and contributors: [`docs/`](docs/README.md).

Report security problems privately, as [SECURITY.md](SECURITY.md) explains. Everyone here follows the
[code of conduct](CODE_OF_CONDUCT.md).

By contributing, you agree that your work is licensed as [README.md](README.md#license) describes.
