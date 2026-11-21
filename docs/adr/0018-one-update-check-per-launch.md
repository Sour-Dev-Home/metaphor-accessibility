---
status: accepted
---

# The mod's one network request: an update check per launch, which the player can turn off

Decided by the maintainer on 2026-11-21 (#576, #628).

Once per launch the mod asks GitHub's public REST API, unauthenticated, for this repository's releases. If the newest,
pre-releases included, is newer than the running mod by semantic-version order, the title screen says so once. Nothing
else goes on the network.

## Scope

- **Nothing sent but the request.** No token, cookie, body or telemetry. The User-Agent names the project only.
- **This repository only.** A constant address, no redirects, an answer capped at 1 MB, tags over 64 characters
  skipped.
- **Off the game thread.** A 5-second timeout; failures say nothing and are logged in Debug builds only.
- **One assembly, HTTP only.** `Metaphor.Features.Updates` may use `System.Net.Http` and nothing else in `System.Net`
  (architecture rule 3, `Rules.cs`). It is in the security-review scope.

## The off switch

`"checkForUpdates": false` in `settings.json`, beside `voicedLines`, stops the request from the next launch and the
notice at once. On by default. [Player docs](../players/how-to/turn-off-the-update-check.md).

## ModVersion matches the tag

The mod compares `ModVersion` in `ModConfig.json` with the tags, so it is bumped to the next release's version before
tagging, and the release gate refuses a tag that isn't `v` plus `ModVersion`
([cut a release](../contributors/how-to/cut-a-release.md)).

## Considered options

- **`releases/latest`**: skips pre-releases, and every release so far is one.
- **An on-demand key**: players must remember to ask.
- **No check**: blind players can't easily watch for releases.
