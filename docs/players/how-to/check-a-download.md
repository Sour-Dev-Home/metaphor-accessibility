# Check a download

> **Draft: there is no release yet.** Until the first one, the zips come from the project's CI runs on `main`, for
> testers on dev builds. Both are checked the same way; only the command's last details differ.

The mod runs inside your game, so check a zip before you install it. GitHub's CI builds each zip and signs it with an
attestation: a record, signed by GitHub, of which repository, workflow and branch or tag built those exact bytes
([sources](../../raw-sources/github-artifact-attestations-2026-11-18.md)). If one byte changes after the build, the
check fails.

## Get a release

1. Open the project's Releases page, `https://github.com/Sour-Dev-Home/metaphor-accessibility/releases`.
2. Under the release you want, download two files: `metaphor-accessibility-` followed by the version and `.zip` (for
   example `metaphor-accessibility-v1.2.3.zip`), and `SHA256SUMS`. Put them in one folder.

The version is the release's name, starting with `v`. Each release is built by its own workflow from that version's
tag, on a commit already on `main`.

## Get a dev build

You need a GitHub account: GitHub asks you to sign in before it downloads a run's artifacts. A run's artifacts expire,
after 90 days at most unless the repository's settings change that, so pick a recent run
([sources](../../raw-sources/github-artifact-attestations-2026-11-18.md)).

1. Sign in to GitHub. Open the project's Actions page, `https://github.com/Sour-Dev-Home/metaphor-accessibility/actions`,
   and choose the `CI` workflow.
2. Open a run for a push to `main` that passed.
3. Under the run's "Artifacts" heading, download `release-` followed by the commit.
4. Unzip it. Inside are the mod's zip, `metaphor-accessibility-` followed by the commit and `.zip`, and `SHA256SUMS`.
   Check that inner zip, then install it.

## Check it with the GitHub CLI

This is the full check: it shows who built the zip, not only that it arrived intact.

1. Install the [GitHub CLI](https://cli.github.com/) and sign in with `gh auth login`.
2. Open a terminal in the folder holding the zip.
3. For a release, run this, with `VERSION` replaced by the version in your zip's name, both times (for example
   `v1.2.3`):

   ```sh
   gh attestation verify metaphor-accessibility-VERSION.zip --repo Sour-Dev-Home/metaphor-accessibility --signer-workflow Sour-Dev-Home/metaphor-accessibility/.github/workflows/release.yml --source-ref refs/tags/VERSION --deny-self-hosted-runners
   ```

   For a dev build, run this instead, with `COMMIT` replaced by the letters and digits in your zip's name:

   ```sh
   gh attestation verify metaphor-accessibility-COMMIT.zip --repo Sour-Dev-Home/metaphor-accessibility --signer-workflow Sour-Dev-Home/metaphor-accessibility/.github/workflows/ci.yml --source-ref refs/heads/main --deny-self-hosted-runners
   ```

4. If it reports a failure or an error, don't install the zip, and say so in an issue.

Each part narrows who could have made it: `--repo` this repository, `--signer-workflow` its release workflow (or, for
a dev build, its CI workflow) and no other, `--source-ref` a build of that version's tag (or of `main`), not of a pull
request's branch, and `--deny-self-hosted-runners` a build on GitHub's own machines
([sources](../../raw-sources/github-release-provenance-2026-11-18.md)). CI runs this same command on every zip it signs.

## Check it without the GitHub CLI

This only shows the zip arrived intact: it compares the zip with the hash in `SHA256SUMS`, which came from the same
place. It can't show who built the zip; the GitHub CLI check above can.

1. Open PowerShell in the folder holding the zip and `SHA256SUMS`.
2. Run this, with `VERSION` replaced as above (for a dev build, the zip's name holds the commit instead). It reads the
   expected hash from `SHA256SUMS` itself, so there is nothing to copy or compare by ear:

   ```powershell
   (Get-FileHash .\metaphor-accessibility-VERSION.zip -Algorithm SHA256).Hash -eq (Get-Content .\SHA256SUMS).Split(' ')[0]
   ```

3. It prints `True` when the hashes match and `False` when they don't. `-eq` ignores case, so `Get-FileHash`'s upper
   case and the file's lower case still match. On `False`, don't install the zip.

## Install

The zip holds one folder, `metaphor.accessibility`. Follow [Install the mod](install-the-mod.md) from "Add the mod from
a dev build".
