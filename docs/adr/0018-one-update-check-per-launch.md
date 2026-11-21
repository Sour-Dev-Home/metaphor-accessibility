---
status: accepted
---

# The mod's one network request: an update check per launch, which the player can turn off

Decided by the maintainer on 2026-11-21 (#576).

The mod may make one network request per game launch: GitHub's public REST API, unauthenticated, for this repository's
releases (`GET https://api.github.com/repos/Sour-Dev-Home/metaphor-accessibility/releases`). If the newest release,
pre-releases included, is newer than the running mod by semantic-version order, the title screen says so once. Nothing
else is allowed on the network.

## Scope

- **Nothing sent but the request.** No token, cookie, body or telemetry. The User-Agent GitHub requires names the
  project (`metaphor-accessibility`), never the player, the machine or the mod's version.
- **This repository only.** The address is a constant. Redirects aren't followed, and the answer is capped at 1 MB.
- **Off the game thread.** A pool thread, a 5-second timeout. Any failure says nothing; the outcome is logged in Debug
  builds only.
- **One assembly.** Only `Metaphor.Features.Updates` may use `System.Net` (architecture rule 3, `Rules.cs`). It is in
  the security-review scope (`scripts/ci/security-stamp.sh`).

## The off switch

`"checkForUpdates": false` in the mod's `settings.json`, beside `voicedLines`, stops the request. It is read at launch.
If it's changed during the session, the new value applies to the notice at once and to the request from the next
launch. It defaults to on. Player docs: [turn off the update check](../players/how-to/turn-off-the-update-check.md).

## Considered options

- **The `releases/latest` endpoint.** It skips pre-releases, and every release so far is one (`v0.0.1-unstable.N`).
- **An on-demand check from a key**, like KOTOR Access's: the player has to remember to ask. Kept as a possible later
  addition.
- **No check.** Players on an old build report bugs already fixed, and blind players can't easily watch a releases page.

## Consequences

The version compared is `ModVersion` in `ModConfig.json`. A release must carry the version of its tag there, or the
notice is wrong: as of this decision `ModConfig.json` says `0.1.0`, which sorts after every `0.0.1-unstable.N` tag, so
no notice is said until the two agree.
