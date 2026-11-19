# GitHub artifact attestations: the actions and `gh attestation verify`

- **Source:** fetched with `gh api` (GitHub CLI 2.101.0) on the date below:
  - `actions/attest` `README.md` at commit `1e69f48acb82d1966a394da916b4c1698aa569d6` (tag v4.2.2);
  - `actions/attest-build-provenance` `README.md` and `action.yml` at commit `4d101475d8b20a2381f78447822ac1eab6504dd8`
    (tag v4.2.2, the latest release);
  - `actions/download-artifact` v8.0.0 release notes and `action.yml` at commit
    `9000827ccba6bdab643e8b6fd33ac0654aef8333` (tag v8.0.2);
  - `actions/upload-artifact` `action.yml` at commit `cf430e030ddbb5b0abf93d22962f4752f3646cd9` (tag v7.0.2, the pin in
    `ci.yml`);
  - the output of `gh attestation verify --help`, GitHub CLI 2.101.0 (2026-09-15), on Windows 11.
- **Saved:** 2026-11-18
- **Game build:** none (this is about GitHub Actions and the GitHub CLI, not the game)

Quoted excerpts, verbatim except that link targets are dropped; nothing else from these files is relied on.

## `actions/attest-build-provenance` is a wrapper on `actions/attest`

From its `README.md`:

> **As of version 4, `actions/attest-build-provenance` is simply a wrapper on top of `actions/attest`.**
>
> Existing applications may continue to use the `attest-build-provenance` action, but new implementations should use
> `actions/attest` instead.

Its `action.yml` runs one step, `uses: actions/attest@1e69f48acb82d1966a394da916b4c1698aa569d6 # v4.2.2`, passing its
inputs through.

## `actions/attest`: what it does, where, and what it needs

From its `README.md`:

> Attestations bind some subject (a named artifact along with its digest) to a predicate (some assertion about that
> subject) using the in-toto format.

> A verifiable signature is generated for the attestation using a short-lived Sigstore-issued signing
> certificate. If the repository initiating the GitHub Actions workflow is public, the public-good instance of Sigstore
> will be used to generate the attestation signature. If the repository is private/internal, it will use the GitHub
> private Sigstore instance.
>
> Once the attestation has been created and signed, it will be uploaded to the GH attestations API and associated with
> the repository from which the workflow was initiated.

> Artifact attestations are available in public repositories for all current GitHub plans.
>
> To use artifact attestations in private or internal repositories, you must be on a GitHub Enterprise Cloud plan.

> | **Provenance** | No `sbom-path` or predicate inputs | Auto-generates SLSA build provenance |

> 1. Ensure that the following permissions are set:
>
>    ```yaml
>    permissions:
>      id-token: write
>      attestations: write
>      artifact-metadata: write
>    ```
>
>    The `id-token` permission gives the action the ability to mint the OIDC token necessary to request a Sigstore
>    signing certificate. The `attestations` permission is necessary to persist the attestation. The
>    `artifact-metadata` permission is necessary to create the artifact storage record.

On the storage record, from the inputs:

> # Whether to create a storage record for the artifact.
> # Requires that push-to-registry is set to true.

and `push-to-registry` "Defaults to false", for OCI images only. Its own provenance example grants
`id-token: write`, `contents: read` and `attestations: write`, without `artifact-metadata`.

> # Path to the artifact serving as the subject of the attestation. At most
> # one of "subject-path", "subject-digest", or "subject-checksums" may be
> # provided.

## `actions/download-artifact` v8 fails on a digest mismatch

From the v8.0.0 release notes:

> A previous release introduced digest checks on the download. If a download hash didn't match the expected hash from
> the server, the action would log a warning. Callers can now configure the behavior on mismatch with the
> `digest-mismatch` parameter. To be secure by default, we are now defaulting the behavior to `error` which will fail
> the workflow run.

`action.yml` at v8.0.2: `digest-mismatch:` with `default: 'error'`.

## `actions/upload-artifact` v7 zips an upload unless told not to

From `action.yml` at v7.0.2:

> archive: If true, the artifact will be archived (zipped) before uploading. If false, the artifact will be uploaded
> as-is without archiving. When `archive` is `false`, only a single file can be uploaded. [...]
> default: 'true'

On expiry, the `retention-days` input:

> Duration after which artifact will expire in days. 0 means using default retention.
>
> Minimum 1 day.
> Maximum 90 days unless changed from the repository settings page.

On signing in, the `artifact-url` output:

> This download URL only works for requests Authenticated with GitHub. Anonymous downloads will be prompted to first
> login.

## `gh attestation verify`

From `gh attestation verify --help`, GitHub CLI 2.101.0:

> By default, this command enforces the `https://slsa.dev/provenance/v1` predicate type.

> At a minimum, this command requires either:
> * the `--owner` flag (e.g. --owner github), or
> * the `--repo` flag (e.g. --repo github/example)
>
> The more precisely you specify the identity, the more control you will have over the security guarantees offered by
> the verification process.
>
> Ideally, the path of the signer workflow is also validated using the `--signer-workflow` or `--cert-identity` flags.

> By default, this command will attempt to fetch relevant attestations via the GitHub API using the values provided to
> `--owner` or `--repo`.

> IMPORTANT: please note that only the `signature.certificate` and the `verifiedTimestamps` properties contain values
> that cannot be manipulated by the workflow that originated the attestation.

Flags:

> -R, --repo string                  Repository name in the format <owner>/<repo>
> --signer-workflow string       Enforce that the workflow that signed the attestation matches the provided value
>                                ([host/]<owner>/<repo>/<path>/<to>/<workflow>)
> --source-ref string            Enforce that the git ref associated with the source repository matches the provided
>                                value
> --deny-self-hosted-runners     Fail verification for attestations generated on self-hosted runners
> -d, --digest-alg string            The algorithm used to compute a digest of the artifact: {sha256|sha512} (default
>                                "sha256")

Example:

> # Verify an artifact linked with a repository
> $ gh attestation verify example.bin --repo github/example
