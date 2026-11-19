# metaphor-accessibility

Mods, tools and reverse-engineering notes for Metaphor: ReFantazio on PC.

> **Disclaimer:** this is a non-commercial, fan-made accessibility tool for blind and visually impaired players. It is
> not affiliated with, endorsed by or sponsored by ATLUS or SEGA.

> **Support:** if you'd like to support my time on this project, you can tip on [Ko-fi](https://ko-fi.com/soure).
> Tipping is entirely optional and gets you nothing extra: no early builds, no extra features, no priority requests.
> The mod is and stays free for everyone.

Status: just started. Nothing here is usable yet.

## What's here, and what isn't

- **Here:** our own code, and notes on the game's formats and engine, each pinned to the game build it was checked on.
- **Not here:** anything from the game itself (executables, archives, assets, decompiled code). You need your own copy of
  the game.

This project is single-player modding only. It doesn't touch online features, DRM or anti-tamper protection.

Metaphor: ReFantazio and related names are trademarks of their respective owners, used here only to say which game
this tool works with.

To contribute, start with [CONTRIBUTING.md](.github/CONTRIBUTING.md).

## License

Code is under [GPL-3.0](LICENSE). Docs (the Markdown files, including everything in `docs/`) are under
[CC BY 4.0](docs/LICENSE), except `docs/raw-sources/`, which holds copies of other people's work under their own terms.

- [CREDITS.md](CREDITS.md) lists the projects whose code, findings or ideas this one uses.
- `src/Metaphor.Hooks/SquirrelRegistrationListener.cs` follows a technique from MF.Toolkit (AGPL-3.0), credited in the
  file. It's a development-only hook that never ships in a release.
- The mod package ships third-party libraries under their own licences, each in `licenses/` with where its source is
  ([ADR 0005](docs/adr/0005-prism-speech-backend.md), [ADR 0006](docs/adr/0006-hooks-through-reloaded-shared-hooks.md)).

## Checks

One command runs every check, locally (Git Bash on Windows) and in CI:

| Stage | Runs | Where |
|---|---|---|
| `scripts/check.sh fast` | repo rules on staged files | by hand |
| `scripts/check.sh pre-commit` | `fast`, then formatting for staged C#, `docs` when Markdown is staged, `signatures` when `hook-signatures.json` is staged and `METAPHOR_GAME_EXE` is set, and a matching security-review stamp when the security-review scope is staged ([CONTRIBUTING.md](.github/CONTRIBUTING.md#security-review-and-deny-rules)) | pre-commit hook |
| `scripts/check.sh pre-push` | the Release build, then `Unit` and `RoundTrip` tests, when the pushed commits change code (refs on stdin) | pre-push hook |
| `scripts/check.sh rules` | repo rules on every tracked file in CI; locally on the files changed since `origin/main` (every file without `origin/main`, when the rules change, or with `METAPHOR_SCAN_ALL=1`) | CI on Ubuntu |
| `scripts/check.sh full` | repo rules, then the .NET build, `Unit` and `RoundTrip` tests, formatting, the smoke-test and deploy scripts' own tests (Pester 5+) and the check-script suites ([`run-check-suites.sh`](scripts/run-check-suites.sh)) | CI: Linux on pull requests, Windows in the merge queue and on `main` (see below) |
| `scripts/check.sh docs` | broken relative links and anchors fail; Markdown lint, Vale and ADRs over 250 words warn | CI on Ubuntu |
| `scripts/check.sh slow` | Stryker.NET mutation testing of `Metaphor.Core` and a jscpd duplication report, into `artifacts/slow/`; reports only, fails only when a tool can't run | weekly CI on Windows (`slow.yml`, also run by hand); never gates a merge |
| `scripts/check.sh signatures [EXE]` | each hook signature matches exactly once in the game exe on disk, no launch ([how-to](docs/contributors/how-to/check-hook-signatures.md)) | locally, before a hook PR |
| `scripts/check.sh pr-body FILE` | warns when a PR description passes 300 words, not counting Evidence | CI on pull requests |
| `scripts/check.sh commit-msg FILE` | one commit message's shape (see the script's header); appends the `Security-Review` trailer to a commit touching the security-review scope | commit-msg hook |
| `scripts/check.sh commits BASE..HEAD` | each commit's shape in a pull request, and a matching `Security-Review` or `Security-Review-Skipped` trailer on each one touching the scope | CI on Ubuntu |

`full` prints each step's time (`restore`, `build`, `tests`, `format`, `pester`, `check-scripts`) and keeps them in the run log. Locally
the pre-push build covers only the test projects the change selects (and any changed project or dependent of one that no test reaches); a build-wide
file or an unplaceable change builds the solution. `tests/check/test-check.sh` runs its tests in parallel
(`METAPHOR_TEST_JOBS=1` runs them one at a time), as does `scripts/run-pester.sh`, and `scripts/run-check-suites.sh` runs
the suites in the same bounded pool ([`pool.sh`](scripts/check/pool.sh)). These print a line per step,
test or file as it finishes (`[37/132] test_x ok (2.1 s)`) and write it to the clone's live log,
`<git dir>/check-live-log`, which `scripts/check-status.sh` reads (`test-check: 37/132, 12 min`). Started from Git
Bash, the check-script suites run in WSL when it's set up, in seconds instead of minutes
([CONTRIBUTING.md](.github/CONTRIBUTING.md#run-the-check-script-suites-in-wsl)).

`full` needs the .NET SDK pinned in `global.json`. CI runs it by two paths under the same required checks
(`repo-rules`, `build`, `scripts`, `security`, and `review-gate`, the label gate run from `main`'s copy of
[`review-gate.yml`](.github/workflows/review-gate.yml)):

- **Pull requests** do the .NET work on Linux: `build` runs `full` there, minus the tests marked `WindowsOnly` (the
  `Category` trait; `check.sh` adds that category on Windows only). The Pester script tests run in parallel `pester`
  jobs on Windows, one per file under `tests/smoke/`; the `scripts` check waits for them all (#314). Each job starts with a `git diff` check (`scripts/ci-changes.sh`) and finishes green at once
  when nothing it covers changed: `build` skips when only `docs/` or Markdown changed, and `scripts` skips unless
  `scripts/`, `tests/smoke/`, the stand-in game or the workflows changed. `repo-rules` and `security` (CodeQL) always run.
- **The merge queue and pushes to `main`** run everything, skipping nothing: `build` on Windows runs `full`, then
  `scripts/build-release.sh` packages the build it tested (`scripts/package-mod.sh`), zips it reproducibly for players
  (`scripts/zip-package.ps1`) with its `SHA256SUMS`, and rebuilds the mod in a second checkout at another path on the
  same runner; the package and the zip must both match. Both are kept as workflow artifacts. On `main`, the `attest`
  job signs that zip with a build-provenance attestation and verifies it with the command in
  [Check a download](docs/players/how-to/check-a-download.md) (#308).
- **A `v*` tag on a commit on `main`** runs the `Release` workflow (#310), which drafts an attested release:
  [Cut a release](docs/contributors/how-to/cut-a-release.md). Players' builds stay
  Windows-built, because a Linux build isn't byte-identical
  ([measurement](docs/raw-sources/linux-build-spike-2026-11-16.md)). A local package won't match CI's hash, because
  CI-only build settings remove machine paths; the CI artifact is the one that counts.

Each stage is timed against its budget from [#1](https://github.com/Sour-Dev-Home/metaphor-accessibility/issues/1)
(`scripts/stage-timing.sh`). `fast` and `pre-commit` (stage 1, 10 s) and `signatures` (stage 4, 30 s) print a
`timing:` line when they finish. CI's `timing` job lists each job's duration on the run's summary page and times stage
2 from the first job's start to the last one's finish (5 min); `slow.yml` lists stage 6's, which has no budget. A stage
over budget prints a warning, and in CI an annotation on the run; it never fails the run.

Turn on the committed git hooks once per clone:

```sh
git config core.hooksPath scripts/githooks
```

The checks also fail a raw source without its header ([`docs/raw-sources/`](docs/raw-sources/README.md)) and a
finding without its game build ([`docs/findings/`](docs/findings/README.md)).

The checks block game content and personal information. For the second, copy `.claude/pii-patterns.example.txt` to
`.claude/pii-patterns.txt` (git-ignored) and list your own private strings. CI checks general patterns on every pull
request and merge-queue run, and the private list on pushes to `main`, from a secret of the `main-push` environment,
which only `main` may use. To set it, a maintainer runs:

```sh
gh secret set PII_PATTERNS --env main-push < .claude/pii-patterns.txt
```

Pushes to `main` fail until the secret is set. The check never prints a matching line or pattern, because CI logs are
public.

## Supply chain

The [security model](docs/contributors/explanation/security-model.md) gathers every security decision in one place.

Workflows use read-only tokens by default, and a job that writes asks for it itself. Every action is pinned to a
commit. [Dependabot](.github/dependabot.yml) proposes NuGet and Actions updates weekly. CI's `security` job, which the
ruleset requires, runs CodeQL and fails on any package with a known vulnerability, read by a pinned OSV-Scanner from
the committed lock files ([`vuln-audit.sh`](scripts/vuln-audit.sh)). The ruleset also blocks a PR that
adds a high or critical CodeQL security alert; other alerts show in the Security tab without blocking. To unblock a
false positive, dismiss the alert there with a reason.
[Scorecard](.github/workflows/scorecard.yml) runs weekly as a report only; its results are in the Security tab.
Bundled binaries are pinned by hash before they're packed (Prism by `scripts/fetch-prism.ps1`, ADR 0005; NuGet packages
by lock files). Every `main` build's zip is attested, and only CI's `attest` job may ask for a signing token
(`tests/scripts/test-release-integrity.sh`); the audit behind this is
[release integrity](docs/raw-sources/release-integrity-audit-2026-11-18.md).
`tests/scripts/test-repo-config.sh` checks these rules.

## Branch rules

All changes reach `main` through pull requests that pass CI, then a merge queue that re-runs CI on each PR merged with
the ones ahead of it, so a PR never has to be updated by hand when `main` moves. Auto-merge is on: `gh pr merge --auto
--squash` queues a PR as soon as its checks pass. The ruleset is exported in
[`.github/rulesets/main.json`](.github/rulesets/main.json), which is the source of truth. It requires no approving
review, because a sole maintainer can't approve their own PR. Admins can bypass it, and GitHub logs each bypass in the
ruleset's insights. [`.github/CODEOWNERS`](.github/CODEOWNERS) marks the code that needs the owner's review.

The private personal-information list only reaches pushes to `main`, so a PR's checks don't run it, and a match shows
up after the merge.

A maintainer applies the export with `POST` the first time, then `PUT` to the id that `gh api
repos/{owner}/{repo}/rulesets` lists:

```sh
gh api -X POST repos/{owner}/{repo}/rulesets --input .github/rulesets/main.json
gh api -X PUT repos/{owner}/{repo}/rulesets/<id> --input .github/rulesets/main.json
```
