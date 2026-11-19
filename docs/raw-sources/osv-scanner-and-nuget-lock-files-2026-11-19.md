# OSV-Scanner on NuGet lock files, and NuGet's locked mode

- **Source:** fetched with `gh api` (GitHub CLI) on the date below:
  - `google/osv-scanner` `docs/supported_languages_and_lockfiles.md`, `docs/configuration.md`, `docs/usage.md` and
    `docs/output.md` at commit `02f96a40691c2d155fdda799af436e111231e365` (tag v2.7.1, the latest release, published
    2026-11-09);
  - the v2.7.1 release's asset digests, from `gh api repos/google/osv-scanner/releases/tags/v2.7.1`;
  - `google/osv.dev` `docs/data.md` at commit `646570b1abd0fe3ef845ca35d0611576e88f7b5c`;
  - `NuGet/docs.microsoft.com-nuget` `docs/consume-packages/Package-References-in-Project-Files.md` at commit
    `30c0a128b39a56cdfc275e1101d65c166aadb4fe`;
  - `dotnet/docs` `docs/core/tools/dotnet-restore.md` and `docs/core/tools/dotnet-package-list.md` at commit
    `e62c5cfbd811b272e34eaf82feb88673f8c6a205`;
  - a local run, described in its own section below.
- **Saved:** 2026-11-19
- **Game build:** none (this is about CI tooling, not the game)

Quoted excerpts, verbatim except that link targets, table padding and code fences are dropped; nothing else from these
files is relied on. The last two sections are a local run and what was checked before choosing OSV-Scanner.

## OSV-Scanner reads `packages.lock.json`

From `supported_languages_and_lockfiles.md`, the table of supported files:

> | .NET       | `deps.json`<br>`packages.config`<br>`packages.lock.json` |

From `usage.md`:

> osv-scanner scan -L package-lock.json --config ./my-osv-scanner-config.toml

> The `--config` flag can be used to specify a global config override to apply to all the files you are scanning.

## A config next to a lock file can ignore vulnerabilities; `--config` overrides it

From `configuration.md`:

> To configure scanning, place an osv-scanner.toml file in the scanned file's directory. This does not propagate to
> child directories.

> To override `osv-scanner.toml` files, pass the `--config=/path/to/config.toml` flag with the path to the
> configuration you want to apply instead, this will apply `config.toml` to all files parsed, and ignore
> `osv-scanner.toml` in all directories.

> To ignore a vulnerability, enter the ID under the `IgnoredVulns` key.

## Exit codes

From `output.md`, "Return Codes":

> | `0` | Packages were found when scanning, but does not match any known vulnerabilities or findings. |
> | `1` | Packages were found when scanning, and there are vulnerabilities or findings. |
> | `1-126` | Reserved for vulnerability result related errors. |
> | `127` | General Error. |
> | `128` | No packages found (likely caused by the scanning format not picking up any files to scan). |
> | `129-255` | Reserved for non result related errors. |

## Release v2.7.1 asset digests

From the release's asset list:

> osv-scanner_linux_amd64 sha256:fb7dab19a8bd9bbe5c3189f09d4b7ee1820c9553a23bac202648a56efd2f6ee8

## OSV's data includes the GitHub Advisory Database

From `google/osv.dev` `docs/data.md`:

> The following ecosystems have vulnerabilities encoded in this format:
>
> - GitHub Advisory Database

## NuGet lock files hold the full closure; locked mode refuses to change them

From `Package-References-in-Project-Files.md`, "Locking dependencies":

> Input to NuGet restore is a set of `PackageReference` items from the project file (top-level or direct dependencies)
> and the output is a full closure of all the package dependencies including transitive dependencies.

> If NuGet detects a change in the defined dependencies as mentioned in the project file(s), it re-evaluates the
> package graph and updates the lock file to reflect the new package closure for the project.
>
> For CI/CD and other scenarios, where you would not want to change the package dependencies on the fly, you can do so
> by setting the `lockedmode` to `true`:
>
> For dotnet.exe, run:
>
> `> dotnet.exe restore --locked-mode`

From `dotnet-restore.md`:

> - **`--locked-mode`**
>
>   Don't allow updating project lock file.

## A project can name another lock file, or turn lock files off

From `Package-References-in-Project-Files.md`, "Lock file extensibility":

> | `-UseLockFile` |`--use-lock-file` | RestorePackagesWithLockFile | Opts into the usage of a lock file. |
> | `-LockedMode` | `--locked-mode` | RestoreLockedMode | Enables locked mode for restore. |
> | `-LockFilePath` | `--lock-file-path` | NuGetLockFilePath | Defines a custom lock file location for a project. By
> default, NuGet supports `packages.lock.json` at the root directory. If you have multiple projects in the same
> directory, NuGet supports project specific lock file `packages.<project_name>.lock.json` |

(The `RestoreLockedMode` row's description is cut after its first sentence.)

## Local run, 2026-11-19

OSV-Scanner 2.7.1 (`osv-scanner_linux_amd64`, SHA-256 as above), installed and run by `scripts/vuln-audit.sh install`
and `scan`, the commands CI runs, in WSL (Debian 13.5) on Windows 11.

- On this repository's 12 committed `packages.lock.json` files: each was scanned (0 to 20 packages each), then
  `No issues found`, exit 0.
- On a scratch repository holding one project whose lock file resolves `Newtonsoft.Json` `12.0.1`: the audit failed
  with exit 1, reporting `GHSA-5crp-9r3c-p9vr` (ecosystem `NuGet`, fixed in `13.0.1`). So OSV-Scanner's NuGet results
  include GitHub Advisory Database (GHSA) entries.
- The same repository with an `osv-scanner.toml` beside the lock file holding a `[[PackageOverrides]]` entry with
  `name = "Newtonsoft.Json"`, `ecosystem = "NuGet"` and `ignore = true`: the audit still failed with the same report.
  A bare `osv-scanner scan source --lockfile=src/App/packages.lock.json` without `--config`, as a control, printed
  `Filtered 1 ignored package/s from the scan.` and `No issues found`, exit 0. The audit's own `--config` is what
  keeps the planted file out.

## What was checked

- `dotnet list package --vulnerable --include-transitive`, which CI ran before (#322). It reads the restore's output,
  so it needs a `dotnet restore` first, and that restore evaluates every project and runs its targets: a project or
  target a pull request adds outside the security-review scope can change what the restore writes, and so what the
  audit reports (issue #324's reason for replacing it). From `dotnet-package-list.md`:

  > Starting with **.NET 10**, the command automatically performs restore if necessary before generating the results.
  > In earlier versions, you first need to **build/restore the project** in order to have the assets needed for this
  > command to process.
- OSV-Scanner, which issue #324 suggested: it parses `packages.lock.json` without MSBuild (the supported-files table
  above) and is pinned here by version and hash. No other scanner was evaluated.
