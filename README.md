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

One command runs every check, locally (Git Bash on Windows) and in CI:

| Stage | Runs | Where |
|---|---|---|
| `scripts/check.sh fast` | repo rules on staged files | pre-commit hook |
| `scripts/check.sh rules` | repo rules on every tracked file | CI on Ubuntu |
| `scripts/check.sh full` | repo rules, then the .NET build, `Unit` tests and formatting | CI on Windows |

`full` needs the .NET SDK pinned in `global.json`. CI then packages the build it tested (`scripts/package-mod.sh`) and
keeps it as a workflow artifact with its SHA-256. It also rebuilds the mod in a second checkout at another path on the
same runner and checks the bytes match. A local package won't match CI's hash, because CI-only build settings remove
machine paths; the CI artifact is the one that counts.

Turn on the committed git hooks once per clone:

```sh
git config core.hooksPath .githooks
```

The checks block game content and personal information. For the second, copy `.claude/pii-patterns.example.txt` to
`.claude/pii-patterns.txt` (git-ignored) and list your own private strings. CI checks general patterns on every push and pull
request, and the private list on pushes to `main`, from a repository secret. To set it, a maintainer runs:

```sh
gh secret set PII_PATTERNS < .claude/pii-patterns.txt
```

Pushes to `main` fail until the secret is set. The check never prints a matching line or pattern, because CI logs are
public.
