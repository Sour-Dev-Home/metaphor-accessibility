# Branch rules

All changes reach `main` through pull requests that pass CI, then a merge queue that re-runs CI on each PR merged with
the ones ahead of it, so a PR never has to be updated by hand when `main` moves. Auto-merge is on: `gh pr merge --auto`
queues a PR as soon as its checks pass. The ruleset is exported in
[`.github/rulesets/main.json`](../../../.github/rulesets/main.json), which is the source of truth. It requires no
approving review, because a sole maintainer can't approve their own PR. Admins can bypass it, and GitHub logs each
bypass in the ruleset's insights. [`.github/CODEOWNERS`](../../../.github/CODEOWNERS) marks the code that needs the
owner's review.

The private personal-information list only reaches pushes to `main`, so a PR's checks don't run it, and a match shows
up after the merge.

A maintainer applies the export with `POST` the first time, then `PUT` to the id that `gh api
repos/{owner}/{repo}/rulesets` lists:

```sh
gh api -X POST repos/{owner}/{repo}/rulesets --input .github/rulesets/main.json
gh api -X PUT repos/{owner}/{repo}/rulesets/<id> --input .github/rulesets/main.json
```
