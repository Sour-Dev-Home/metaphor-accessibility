# Reloaded-II's mod updater, for a blind player

Keep updating through our installer. Reloaded-II's own updater never runs on the path a blind player uses, shows its
prompt in a window nobody has checked with a screen reader, and verifies nothing it downloads. The question is #632;
the facts and their sources are in [the raw source](../../raw-sources/reloaded-ii-mod-updater-2026-11-24.md), and
where it says [NEEDS VERIFICATION], so does this page.

## Why not adopt it

- **It doesn't check on our start.** Reloaded-II checks mods for updates when its launcher window opens, or on demand
  in that window. Our desktop shortcut starts the game with `--launch`
  (`scripts/player/MetaphorInstall.psm1`, step 9), which starts the game and exits before the window or its update
  check ever exists. A player would have to open Reloaded-II's window to be offered an update, which is the window
  the install guide keeps them out of.
- **Its prompt is unchecked by ear.** The update dialog is a modal WPF window: a grid of checkbox rows (one mod per
  row), a download button and a changelog viewer, with no accessibility names set. It might read well in NVDA; nobody
  knows.
- **It verifies nothing.** It downloads the release asset from the URL GitHub's API returns, with no hash or signature
  check. Our installer checks the release's attestation (#309), so switching would lose that. It also downloads
  missing dependencies without asking.

## What it would take, if it's ever wanted

The config part is small: a `GitHubRelease` entry in `ModConfig.json` naming the repository, semver release tags, and
an asset matching `AssetFileName` (an ordinary zip appears to work without Reloaded-II's own release metadata). That
makes it a cheap extra for sighted players who use Reloaded-II's window. Before that: check that a plain zip of the
mod folder updates correctly, try the dialog with NVDA, and decide whether an unverified download path beside the
attested installer is acceptable. That decision is the maintainer's (security posture), so no follow-up issue is
filed here.

Today's path stays: the title-screen notice (#576) says a newer version exists, and its installer updates in place,
keeping settings.
