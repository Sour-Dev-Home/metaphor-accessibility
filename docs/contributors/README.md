# Contributor docs

For people working on the code, the notes or the docs. Start with [`CONTRIBUTING.md`](../../.github/CONTRIBUTING.md):
in PowerShell, `.\scripts\setup\onboard.ps1` sets up a fresh clone in one command
([how](../../.github/CONTRIBUTING.md#one-command); on a fresh Windows install, allow scripts first).
[`../README.md`](../README.md) says which type of page goes in which folder.

How-tos: [add a hook](how-to/add-a-hook.md), [deploy a dev build](how-to/deploy-a-dev-build.md), [run the smoke test](how-to/run-the-smoke-test.md),
[add a game build](how-to/add-a-game-build.md), [check hook signatures](how-to/check-hook-signatures.md),
[turn on the dialogue window hooks](how-to/turn-on-the-dialogue-window-hooks.md),
[inspect the running game](how-to/inspect-the-running-game.md),
[query the Ghidra project](how-to/query-the-ghidra-project.md), [add or update a GitHub Action](how-to/add-or-update-a-github-action.md),
[cut a release](how-to/cut-a-release.md), [handle a GitHub outage](how-to/handle-a-github-outage.md),
[merge main into a draft PR](how-to/merge-main-into-a-draft-pr.md).

Reference: [checks](reference/checks.md), every check stage and how CI runs them; [commit messages](reference/commit-messages.md),
the subject, body and trailer rules; [supply chain](reference/supply-chain.md);
[branch rules](reference/branch-rules.md), the ruleset and the merge queue.

Explanation: [how a change reaches main](explanation/how-a-change-reaches-main.md), a diagram of the hooks, CI and the
merge queue; [security model](explanation/security-model.md), each security decision and what it protects.
