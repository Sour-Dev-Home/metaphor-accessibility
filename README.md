# metaphor-refantazio

Mods, tools and reverse-engineering notes for Metaphor: ReFantazio on PC.

Status: just started. Nothing here is usable yet.

## What's here, and what isn't

- **Here:** our own code, and notes on the game's formats and engine, each pinned to the game build it was checked on.
- **Not here:** anything from the game itself (executables, archives, assets, decompiled code). You need your own copy of
  the game.

This project is single-player modding only. It doesn't touch online features, DRM or anti-tamper protection.

Metaphor: ReFantazio is a trademark of its owners. This project is unofficial and not affiliated with them.

## Checks

`scripts/check.sh fast` checks staged files; `scripts/check.sh full` checks every tracked file, as CI does. It runs in
Git Bash on Windows. Turn on the committed git hooks once per clone:

```sh
git config core.hooksPath .githooks
```
