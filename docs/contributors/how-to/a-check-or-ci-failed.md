# A check or CI failed

Find your symptom below. Each entry says what you see, why, and what to do; the rule's own page holds the detail. The
same `scripts/check.sh` runs locally and in CI, so a local failure is the one CI would give.

## The security-review stamp doesn't match, or a commit has no review trailer

- **You see:** the pre-commit hook says `security review: the staged hook or deploy code has no matching stamp`, or CI's
  `repo-rules` says `commit <sha>: touches the security-review scope without a Security-Review trailer matching its
  content`.
- **Why:** the commit changes a file in the security-review scope, and no stamp from the `security-reviewer` agent
  matches it. Any edit after the review voids the stamp, and the stamp is single-use.
- **Do:** stage the change, run the `security-reviewer` agent again, and commit. If CI complains about an earlier
  commit, amend or redo that commit with a fresh stamp. Never use `SECURITY_REVIEW_SKIP`; it is the maintainer's.
  [The stamp](../reference/security-review.md#the-stamp).
- **Also:** `review-gate` fails on a PR from anyone but the maintainer that changes anything outside `docs/`, until the
  maintainer adds `security-reviewed`. Wait for it
  ([the label gate](../reference/security-review.md#everyone-elses-pull-requests)).

## A game-content block

- **You see:** `game content: <path>: <reason>`, where the reason is `inside a game folder`, `game archive extension`,
  `disassembler database extension`, `larger than 1048576 bytes`, `hash matches a known game file` or similar.
- **Why:** [rule 4](../../../AGENTS.md#ground-rules): executables, archives, extracted assets, dumps and Ghidra
  databases are never committed.
- **Do:** unstage the file (`git restore --staged <path>`) and keep it in a git-ignored folder (`game/`, `extracted/`,
  `dumps/`, `captures/`, `out/ghidra/`). If a note needs it, quote a short snippet or save counts and offsets in a
  [raw source](write-a-finding.md). Don't rename the file to get past the check. A file already in a commit you haven't pushed: reset
  that commit and commit again without it; one already pushed: tell the maintainer.
  [Content rules](../reference/checks.md#content-rules).

## A personal-information block

- **You see:** `personal information: <path>:<line>: <reason>`, such as an email address, a user folder path
  (`C:\Users\...`) or a file name matching your private patterns. The line's text is never printed.
- **Why:** [rule 6](../../../AGENTS.md#ground-rules). Your private patterns live in `.claude/pii-patterns.txt`.
- **Do:** replace the match with a repository-relative path or an environment variable and stage again. Where an
  output has your path in it, shorten it (`<mods>`, `~`). A short private pattern that matches an ordinary word:
  [reword the line, never remove it](set-up-a-clone.md#2-clone-and-set-up). CI runs the private list only on pushes to
  `main`, so a clean PR can still fail after the merge; run the check locally with your patterns set.

## A commit message was rejected

- **You see:** `subject needs a type prefix`, `subject is <n> characters; the limit is 72`, `leave a blank line after the
  subject` or `body has <n> lines; the limit is 5`.
- **Do:** for the latest commit, `git commit --amend` with a message of the right shape. CI checks every commit in the
  PR, so for an older one, `git reset --soft <base>` and commit again. [Commit messages](../reference/commit-messages.md).

## markdownlint or a link check failed

- **You see:** `broken link: <file>:<line>: <target>` or `relative link: .github/<file>:<line>: <target>`, from
  `bash scripts/check.sh docs`.
- **Why:** a relative link, an anchor or a link to this repository's `blob/main/` or `tree/main/` points at nothing.
  Renaming a heading breaks every anchor to it. Markdown under `.github/` links absolutely, since GitHub also shows it
  outside its folder, where a relative link 404s.
- **Do:** fix the link and rerun `bash scripts/check.sh docs`. Markdown lint, Vale and long ADRs only warn; they never
  fail a run, but a warning is worth fixing. [Checks](../reference/checks.md).

## No CI run started

- **You see:** no checks on the PR five minutes after a push.
- **Why:** GitHub sometimes starts no run at all (#329).
- **Do:** `gh pr close <n> && gh pr reopen <n>`, then turn auto-merge on again with `gh pr merge <n> --auto`.
  If GitHub itself is failing (5xx, timeouts): [handle a GitHub outage](handle-a-github-outage.md).

## The merge queue dropped the PR

- **You see:** the PR passed its checks, then left the queue with a failed run.
- **Why:** the queue re-runs everything on Windows, merged with the PRs ahead of yours, which the pull-request run
  doesn't. A Windows-only failure, or a clash with a PR ahead of you, shows only there.
- **Do:** open the failed run in the queue's check, fix the cause, run `bash scripts/check.sh full` locally, push, and
  run `gh pr merge <n> --auto` again. A change that needs a stamp and conflicts with `main` needs a fresh one
  ([the stamp](../reference/security-review.md#the-stamp)). [In CI](../reference/checks.md#in-ci),
  [branch rules](../reference/branch-rules.md).
