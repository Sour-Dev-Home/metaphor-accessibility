# How a change reaches main

Every change takes the same path. The same script, [`scripts/check.sh`](../../../scripts/check.sh), runs in the local
git hooks and in CI, so a check that passes on your machine passes the same way on GitHub.

```mermaid
flowchart LR
    subgraph local["Your machine · every branch"]
        direction TB
        change["Code change"] --> precommit["pre-commit hook<br/><small>repo rules · format</small>"]
        precommit --> commitmsg["commit-msg hook<br/><small>message · trailer</small>"]
        commitmsg --> prepush["pre-push hook<br/><small>build · tests</small>"]
        prepush --> beforepr["Before the PR<br/><small>full check · security review</small>"]
    end

    subgraph pr["Pull request"]
        direction TB
        opened["PR opened<br/><small>auto-merge on</small>"] --> checks
        subgraph checks["5 required checks, in parallel"]
            direction LR
            rules["repo-rules"]
            build["build"]
            scripts["scripts"]
            security["security"]
            gate["review-gate"]
        end
    end

    subgraph queue["Merge queue"]
        direction TB
        again["Same 5 checks<br/><small>on latest main</small>"] --> squash["Squash merge<br/><small>branch deleted</small>"]
    end

    subgraph mainzone["main"]
        direction TB
        main(["main"])
        main -.-> conflict["Conflict alert<br/><small>every 6 h</small>"]
        main -.-> scorecard["Scorecard<br/><small>weekly</small>"]
        main -.-> slow["Slow suite<br/><small>weekly</small>"]
    end

    beforepr ==> opened
    checks ==> again
    squash ==> main

    classDef gate stroke:#c25e0c,stroke-width:3px
    class precommit,commitmsg,prepush,beforepr,rules,build,scripts,security,again gate
```

An orange outline means a failure stops the change. A dotted line means the job only reports.

| Stage | What runs | Where it's defined |
| --- | --- | --- |
| Hooks | `check.sh pre-commit`, `commit-msg`, `pre-push` | [`scripts/githooks/`](../../../scripts/githooks/) |
| Before the PR | `check.sh full`; the security review for code in its scope | [`CONTRIBUTING.md`](../../../.github/CONTRIBUTING.md#3-run-the-checks) |
| Pull request and merge queue | `repo-rules`, `build`, `scripts`, `security`; `review-gate` from `main`'s copy | [`ci.yml`](../../../.github/workflows/ci.yml), [`review-gate.yml`](../../../.github/workflows/review-gate.yml) |
| After merge | conflict alert, Scorecard, slow suite | [`.github/workflows/`](../../../.github/workflows/) |

These are always on, from the repository settings: SHA-pinned actions from an allowed list
([how to pin one](../how-to/add-or-update-a-github-action.md)), Dependabot, CodeQL, secret scanning with push
protection, and the ruleset on `main`. The ruleset accepts pull requests only, requires the five checks and merges
through the queue.
