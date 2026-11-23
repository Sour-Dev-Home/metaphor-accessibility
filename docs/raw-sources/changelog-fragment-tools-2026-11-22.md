# Changelog fragment tools: towncrier and scriv, checked for #758

- **Source:** GitHub REST API (`gh api repos/<owner>/<repo>`, `.../releases/latest`, `.../contents/pyproject.toml`) on
  `twisted/towncrier` and `nedbat/scriv`.
  - <https://github.com/twisted/towncrier>
  - <https://github.com/nedbat/scriv>
- **Saved:** 2026-11-22.
- **Game build:** none (build tooling, not the game). Nothing was installed or run; no code copied.

## What was found

| Tool | Licence | Latest release read | Runtime needs |
| --- | --- | --- | --- |
| towncrier | MIT | 26.9.0 (2026-09-04) | Python >= 3.11; `click`, `jinja2` |
| scriv | Apache-2.0 | 1.8.2 (2026-11-15) | Python >= 3.10 (plus `tomli` below 3.11 for TOML) |

Both keep one fragment file per change in a folder (towncrier `newsfragments/<id>.<type>.md`, scriv
`changelog.d/<date>_<name>.md`) and join them into the changelog at release. Both licences are GPL-3.0-compatible.

## Decision

Neither is adopted. The repository has no Python tool stack (checks are bash and PowerShell); either tool would add a
Python runtime and two or more pinned packages to every contributor's setup and to CI, for what is a join of a few
Markdown files. Our entries are hand-wrapped bullets that the changelog keeps verbatim, and towncrier's and scriv's
templates would rewrite them. A small script, `scripts/release/join-changelog.sh`, does the join. The fragment naming,
`<id>.<kind>.md` in `changelog.d/`, follows towncrier's convention (idea credited in `CREDITS.md`; no code taken).
