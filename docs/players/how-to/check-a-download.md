# Check a download

> **Draft for testers on dev builds.** There is no release yet. Until there is, the zips come from the project's CI
> runs on `main`; releases will be checked the same way, from their own workflow
> ([#310](https://github.com/Sour-Dev-Home/metaphor-accessibility/issues/310)).

The mod runs inside your game, so check a zip before you install it. GitHub's CI builds each zip from a commit on
`main` and signs it with an attestation: a record, signed by GitHub, of which repository, workflow and branch built
those exact bytes ([sources](../../raw-sources/github-artifact-attestations-2026-11-18.md)). If one byte changes after
the build, the check fails.

## Get a build

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
2. Open a terminal in the folder holding the zip and run this, with `COMMIT` replaced by the letters and digits in
   your zip's name:

   ```sh
   gh attestation verify metaphor-accessibility-COMMIT.zip --repo Sour-Dev-Home/metaphor-accessibility --signer-workflow Sour-Dev-Home/metaphor-accessibility/.github/workflows/ci.yml --source-ref refs/heads/main --deny-self-hosted-runners
   ```

3. If it reports a failure or an error, don't install the zip, and say so in an issue.

Each part narrows who could have made it: `--repo` this repository, `--signer-workflow` its CI workflow and no other,
`--source-ref` a build of `main`, not of a pull request's branch, and `--deny-self-hosted-runners` a build on GitHub's
own machines. CI runs this same command on every zip it signs.

## Check it without the GitHub CLI

This only shows the zip arrived intact: it compares the zip with the hash in `SHA256SUMS`, which came in the same
download. It can't show who built the zip; the GitHub CLI check above can.

1. Open PowerShell in the folder holding the zip and `SHA256SUMS`.
2. Run this, with `COMMIT` replaced as above. It reads the expected hash from `SHA256SUMS` itself, so there is
   nothing to copy or compare by ear:

   ```powershell
   (Get-FileHash .\metaphor-accessibility-COMMIT.zip -Algorithm SHA256).Hash -eq (Get-Content .\SHA256SUMS).Split(' ')[0]
   ```

3. It prints `True` when the hashes match and `False` when they don't. `-eq` ignores case, so `Get-FileHash`'s upper
   case and the file's lower case still match. On `False`, don't install the zip.

## Install

The zip holds one folder, `metaphor.accessibility`. Follow [Install the mod](install-the-mod.md) from "Add the mod from
a dev build".
