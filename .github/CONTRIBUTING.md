# Contributing

This guide takes you from a fresh clone to passing checks in about 30 minutes, most of it installing the .NET SDK. You
don't need the game for this.

Read the ground rules in [`AGENTS.md`](../AGENTS.md) first. They apply to people and agents alike, and the checks enforce
the mechanical ones: no game content, no personal information, and a header on every raw source and finding.

For the whole path from your branch to `main` in one picture, see
[how a change reaches main](../docs/contributors/explanation/how-a-change-reaches-main.md).

## One command

In PowerShell, from the root of your clone:

```powershell
.\scripts\onboard.ps1
```

It does steps 1 to 3 below and the rest of the setup, asks before installing anything, and ends with a ready summary
and a prompt to paste into Claude. It's safe to run again. Its header in
[`scripts/onboard.ps1`](../scripts/onboard.ps1) lists each step; the steps below are for doing them by hand.

## 1. Install the tools

On Windows:

- [Git for Windows](https://git-scm.com/download/win), which includes Git Bash. Every script runs in Git Bash.
  In PowerShell, plain `bash` may start WSL's Linux bash instead, which has no git and fails with
  `git: command not found`. Open a Git Bash window, or call it by path:
  `& "C:\Program Files\Git\bin\bash.exe" scripts/check.sh full`. The same applies to every `bash scripts/...` command
  in this guide. The [`metaphor-checks` distro](#run-the-check-script-suites-in-wsl) doesn't change this: the suites
  reach it from Git Bash on their own. If it's your only distro, WSL makes it the default, and plain `bash` then opens
  it as root; still use Git Bash.
- The .NET SDK version pinned in [`global.json`](../global.json), from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet).
  Roll-forward is off, so a newer SDK won't do. `dotnet --list-sdks` shows what you have.
- Optional: the [GitHub CLI](https://cli.github.com/) (`gh`) for issues and pull requests.

## 2. Clone and set up

In Git Bash:

```sh
git clone https://github.com/Sour-Dev-Home/metaphor-accessibility.git
cd metaphor-accessibility
git config core.hooksPath scripts/githooks
cp .claude/pii-patterns.example.txt .claude/pii-patterns.txt
```

The hooks check each commit and push, running only what the change needs:

- **Before a commit:** the repo rules on staged files, plus formatting, docs and hook signatures when those files are
  staged. Signatures need `METAPHOR_GAME_EXE` set to the game's exe; without it the hook says how to run them. A merge
  that commits itself runs the same checks.
- **On the commit message:** its shape, and the security-review trailer when the commit touches hook or deploy code
  ([below](#security-review-and-deny-rules)).
- **Before a push:** the Release build and the `Unit` and `RoundTrip` tests, when the pushed commits change code. The
  first push after a pull builds from cold and takes longer.

Keep `core.hooksPath` relative, as above: git reads it from each worktree's root, so each worktree runs its own branch's
hooks. `check.sh rules` and `full` warn about an absolute one.

CI runs everything again, so it stays the authority. In `.claude/pii-patterns.txt`, replace
the two example lines with your own private strings, such as your name and email, one per line. It's git-ignored, and
the checks block any commit that contains one. Don't leave the example lines in: they match the example file itself, so
the checks would fail.

Known limitation: each line matches anywhere in a line, ignoring case, with no exceptions. A short pattern, such as a
first name alone, can match a game character, a credited person or part of a longer word, and block a commit that holds
no personal information. Use specific strings (full name, email, user name, phone number). If a real collision blocks
you, reword that line, for example name a character by role; don't weaken or remove the pattern.

## 3. Run the checks

```sh
bash scripts/check.sh fast
bash scripts/check.sh full
```

`fast` checks the staged files, so with nothing staged it passes at once. `full` checks the files changed since `origin/main`
(CI checks every tracked file), then builds,
runs the `Unit` and `RoundTrip` tests, checks formatting and runs the script tests. Both end with exit code 0 when everything passes. A passing `full` is recorded per clone (in the git dir) against the
index's tree hash; `full` or `pre-push` on an identical tree says it reused the pass and skips the work. `pre-push`
takes a machine-wide build slot only when it builds, so a docs-only push never waits on it. Run `full`, then push: with
no recorded pass and every slot busy, `pre-push` stops at once instead of waiting, since a waiting push is cut (exit 141).
[README.md](../README.md#checks) lists every stage.

CI runs `full` on Linux for a pull request (faster; tests marked `WindowsOnly` and the Pester script tests are left to
Windows, and a docs-only change skips the .NET work), and the whole Windows gate in the merge queue. A pull request that
passes can still fail in the queue; run `full` on Windows locally before you push a change near Windows behaviour. Mark a
test that only holds on Windows with `[Trait("Category", "WindowsOnly")]` instead of `Unit`.

`RoundTrip` tests check our format code against real game files, so they need game data and are skipped without it (CI
never has any, per [ADR 0001](../docs/adr/0001-ci-never-sees-game-content.md)). To run them, point `METAPHOR_GAME_DATA` at
your extracted files, or put them in the git-ignored `extracted/` folder. If the variable is set, `extracted/` isn't
checked. Put the [game build](../GLOSSARY.md) on the first line of `game-build.txt` in that folder; the tests show it in
their names.

If you change docs, also install the docs tools (Node.js is needed for the Markdown lint) and run their stage:

```sh
bash scripts/install-docs-tools.sh
bash scripts/check.sh docs
```

A broken link fails it; everything else is a warning. The check-script tests (`tests/check/test-check.sh`) need these
tools too. To flag another filler word, add it to
[`tools/docs/.vale/styles/Project/Filler.yml`](../tools/docs/.vale/styles/Project/Filler.yml).

### Run the check-script suites in WSL

The check-script suites (`tests/check/test-check.sh` and `tests/scripts/*.sh`) start thousands of processes, which is
slow in Git Bash: `test-check.sh` takes minutes there and seconds on Linux
([`run-check-suites.sh`](../scripts/run-check-suites.sh) runs them all). With WSL 2 on your machine, one command
sets up a distro for them. In PowerShell, from the repository root:

```powershell
.\scripts\setup-wsl.ps1
```

It asks once, then creates a WSL distro named `metaphor-checks` from Debian (a download of about 100 MB) and installs
the tools listed in [`scripts/wsl-tools.txt`](../scripts/wsl-tools.txt) inside it, as the distro's root. Your own
distros, WSL's settings and your git config stay as they are. Running it again installs only what's missing;
`-Update` upgrades the distro's packages. On a machine without WSL 2 (no WSL, WSL 1 only, or virtualization off) it
stops with the reason and changes nothing.

From then on, a suite started in Git Bash runs itself in `metaphor-checks`, from your checkout through `/mnt/c`, with
its temporary repositories in the distro's `/tmp`, and exits with the suite's code. Its first line names the tools'
versions. `test-check.sh` uses every core there (`nproc`) instead of Git Bash's 4; `METAPHOR_TEST_JOBS` overrides
both. CI already runs the suites on Linux and doesn't use WSL.

When WSL, the distro or a tool is missing, the suite runs in Git Bash as before and prints a one-line notice naming the
setup command. `METAPHOR_NO_WSL=1` makes it run in Git Bash on purpose. `full` then runs `test-check.sh` only when a
check script or check test changed.

To remove the distro and everything in it: `wsl --unregister metaphor-checks`.

## 4. Make a change

1. Pick an issue, or open one with a template. Issues labelled `ready-for-agent` or `ready-for-human` are ready to start.
2. Work on a branch. `main` only takes pull requests that pass CI.
3. Follow the [coding standards](../docs/coding-standards.md).
4. Commit with a typed subject such as `feat: ...`, and a short body that says why. The commit-msg hook checks the
   shape; the rules are in the header of [`scripts/check.sh`](../scripts/check.sh).
5. Open a pull request that says what changed and why. A PR that touches hooking, loading or injection includes
   in-game evidence on a named game build.
6. Editing a workflow? Actions are pinned by commit SHA, and only an allowed list may run:
   [add or update a GitHub Action](../docs/contributors/how-to/add-or-update-a-github-action.md).

Your branch is deleted automatically after its PR merges. Delete your local copy with `git branch -d <branch>`.

## Security review and deny rules

**Must-run review.** The security-review scope, listed in [`scripts/security-stamp.sh`](../scripts/security-stamp.sh), is:

- the hook layer, `src/Metaphor.Hooks/`, and the scripts that deploy, launch, package or fetch native code, including
  `scripts/onboard.ps1`, its modules and the scripts it runs (it installs tools and runs a test deploy);
- the guards that enforce the ground rules (#306): everything the checks, the git hooks and CI run, transitively, and
  what they read to decide. That is `scripts/check.sh` and every script it or a workflow calls, `scripts/githooks/`,
  the whole `.github/workflows/` folder, the check tests in `tests/check/` and `tests/scripts/` (CI runs them before
  the file scan), the pinned tools in `tools/docs/`, `tools/slow/` and `.config/dotnet-tools.json`,
  `scripts/github-outage.sh` (it classifies push failures and enables auto-merge) and
  `.claude/agents/security-reviewer.md`. A change to them can otherwise merge with only CI passing, and CI is what
  changed. `tests/check/test-check.sh` fails when a script the gate calls falls outside the scope;
- what decides `dotnet restore`, and so the lock files the vulnerability audit reads, in any folder (#322, #324):
  `Directory.Build.*`, `Directory.Packages.props`, `Directory.Solution.*`, `*.sln.targets`, `global.json` and NuGet
  configs. Their targets or feeds could hide a vulnerable package. The audit itself (`scripts/vuln-audit.sh`) reads
  only the committed `packages.lock.json` files, never a project, and CI's build restores in locked mode, so a lock
  file must match its project. Only the root `Directory.Build.props` may set the lock-file properties. `markdownlint` configs may live only in `tools/docs/`: `check.sh docs` fails on any
  other, in any case, because markdownlint loads one from each linted folder and a `.cjs` or `.mjs` one runs code.

A commit touching it needs a review stamp:

1. Stage the change, then run the `security-reviewer` agent. When it finds nothing critical or important, its last step
   runs `scripts/security-stamp.sh`, which hashes the staged scope into a stamp under `.git/`.
2. Commit. The pre-commit hook refuses the commit unless the stamp matches the staged scope, so any edit after the
   review needs a new one. The commit-msg hook appends `Security-Review: <hash>`. The stamp is single-use: the
   post-commit and post-merge hooks remove it once a commit carries its trailer, or commits the stamped scope without one
   (`--no-verify`), and a refused commit keeps it for the retry.
3. CI checks only the pull request's HEAD commit, when the PR's diff touches the scope
   ([ADR 0012](../docs/adr/0012-review-trailer-covers-clean-merges.md), #174, #246). HEAD passes with a
   `Security-Review` trailer matching its own scope content, or through its first parent when HEAD leaves the scope as
   every parent had it or is a clean merge from `main`; the walk passes at a commit already on the base branch, so older
   merges without a trailer don't fail a branch. A commit stamped before the guards joined the scope carries a hash of
   the old scope only; CI still accepts it when the commit changes no guard file, so earlier reviews stay valid.

A **clean merge from `main`** needs no stamp, and the commit-msg hook says so and adds no trailer. Its second parent is
already on `origin/main`, and each scope file is either the second parent's version where the branch left it as their
merge base had it, or the first parent's where `main` left it alone. A scope conflict you resolve, a file git merges
from both sides' changes, and a side kept over the other's change (`-s ours`, `checkout --ours` or `--theirs`) all
need a stamp.

**Override (maintainer only).** `SECURITY_REVIEW_SKIP="<reason>" git commit ...` lets the commit through and records
`Security-Review-Skipped: <reason>`, which CI accepts. Agents are denied it.

**Everyone else's PRs (the `security-reviewed` label, #127, #309).** CI treats a PR as the maintainer's when its author
is the repository's owner or has admin permission (in an organization's repository the maintainer's PRs show
`MEMBER`). Every other PR, collaborators' and members' included, works like this:

- Its HEAD needs the `Security-Review` trailer when it touches the scope, as above. A `Security-Review-Skipped` trailer
  doesn't count.
- If it changes anything outside `docs/`, CI also fails until a maintainer reviews it (running the `security-reviewer`
  agent when it touches the scope) and adds the `security-reviewed` label. The label counts only if the latest label
  event was added by an admin, after the PR's latest push; a push after the review needs a new review and the label
  added again. The `review-gate` check runs again when the label goes on, so add it last: another label added after
  it means adding it again. A docs-only PR needs no label.
- Agents working for a collaborator follow the same rules as their hand-written PRs. Auto-merge only lands once this
  gate passes.

The label gate is its own workflow, [`review-gate.yml`](workflows/review-gate.yml) (#330). It runs on
`pull_request_target`, so GitHub runs `main`'s copy of it, and `main`'s `scripts/check.sh review-label`; a PR can't edit
or delete it. It reads the PR's file list, timeline and head through the API with a read-only token and never checks
out or runs the PR's code. It judges the whole diff, so a change made inside a merge commit counts. The trailer check
in `repo-rules` runs the base branch's copy of `scripts/check.sh` and `scripts/security-stamp.sh`, first in its job.
The PR can still edit `ci.yml`, or add a job of its own named `review-gate` that passes, since the ruleset requires the
check by name. So read a workflow change before approving a fork's CI run or labelling it (the remaining gap:
[security-model.md](../docs/contributors/explanation/security-model.md#known-limits)).

**Collaborators.** A collaborator has the `write` role: they push branches here and merge through the merge queue. The
`main` ruleset keeps 0 required approvals and no code-owner review, so the maintainer's and agents' PRs keep flowing;
the label above is the review gate. 2FA isn't required. Only admins create, update or delete `v*` tags. Workflow runs
from forks need the maintainer's approval for every outside contributor, not only first-timers (Actions settings). The private patterns list
(`PII_PATTERNS`) is a secret of the `main-push` environment, which only `main` may use, so no branch's run can read it.
Before their first PR, a collaborator's clone uses the repository's git hooks and their own private-patterns file
([step 2](#2-clone-and-set-up)).

**Deny rules.** [`.claude/settings.json`](../.claude/settings.json) denies Claude Code every force push, printing
`.claude/pii-patterns.txt` (the Read tool, `cat`, `type`, `Get-Content`, `head`, `tail`, `less`), setting
`SECURITY_REVIEW_SKIP`, and skipping the git hooks with `--no-verify` (or `git commit -n`). `scripts/check.sh` and a `grep -F -f` over the patterns file stay allowed. The maintainer's
private `~/.claude-metaphor/settings.json` also denies edits and common shell writes inside the game's install folder
and its save folder.

These rules match the command text Claude Code sees, so another spelling of the same command can slip past them. The
stamp guards against a forgotten review, not a determined one: anyone can run the stamp script, and CI checks the
trailer, not that a review happened. They are a strong guard, not a guarantee.

## Contributing without Claude

You don't need Claude Code or any agent tooling. The [ground rules in `AGENTS.md`](../AGENTS.md#ground-rules) apply to
you as they do to agents.

- **Pick an issue.** Any open issue not labelled `needs-game` works
  ([filter](https://github.com/Sour-Dev-Home/metaphor-refantazio/issues?q=is%3Aopen+-label%3Aneeds-game)). There is no
  `good first issue` label. Comment on the issue before you start.
- **Labels.** [`docs/agents/triage-labels.md`](../docs/agents/triage-labels.md) has the full table.
  - `ready-for-agent` and `ready-for-human` both mean the issue is ready to start. The names say who the maintainer
    expected to do it; they don't stop you.
  - `needs-triage`: the maintainer hasn't evaluated it yet. Ask before starting.
  - `needs-info`: waiting on the reporter. Skip it unless you can supply the answer.
  - `needs-game`: you need your own copy of the game to do or check the work.
- **The skills are optional.** The Matt Pocock skills are how the maintainer writes issues and runs agents. You can
  work and write issues by hand.
- **The game.** Most work needs your own copy of the game, to check offsets, formats and in-game behaviour. Without
  it you can still do docs, tooling and anything the unit tests cover.
- **Security label.** A pull request that changes anything outside `docs/` gets the maintainer's `security-reviewed`
  label after review ([the gate](#security-review-and-deny-rules)).

## Where things go

- Notes on what the game does: [`docs/findings/`](../docs/findings/README.md), citing sources saved in
  [`docs/raw-sources/`](../docs/raw-sources/README.md).
- Decisions: [`docs/adr/`](../docs/adr/). Terms: [`GLOSSARY.md`](../GLOSSARY.md).
- Docs for players and contributors: [`docs/`](../docs/README.md).

Report security problems privately, as [SECURITY.md](SECURITY.md) explains. Everyone here follows the
[code of conduct](CODE_OF_CONDUCT.md).

By contributing, you agree that your work is licensed as [README.md](../README.md#license) describes.
