# Contributor docs

For people working on the code, the notes or the docs. Start with [`CONTRIBUTING.md`](../../.github/CONTRIBUTING.md):
in PowerShell, `.\scripts\onboard.ps1` sets up a fresh clone in one command
([how](../../.github/CONTRIBUTING.md#one-command); on a fresh Windows install, allow scripts first).
[`../README.md`](../README.md) says which type of page goes in which folder.

How-tos: [add a hook](how-to/add-a-hook.md), [deploy a dev build](how-to/deploy-a-dev-build.md), [run the smoke test](how-to/run-the-smoke-test.md),
[add a game build](how-to/add-a-game-build.md), [check hook signatures](how-to/check-hook-signatures.md),
[turn on the dialogue window hooks](how-to/turn-on-the-dialogue-window-hooks.md),
[query the Ghidra project](how-to/query-the-ghidra-project.md), [add or update a GitHub Action](how-to/add-or-update-a-github-action.md),
[cut a release](how-to/cut-a-release.md).

Explanation: [how a change reaches main](explanation/how-a-change-reaches-main.md), a diagram of the hooks, CI and the
merge queue; [security model](explanation/security-model.md), each security decision and what it protects.
