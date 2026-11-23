# Start here

An accessibility mod for Metaphor: ReFantazio on PC, for blind and visually impaired players, built by reverse
engineering a proprietary engine. This page maps the repository, the tools and the workflow; each line links to the page
that holds the detail.

The [ground rules in `AGENTS.md`](../../AGENTS.md#ground-rules) apply to people and agents alike. To get going:
[set up a clone](how-to/set-up-a-clone.md) (one command, about 30 minutes, no game needed).

## Find work

Most issues need your own copy of the game, but many don't. Claim first, so two people don't build the same thing.

- **No game needed:** every open issue without `needs-game` or `needs-game-session`
  ([filter](https://github.com/Sour-Dev-Home/metaphor-accessibility/issues?q=is%3Aopen+-label%3Aneeds-game+-label%3Aneeds-game-session)).
  Docs, check scripts, tests and anything the unit tests cover live here. `gh issue list --search "-label:needs-game"`
  gives the same list.
- **Start small:** [`good first issue`](https://github.com/Sour-Dev-Home/metaphor-accessibility/issues?q=is%3Aopen+label%3A%22good+first+issue%22)
  marks a small issue with clear acceptance criteria.
- **Ready to build:** `ready-for-agent` and `ready-for-human` mean fully specified; skip `needs-triage` and `needs-info`
  until the maintainer has answered ([labels](../agents/triage-labels.md)). Read the issue's comments first: decisions
  are recorded there.
- **Claim:** comment that you're on it, or assign yourself, after a look for an existing branch or PR
  ([the claim step](../agents/workflow.md#start)).

## Repository layout

```text
src/                      the mod
  Metaphor.Core/            plain library: formats, logic, hook signatures, known game builds
  Metaphor.Features.*/      one project per feature (Dialogue, Field, Menus, Battle, Updates); never depend on each other
  Metaphor.Hooks/           the thin hook layer: the Reloaded-II mod, the only project touching game memory
installer/                the installer exe (ADR 0020): a .NET Framework 4.8 launcher that runs the zip's installer
tests/
  Metaphor.*.Tests/         xUnit tests (Unit, RoundTrip, WindowsOnly); Architecture.Tests check the architecture rules
  Metaphor.StandInGame/     stands in for Reloaded-II and the game in the script tests
  pester/                   Pester tests for the PowerShell scripts
  bash/, check/             tests for the check scripts
  fixtures/                 recorded sessions and outage replies the tests read
scripts/
  check.sh, check/          the check command and its stages
  githooks/                 the git hooks, which call check.sh
  ci/                       CI-only steps: security stamp, vulnerability audit, test selection, alerts
  release/                  build, package and zip the mod, and build the installer exe around the zip
  player/                   exactly what the release zip ships (installer, install check)
  dev/                      game sessions (session.ps1: deploy, run, smoke, spike, inspect, status), Ghidra
  setup/                    onboard, WSL, docs tools, Claude Code launcher
tools/                    dev-only programs: session log, session fixture, signature check, Ghidra scripts,
                          and the docs and slow-suite tool pins (docs/, slow/)
docs/                     players/, contributors/, findings/, raw-sources/, adr/, agents/ (see docs/README.md)
```

Git-ignored, never committed ([rule 4](../../AGENTS.md#ground-rules)): `game/`, `extracted/`, `dumps/` and `captures/`
for game content; `out/ghidra/` for the Ghidra project; `artifacts/` for the packaged mod, Prism and slow-suite output;
`bin/` and `obj/` for builds; `.claude/worktrees/` for agents' worktrees. [`docs/README.md`](../README.md) says which
page type goes in which docs folder.

## Tool stack

| Tool | For | Pinned or installed by |
|---|---|---|
| Git for Windows (Git Bash) | every script runs in Git Bash | [`onboard.ps1`](../../scripts/setup/onboard.ps1) (winget pin) |
| PowerShell 7 (`pwsh`) | dev scripts, Pester, onboard | [`onboard.ps1`](../../scripts/setup/onboard.ps1) (winget pin) |
| Windows PowerShell 5.1 | `scripts/player/` must load in it, ASCII only (each module's header) | ships with Windows |
| .NET SDK | build and tests | [`global.json`](../../global.json), roll-forward off |
| WSL 2 (optional) | faster check-script suites | [`setup-wsl.ps1`](../../scripts/setup/setup-wsl.ps1), tools in [`wsl-tools.txt`](../../scripts/setup/wsl-tools.txt) |
| Reloaded-II | loads the mod | your install for dev runs; the installer's pin is [`install-pins.json`](../../scripts/player/install-pins.json) |
| Ghidra and a JDK (optional) | the shared static-analysis project | [query the Ghidra project](how-to/query-the-ghidra-project.md) |
| FFmpeg, Ollama (optional) | the chapter scrub | your install; [scrub a chapter](how-to/scrub-a-chapter.md) |
| Pester | PowerShell script tests | [`onboard.ps1`](../../scripts/setup/onboard.ps1) (PowerShell Gallery pin, which CI installs); [`run-pester.sh`](../../scripts/check/run-pester.sh) takes 5.0 or later and warns off the pin |
| Stryker.NET | weekly mutation testing | [`.config/dotnet-tools.json`](../../.config/dotnet-tools.json) |
| lychee, Vale | `check.sh docs` | [`install-docs-tools.sh`](../../scripts/setup/install-docs-tools.sh) |
| markdownlint, Node.js | `check.sh docs` | [`tools/docs/package.json`](../../tools/docs/package.json); Node.js LTS from `onboard.ps1` |
| Visual C++ Redistributable | lychee on Windows | [`onboard.ps1`](../../scripts/setup/onboard.ps1) (winget pin) |
| OSV-Scanner | CI's vulnerability audit | [`vuln-audit.sh`](../../scripts/ci/vuln-audit.sh) |
| GitHub CLI (`gh`) | issues and pull requests | [`onboard.ps1`](../../scripts/setup/onboard.ps1) (winget pin) |
| Claude Code (optional) | agents and skills | [`claude.ps1`](../../scripts/setup/claude.ps1) starts it with this project's config |

## Environment variables

| Variable | What it does |
|---|---|
| `METAPHOR_GAME_EXE` | the game's exe; signature checks, deploys and onboard's game step use it |
| `METAPHOR_RELOADED_EXE` | Reloaded-II's launcher for the dev scripts, when it isn't in Reloaded-II's config |
| `METAPHOR_GAME_DATA` | your extracted game files for the `RoundTrip` tests (default: `extracted/`) |
| `METAPHOR_CHECK_SLOTS` | how many check runs may build at once on this machine (default 2) |
| `METAPHOR_TEST_JOBS` | parallel jobs for the test pools (`1` runs one at a time) |
| `METAPHOR_PESTER_BATCH` | Pester tests per process in `run-pester.sh` (default 4) |
| `METAPHOR_NO_WSL` | `1` runs the check-script suites in Git Bash even with WSL set up |
| `METAPHOR_SCAN_ALL` | `1` makes `check.sh rules` scan every tracked file, as CI does |
| `METAPHOR_FULL_TESTS` | `1` builds and tests the whole solution locally instead of the selected projects |
| `PRISM_CACHE` | download cache for [`fetch-prism.ps1`](../../scripts/dev/fetch-prism.ps1) (default `artifacts/native/cache`) |
| `GHIDRA_INSTALL_DIR`, `JAVA_HOME` | where [`ghidra.ps1`](../../scripts/dev/ghidra.ps1) finds Ghidra and the JDK |
| `SECURITY_REVIEW_SKIP` | maintainer-only review override ([security review](reference/security-review.md#the-stamp)) |

Other `METAPHOR_*` variables (`METAPHOR_CHECK_LOCK`, `METAPHOR_LIVE_LOG`, `METAPHOR_STANDIN_*` and the like) are
internal to the scripts and their tests; each script's header names the ones it reads.

## What you'll deal with

1. **Set up once:** `.\scripts\setup\onboard.ps1` ([set up a clone](how-to/set-up-a-clone.md)).
2. **Check:** `bash scripts/check.sh full` before you push; the pre-push hook reuses its pass
   ([checks](reference/checks.md)).
3. **Open a PR** that says what changed and why, with evidence and the test
   [ground rule 9](../../AGENTS.md#ground-rules) asks for; a hook, loading or injection change shows in-game
   output on a named game build. `gh pr merge <n> --auto` queues it.
4. **Review:** a PR in the [security-review scope](reference/security-review.md) needs a stamped review; a PR by
   anyone but the maintainer that changes anything outside `docs/` also needs the maintainer's `security-reviewed`
   label.

The guards, why each may block you, and what to do:

- **Game content, personal information or a local path** (pre-commit, CI): remove it
  ([rules 4 and 6](../../AGENTS.md#ground-rules)); a false match from your private patterns:
  [reword the pattern](how-to/set-up-a-clone.md#2-clone-and-set-up).
- **A raw source without its header, a finding without its game build:** use the template in
  [`raw-sources/`](../raw-sources/README.md) or [`findings/`](../findings/README.md).
- **Commit message shape** (commit-msg hook, CI): [commit messages](reference/commit-messages.md).
- **No security-review stamp** (pre-commit, CI trailer): [the stamp](reference/security-review.md#the-stamp).
- **Build, tests or formatting** (pre-push, `full`): fix and rerun; a busy build slot:
  [recorded pass](reference/checks.md#recorded-pass), `bash scripts/check/check-status.sh` shows who holds it.
- **A feature with no setting** (architecture rule 18): give it a member of its project's feature enum, mark it
  `[FeatureSetting<TheFeatureEnum>(...)]` and regenerate [the settings reference](../players/reference/settings.md); the
  rule is in [coding standards](../coding-standards.md#architecture-rules).
- **Stale settings reference** (`Metaphor.Settings.Tests`): run
  `dotnet run --project tools/Metaphor.SettingsReference -c Release -v q` from the repository root and commit the page.
- **Stale navigation sounds** (`NavSoundFilesTests`): run `dotnet run --project tools/Metaphor.NavSounds -c Release -v q`
  from the repository root and commit `data/sounds/`.
- **Architecture tests:** a rule in [coding standards](../coding-standards.md#architecture-rules) was broken.
- **Broken link or anchor** (`check.sh docs`): fix the link; lint and Vale only warn.
- **Missing `security-reviewed` label** (`review-gate`): wait for the maintainer
  ([the label gate](reference/security-review.md#everyone-elses-pull-requests)).
- **Merge queue fails on Windows after the PR passed:** [in CI](reference/checks.md#in-ci).
- **An action not pinned or not allowed:** [add or update a GitHub Action](how-to/add-or-update-a-github-action.md).
- **Claude Code refuses a command** (force push, `--no-verify`, the patterns file): a
  [deny rule](reference/security-review.md#deny-rules); do it another way.

## Change workflow

Pick an issue (`ready-for-agent` or `ready-for-human` are ready to start; [labels](../agents/triage-labels.md)), work on
a branch, follow the [coding standards](../coding-standards.md), and open a PR. The whole path, from hooks to CI to the
merge queue: [how a change reaches main](explanation/how-a-change-reaches-main.md). Agents also follow
[`workflow.md`](../agents/workflow.md). Your branch is deleted after its PR merges; delete your local copy with
`git branch -d <branch>`.

### Two skills worth using

Onboard installs Matt Pocock's skills for Claude Code (start Claude with `.\scripts\setup\claude.ps1`). Two of them carry
most of how this project works:

- **Grilling** (say "grill me"). Before you build anything non-trivial, Claude questions your plan round by round,
  each question with a recommended answer, until nothing is left assumed. It's how the maintainer settles decisions,
  and the answers go on the issue, so check an issue's comments for decisions already made before you start. A wrong
  guess about the engine or a gate costs days here; a grilling costs minutes.
- **`pr`** (writing a PR body). It shapes the body as a summary, before-and-after evidence, and the merge danger: a
  one-way or two-way door and the blast radius. Reviews here hinge on evidence (a test run, in-game output on a named
  build) and on what a change could break, so this is the shape a review can act on fastest.

Both are optional; without Claude, write the same things by hand.

### Contributing without Claude

You don't need Claude Code or any agent tooling.

- **Pick an issue** from [Find work](#find-work). `ready-for-agent` and `ready-for-human` name who the maintainer
  expected; neither stops you.
- **The game.** Most work needs your own copy of the game, to check offsets, formats and in-game behaviour. Without
  it you can still do docs, tooling and anything the unit tests cover.

Then deliver, in this order:

1. **Claim the issue**: comment that you're on it, after a look for an existing branch or PR
   ([the claim step](../agents/workflow.md)).
2. **Branch** from `origin/main`, named `<issue>-<slug>`, for example `125-install-guide`.
3. **Commit** in the [commit message shape](reference/commit-messages.md); the commit-msg hook checks it.
4. **Check** before you push: `bash scripts/check.sh docs` for a change that touches only Markdown, and
   `bash scripts/check.sh full` for anything else ([checks](reference/checks.md)).
5. **Push**, then open the PR with the [PR template](https://github.com/Sour-Dev-Home/metaphor-accessibility/blob/main/.github/pull_request_template.md)
   (Summary, Evidence, Merge Danger), linking the issue with `Closes #n`.
6. **Queue the merge**: `gh pr merge <n> --auto`.
7. **Security stamp**: only a change in the [review scope](reference/security-review.md) needs one; a docs-only PR
   doesn't ([the stamp](reference/security-review.md#the-stamp)).

To write a player page, follow [write a player doc](how-to/write-a-player-doc.md).

### What a game session produces

A play session, yours or a tester's, leaves a log and notes; issues come from those. The player presses `F8` (the
[mark key](../players/reference/keys-and-speech.md#mark-key-note-a-moment-for-your-report)) at a wrong or silent
moment, which writes a numbered marker with the time to `report-log.txt`
([settings](../players/reference/settings.md#report)). To run one yourself, and where its output goes (never into
git), follow [run a game session](how-to/run-a-game-session.md). File each finding as an issue per
[`issue-tracker.md`](../agents/issue-tracker.md), tagged `needs-game` when it needs the game to build or check.

## Playbooks

How-tos: [set up a clone](how-to/set-up-a-clone.md), [add a hook](how-to/add-a-hook.md),
[deploy a dev build](how-to/deploy-a-dev-build.md), [run the smoke test](how-to/run-the-smoke-test.md),
[add a game build](how-to/add-a-game-build.md), [check hook signatures](how-to/check-hook-signatures.md),
[turn on the dialogue window hooks](how-to/turn-on-the-dialogue-window-hooks.md),
[inspect the running game](how-to/inspect-the-running-game.md),
[query the Ghidra project](how-to/query-the-ghidra-project.md),
[add or update a GitHub Action](how-to/add-or-update-a-github-action.md), [cut a release](how-to/cut-a-release.md),
[handle a GitHub outage](how-to/handle-a-github-outage.md),
[merge main into a draft PR](how-to/merge-main-into-a-draft-pr.md),
[run a game session](how-to/run-a-game-session.md), [a check or CI failed](how-to/a-check-or-ci-failed.md),
[write a finding or save a raw source](how-to/write-a-finding.md),
[add a feature project](how-to/add-a-feature-project.md), [handle a game update](how-to/handle-a-game-update.md),
[write a player doc](how-to/write-a-player-doc.md), [plan a chapter](how-to/plan-a-chapter.md),
[scrub a chapter](how-to/scrub-a-chapter.md), [name a nearby thing](how-to/name-a-nearby-thing.md).

Reference: [checks](reference/checks.md), every check stage and how CI runs them;
[security review](reference/security-review.md), the stamp, the label gate and the deny rules;
[commit messages](reference/commit-messages.md); [supply chain](reference/supply-chain.md);
[branch rules](reference/branch-rules.md), the ruleset and the merge queue.

Explanation: [how a change reaches main](explanation/how-a-change-reaches-main.md), a diagram of the hooks, CI and the
merge queue; [security model](explanation/security-model.md), each security decision and what it protects;
[TLOU2's accessibility options](explanation/tlou2-accessibility-feasibility.md), which of them this mod can have;
[installer exe options](explanation/installer-exe-options.md), the ways to build the installer exe, compared.

Notes on the game go in [`findings/`](../findings/README.md), citing sources saved in
[`raw-sources/`](../raw-sources/README.md); decisions in [`adr/`](../adr/); terms in [`GLOSSARY.md`](../../GLOSSARY.md).
Report security problems privately, as [`SECURITY.md`](../../.github/SECURITY.md) explains.
