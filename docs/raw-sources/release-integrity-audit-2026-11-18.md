# Release integrity: what exists, what's missing (#308)

- **Source:** this repository at `origin/main` 0da870e, read on the date below (file:line references are to that
  commit); GitHub issues #283, #303 and #309 read with `gh issue view`; NuGet's NU1403 reference,
  <https://learn.microsoft.com/en-us/nuget/reference/errors-and-warnings/nu1403> (docs commit `107b92ee`), read with a
  fetch tool on the same date.
- **Saved:** 2026-11-18
- **Game build:** none (this note is about our build and release pipeline, not the game)

Each point is one of #308's five. Verdict first, then the evidence.

## 1. Releases are built only by CI from a tagged commit on `main`

**Missing: there is no release workflow.** `.github/workflows/` holds `ci.yml`, `conflict-alert.yml`, `scorecard.yml`
and `slow.yml`; none triggers on a tag or creates a release. What exists is the build half:

- `ci.yml:96-97, 132-146`: in the merge queue and on `main`, the Windows gate packages the Release build it just tested
  (`scripts/package-mod.sh --no-build`) and fails unless `Metaphor.Hooks.dll` names `$GITHUB_SHA` as a clean build.
- `ci.yml:148-160`: a second checkout at another path rebuilds from scratch and must give the same package hash.
- `ci.yml:161-166`: the package is uploaded as the run artifact `mod-<sha>`.
- `Directory.Build.props:20-23`: deterministic builds, `ContinuousIntegrationBuild` in CI only, so only CI's hash is
  the reference.

Filed as #310 (release workflow).

## 2. Each release artifact has an attestation and a published SHA-256; players' docs say how to check

**Partly.** `ci.yml:145-146` writes the package's SHA-256 to the run log and summary. That hash is `scripts/tree-hash.sh`
(lines 7, 14-17): a hash of a sorted per-file manifest, not of a downloadable file, so a player can't check it with one
standard command. No workflow calls `actions/attest-build-provenance`, and no players' doc covers checking a download.
All in #310.

## 3. Every bundled third-party binary is pinned and verified against a recorded hash; a mismatch fails the build

**Exists.** The package holds two kinds of third-party binary:

- **Prism (`prism.dll` and its licences).** `src/Metaphor.Hooks/prism.json:2-7` pins version v0.18.3, the URL, the zip's
  SHA-256 and the DLL's SHA-256. `scripts/fetch-prism.ps1:39-49` refuses a zip whose hash doesn't match (exit 1),
  `:53-66` extracts only the pinned DLL path and `LICENSES/` and refuses entries escaping the output folder, and `:73-77`
  refuses a DLL whose hash doesn't match (exit 1). `src/Metaphor.Hooks/Metaphor.Hooks.csproj:40-41` runs it in every
  publish through an MSBuild `Exec`, so exit 1 fails the package. `tests/smoke/fetch-prism.Tests.ps1:86, 96, 108` test
  the three refusals. At run time `src/Metaphor.Hooks/PrismSpeech.cs:80-84` checks the DLL against the same pin again,
  holding the file open from the check to the load. ADR 0005 (`docs/adr/0005-prism-speech-backend.md:7-8, 24-25`)
  records the pin and the re-pin on upgrade; `CREDITS.md:12` names the version.
- **NuGet packages that ship (Reloaded.SharedLib.Hooks, Reloaded.Hooks.Definitions).** `Directory.Packages.props:11, 13`
  pins their versions centrally (`CREDITS.md:11` names them). `Directory.Build.props:27-28` turns on lock files and, in
  CI, locked mode; each project's `packages.lock.json` records every package's `contentHash`. NuGet fails a restore with
  NU1403 when a package's content differs from the lock file's hash (Microsoft's NU1403 page, Source above).

Gap found, too small for a ticket and left as is: `fetch-prism.ps1:33-35` skips the fetch when the DLL matches and the
two licence files exist, without hashing the licence files. They are text, came from the hash-checked zip, and CI starts
from a clean `artifacts/` (only the zip cache is shared, and it is re-hashed at `:39`).

## 4. Release and tag creation limited by the ruleset; the release workflow's token least-privilege

**Missing, owned elsewhere.** The tag ruleset is #309's (its point 8: only admins create, update or delete `v*` tags).
Creating a GitHub Release needs repository write access, which no ruleset narrows; who has it is #309's collaborator
question. There is no release workflow token yet; #310's criteria set its permissions. Existing workflows already
default to read: `ci.yml:10-11` (`contents: read`), and `scorecard.yml:12`, `conflict-alert.yml:14`, `slow.yml:12`
declare top-level permissions, with writes granted per job only (`ci.yml:207`, `conflict-alert.yml:27`,
`scorecard.yml:20`). Every action in `ci.yml` is pinned by commit SHA (for example `:105, 117, 161`).

## 5. The install path (#303) and install check (#283) verify the hash of what they install

**Not built yet; criteria exist for one.** Both issues are open with no code. #303's acceptance criteria include
"Downloads, if any, are pinned and hash-checked; nothing runs from an unpinned URL." #283 checks the game's build hash and
that the speech library is present, but not the hash of the mod's own files; a comment on #283 suggests it check the
installed files against the release's `SHA256SUMS` from #310.
