# Licences of the pinned check tools

- **Source:** GitHub's licence detection, fetched with `gh api repos/<owner>/<repo>/license` (GitHub CLI) for each
  repository's default branch, and `gh api repos/lycheeverse/lychee/contents` for lychee's licence files.
- **Saved:** 2026-11-19
- **Game build:** none (these are check tools, not the game)

The licence of each repository's default branch on the date above, not of the pinned release; none of these tools is
shipped. The output, one line per repository (repository, licence file, SPDX id, file blob):

```text
google/osv-scanner LICENSE Apache-2.0 d645695673349e3947e8e5ae42332d0ac3164cd7
lycheeverse/lychee LICENSE-APACHE Apache-2.0 f51e79e40005e667e15829988aa8471e3a8e68f1
errata-ai/vale LICENSE MIT 8ed286bfc811f1fe0968e9fda6a3072be5b3c28c
DavidAnson/markdownlint-cli2 LICENSE MIT 75c778e52d8dea682c9a25a9950cebd1b74afc69
```

lychee's root also holds `LICENSE-MIT` beside `LICENSE-APACHE`; GitHub's detection reports only the Apache file.
