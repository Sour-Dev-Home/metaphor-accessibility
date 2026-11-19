# Handle a GitHub outage

For anyone pushing work while GitHub is down. Commands are in
[`scripts/ci/github-outage.sh`](../../../scripts/ci/github-outage.sh) (#312).

1. A failed `git push`, `gh` call or CI run: pipe its output to `bash scripts/ci/github-outage.sh classify <exit code>`.
   It prints `outage` (a 5xx, a timeout, no connection) or `real` (auth, a rejected push, a failed hook, a failing
   check). Confirm an outage with `bash scripts/ci/github-outage.sh status`; anything but `none` is one.
2. `real`: fix it as usual. Only an outage is parked.
3. Outage: retry with backoff for about 10 minutes (for example at 1, 2, 3 and 4 minutes). Still down:
   `bash scripts/ci/github-outage.sh park <branch> <issue> --title "<PR title>" --body-file <file>`, report
   `parked: GitHub outage` to the main session and stop. The commit stays on its branch.
4. Keep working on what needs no GitHub: local commits, `check.sh`, security review and stamp.
5. Actions down, git up: push and open the PR as usual. The merge queue waits for CI; don't re-run or re-push to nudge it.
6. Never pass `--no-verify`, merge into `main` locally or edit a check to get past an outage.
7. The main session resumes after the outage with `bash scripts/ci/github-outage.sh resume`. It pushes each parked
   branch, opens the PR, enables auto-merge, reports each result and is safe to run twice. `bash scripts/check/check-status.sh`
   lists what is parked.
