# Release provenance: what a tag build's attestation records, and the tools that make and check a release

- **Source:** fetched with `gh api` (GitHub CLI 2.101.0) on the date below:
  - `github/docs` at commit `39c1e014c75e972ab1fe531a81bc5bd42b543b07`:
    `content/actions/reference/workflows-and-actions/events-that-trigger-workflows.md` and
    `content/actions/reference/security/oidc.md`;
  - `sigstore/fulcio` at commit `b01d056a925d1f15d1315676d454adf46b6b6629`: `docs/oid-info.md` and
    `config/identity/config.yaml` (the GitHub Actions issuer's extension templates);
  - `cli/cli` at tag `v2.105.0` (the latest release, commit `e1219499a578b69e3437adaa31cd60614e23c802`):
    `pkg/cmd/attestation/verify/verify.go`, `pkg/cmd/attestation/verify/policy.go`,
    `pkg/cmd/attestation/verification/extensions.go` and `pkg/cmd/release/create/create.go`.
- **Saved:** 2026-11-18
- **Game build:** none (this is about GitHub Actions, Sigstore and the GitHub CLI, not the game)

Quoted excerpts, verbatim except that link targets and docs templating are dropped; nothing else from these files is
relied on. The chain: a workflow run's OIDC token carries the `ref` that triggered it; Fulcio copies that claim into
the signing certificate's Source Repository Ref; `gh attestation verify --source-ref` compares it exactly. So a release
built from tag `v1.2.3` verifies with `--source-ref refs/tags/v1.2.3`, and a build of `main` doesn't.

## What triggers the run, and its ref and commit (GitHub docs)

From `events-that-trigger-workflows.md`, the `push` event's table:

> | Webhook event payload | Activity types | `GITHUB_SHA` | `GITHUB_REF` |
> | `push` | Not applicable | Tip commit pushed to the ref. When you delete a branch, the SHA in the workflow run (and
> its associated refs) reverts to the default branch of the repository. | Updated ref |

> Runs your workflow when you push a commit or tag, or when you create a repository from a template.

> Events will not be created for tags when more than three tags are pushed at once.

The `workflow_dispatch` event's table and text:

> | `workflow_dispatch` | Not applicable | Last commit on the `GITHUB_REF` branch or tag | Branch or tag that received
> dispatch |

> Once a workflow has run at least once, you can dispatch it against any branch or tag via the API

## The OIDC token's `ref` claim (GitHub docs)

From `oidc.md`, the token's claims:

> | `ref`| _(Reference)_ The git ref that triggered the workflow run. |
>
> | `ref_type`| The type of `ref`, for example: "branch". |

## Fulcio puts that claim in the certificate

From `docs/oid-info.md`:

> ### 1.3.6.1.4.1.57264.1.14 | Source Repository Ref
>
> Source Repository Ref that the build run was based upon. For example: `refs/head/main` git branch or tag.

From `config/identity/config.yaml`, the GitHub Actions issuer's `extension-templates`:

> ```yaml
>       # ref: Git ref being built
>       github-workflow-ref: "ref"
>       # job_workflow_ref: Specific build instructions (i.e. reusable workflow)
>       build-signer-uri: "{{ .url }}/{{ .job_workflow_ref }}"
>       ...
>       source-repository-ref: "ref"
> ```

## `gh attestation verify --source-ref` compares it exactly

From `verify.go`:

> ```go
> verifyCmd.Flags().StringVarP(&opts.SourceRef, "source-ref", "", "", "Enforce that the git ref associated with the source repository matches the provided value")
> ```

From `policy.go`:

> ```go
> c.Certificate.SourceRepositoryRef = opts.SourceRef
> ```

From `extensions.go`:

> ```go
> // SourceRepositoryRef must be a case-sensitive comparison
> if expected.SourceRepositoryRef != "" && expected.SourceRepositoryRef != given.SourceRepositoryRef {
> ```

On `--signer-workflow`, from `policy.go`'s comment on the pattern it builds:

> sigstore-go matches SAN patterns with regexp.MatchString, which is unanchored, so a pattern anchored only at the start
> is a prefix match: a workflow named release.yml.attacker.yml satisfies a pin on release.yml. Anchoring the end makes
> the pin an identity match.

## `gh release create`

From `create.go`:

> ```go
> cmd.Flags().BoolVarP(&opts.Draft, "draft", "d", false, "Save the release as a draft instead of publishing it")
> cmd.Flags().BoolVarP(&opts.VerifyTag, "verify-tag", "", false, "Abort in case the git tag doesn't already exist in the remote repository")
> ```
