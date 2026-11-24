# Cut a release

For the maintainer. A release starts with a `v*` tag on a commit on `main`. The `Release` workflow
([`release.yml`](../../../.github/workflows/release.yml)) builds the tagged commit from scratch, rebuilds it to compare,
attests the installer and the zip and attaches them with `SHA256SUMS` to a draft release. You publish the draft. Only
admins create `v*` tags ([collaborators](../reference/security-review.md#collaborators)).

You need the GitHub CLI signed in with an admin account, and a clone with `main` up to date.

## 1. Dry run on `main`

A run by hand builds, attests and verifies a zip of `main`'s tip, and drafts nothing.

1. Start it: on GitHub, Actions, `Release`, "Run workflow" with the branch `main`. Or from a terminal:

   ```sh
   gh workflow run release.yml --ref main --repo Sour-Dev-Home/metaphor-accessibility
   ```

   The gate refuses any other branch.
2. Wait for the run to pass. `gate`, `build` and `attest` pass; `release` is skipped, because only a tag push drafts a
   release. The run's summary names the zip and its SHA-256 and says the rebuild matched.
3. Download the run's `release` artifact and unzip it. It holds `metaphor-accessibility-` followed by the commit and
   `.zip`, and `SHA256SUMS`. Check the zip as a player would, with `COMMIT` replaced by the commit in its name:

   ```sh
   gh attestation verify metaphor-accessibility-COMMIT.zip --repo Sour-Dev-Home/metaphor-accessibility --signer-workflow Sour-Dev-Home/metaphor-accessibility/.github/workflows/release.yml --source-ref refs/heads/main --deny-self-hosted-runners
   ```

   A dry run's attestation names `refs/heads/main`, not a tag, so it never passes a player's check for a release.

## 2. Tag the release

1. Pick the version. The gate takes `v` and three numbers, with an optional suffix of letters, digits and dots after a
   hyphen: `v0.1.0`, `v1.2.3-beta.1`. Anything else fails the run before it builds. A version is never reused.
2. Bump the version first: set `ModVersion` in
   [`src/Metaphor.Hooks/ModConfig.json`](../../../src/Metaphor.Hooks/ModConfig.json) to it without the `v`, such as
   `0.1.0`, and merge that to `main`. The mod's update notice compares it with the tags
   ([ADR 0018](../../adr/0018-one-update-check-per-launch.md)), and the gate fails a tag that isn't `v` plus
   `ModVersion` in the tagged commit.
3. Tag the commit on `main` with an annotated tag, and push that tag alone:

   ```sh
   git fetch origin
   git tag -a v0.1.0 -m "v0.1.0" origin/main
   git push origin v0.1.0
   ```

   GitHub starts no workflow for tags when more than three are pushed at once
   ([sources](../../raw-sources/github-release-provenance-2026-11-18.md)), so push one at a time.
4. The push starts `Release`. `gate` fails a tag whose commit isn't on `main`, or that doesn't match `ModVersion`.
   Then `build` builds and rebuilds, `attest` signs and verifies the zip, and `release` drafts the release `v0.1.0`
   with three assets: `metaphor-accessibility-v0.1.0-installer.exe`, `metaphor-accessibility-v0.1.0.zip` and
   `SHA256SUMS`.

## 3. Publish

1. Open the Releases page, `https://github.com/Sour-Dev-Home/metaphor-accessibility/releases`, and the draft.
2. Check its three assets. Download them and run the player's check from
   [Check a download](../../players/how-to/check-a-download.md#check-it-with-the-github-cli), with `VERSION` as the tag.
   The zip's SHA-256 must match `SHA256SUMS` and the run's summary. Verify the installer's attestation too, the same
   way as the zip's.
3. Write the release notes, leading with the installer exe as the easiest way to install (the first release's draft is
   [release-notes-0.1.0.md](../reference/release-notes-0.1.0.md)), then publish the draft. The release PR updates
   [`CHANGELOG.md`](../../../CHANGELOG.md) in every release: run `bash scripts/release/join-changelog.sh 0.1.0 YYYY-MM-DD`, which joins the
   [fragments](../../../changelog.d/README.md) into a new version section and deletes them (lines still under
   "Unreleased" move into it too), then read the result and commit it.

## When a step fails

- **Before a draft exists** (`gate`, `build` or `attest` failed): read the failed job's log. A fault in the run, such as
  a timeout, is fixed by re-running the failed jobs. A fault in the code needs a fix merged to `main` and a new version;
  delete the unused tag (`git push origin :refs/tags/v0.1.0`) only if nothing was published from it.
- **A draft exists but is wrong**: delete the draft, fix the cause, then re-run the workflow. `release` fails while a
  release for the tag already exists. To delete it from a terminal:
  `gh release delete v0.1.0 --repo Sour-Dev-Home/metaphor-accessibility`.
- **After publishing**: never move or delete a published tag; players' checks and downloads name it. Release a new
  version instead, and say in the old release's notes what is wrong with it.
