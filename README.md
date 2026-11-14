# metaphor-refantazio

Mods, tools and reverse-engineering notes for Metaphor: ReFantazio on PC.

Status: just started. Nothing here is usable yet.

## What's here, and what isn't

- **Here:** our own code, and notes on the game's formats and engine, each pinned to the game build it was checked on.
- **Not here:** anything from the game itself (executables, archives, assets, decompiled code). You need your own copy of
  the game.

This project is single-player modding only. It doesn't touch online features, DRM or anti-tamper protection.

Metaphor: ReFantazio is a trademark of its owners. This project is unofficial and not affiliated with them.

To contribute, start with [CONTRIBUTING.md](CONTRIBUTING.md).

## License

Code is under [GPL-3.0](LICENSE). Docs (the Markdown files, including everything in `docs/`) are under
[CC BY 4.0](docs/LICENSE), except `docs/raw-sources/`, which holds copies of other people's work under their own terms.

## Checks

One command runs every check, locally (Git Bash on Windows) and in CI:

| Stage | Runs | Where |
|---|---|---|
| `scripts/check.sh fast` | repo rules on staged files | pre-commit hook |
| `scripts/check.sh rules` | repo rules on every tracked file | CI on Ubuntu |
| `scripts/check.sh full` | repo rules, then the .NET build, `Unit` tests and formatting | CI on Windows |
| `scripts/check.sh commit-msg FILE` | one commit message's shape (see the script's header) | commit-msg hook |
| `scripts/check.sh commits BASE..HEAD` | the same, for each commit in a pull request | CI on Ubuntu |

`full` needs the .NET SDK pinned in `global.json`. CI then packages the build it tested (`scripts/package-mod.sh`) and
keeps it as a workflow artifact with its SHA-256. It also rebuilds the mod in a second checkout at another path on the
same runner and checks the bytes match. A local package won't match CI's hash, because CI-only build settings remove
machine paths; the CI artifact is the one that counts.

Turn on the committed git hooks once per clone:

```sh
git config core.hooksPath .githooks
```

The checks also fail a raw source without its header ([`docs/raw-sources/`](docs/raw-sources/README.md)) and a
finding without its game build ([`docs/findings/`](docs/findings/README.md)).

The checks block game content and personal information. For the second, copy `.claude/pii-patterns.example.txt` to
`.claude/pii-patterns.txt` (git-ignored) and list your own private strings. CI checks general patterns on every push and pull
request, and the private list on pushes to `main`, from a repository secret. To set it, a maintainer runs:

```sh
gh secret set PII_PATTERNS < .claude/pii-patterns.txt
```

Pushes to `main` fail until the secret is set. The check never prints a matching line or pattern, because CI logs are
public.

## Supply chain

Workflows use read-only tokens by default, and a job that writes asks for it itself. Every action is pinned to a
commit. [Dependabot](.github/dependabot.yml) proposes NuGet and Actions updates weekly. CI's `security` job, which the
ruleset requires, runs CodeQL and fails on any package with a known vulnerability. The ruleset also blocks a PR that
adds a high or critical CodeQL security alert; other alerts show in the Security tab without blocking. To unblock a
false positive, dismiss the alert there with a reason.
[Scorecard](.github/workflows/scorecard.yml) runs weekly as a report only; its results are in the Security tab.
`tests/scripts/test-repo-config.sh` checks these rules.

## Branch rules

All changes reach `main` through pull requests that pass CI. The ruleset is exported in
[`.github/rulesets/main.json`](.github/rulesets/main.json), which is the source of truth. It requires no approving
review, because a sole maintainer can't approve their own PR. Admins can bypass it, and GitHub logs each bypass in the
ruleset's insights. [`.github/CODEOWNERS`](.github/CODEOWNERS) marks the code that needs the owner's review.

The private personal-information list only reaches pushes to `main`, so a PR's checks don't run it, and a match shows
up after the merge.

A maintainer applies the export with `POST` the first time, then `PUT` to the id that `gh api
repos/{owner}/{repo}/rulesets` lists:

```sh
gh api -X POST repos/{owner}/{repo}/rulesets --input .github/rulesets/main.json
gh api -X PUT repos/{owner}/{repo}/rulesets/<id> --input .github/rulesets/main.json
```
