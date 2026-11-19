# Merge main into a draft PR

For authors of an open draft PR. Integration branches are the heads of open draft PRs. When `main` moves, the
[`Conflict alert`](../../../.github/workflows/conflict-alert.yml) workflow runs the sync pass of
[`scripts/ci/conflict-alert.sh`](../../../scripts/ci/conflict-alert.sh) (#242): it test-merges `main` into each draft PR's
branch in memory and comments once, with either the conflicting files or "mergeable cleanly, needs a merge from `main`
(and a review if hook files change)". It stays quiet while the outcome is unchanged. CI never pushes the merge: one that
resolves a conflict in the security-review scope needs a fresh stamp, and CI can't give one. A clean merge needs none
([the stamp](../reference/security-review.md#the-stamp)). When you see the comment, merge
`main` into the branch yourself, following
[the agent workflow](../../agents/workflow.md#hook-loading-install-or-update-code-and-the-guards) if you resolve a
conflict in hook files.
