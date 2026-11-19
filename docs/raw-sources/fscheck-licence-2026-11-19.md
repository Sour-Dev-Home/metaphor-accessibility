# FsCheck's licence and pinned version

- **Source:** GitHub's licence detection and the licence file, fetched with the GitHub CLI:
  `gh api repos/fscheck/FsCheck/license` and `gh api repos/fscheck/FsCheck/contents/License.txt` (default branch
  `master`, head commit `1ef52cfc65d5c318f5ee4da467df9087ad03e34c`); its releases with
  `gh api repos/fscheck/FsCheck/releases`; the package's metadata from
  `https://api.nuget.org/v3-flatcontainer/fscheck/3.6.0/fscheck.nuspec`. Its one dependency's licence with
  `gh api repos/dotnet/fsharp/license`.
- **Saved:** 2026-11-19
- **Game build:** none (a test library, not shipped)

For issue #443 and ADR 0015. The output:

```text
fscheck/FsCheck License.txt BSD-3-Clause f94547651d2805335f961d64c3fa2f8ef1ee5cca
dotnet/fsharp License.txt MIT c35b1079d3c59b514e5a9f54eb92a912abe9b1a6
```

`License.txt` opens "Copyright (c) 2008-2014, Kurt Schelfthout. All rights reserved." and carries the three BSD
clauses: keep the notice in source, reproduce it in binary distributions, and don't use the names to endorse derived
products. BSD-3-Clause is GPL-3.0-compatible (the FSF lists the modified BSD licence as compatible with the GNU GPL).

The latest release is 3.6.0, published 2026-10-21. Its nuspec gives `<license type="expression">BSD-3-Clause</license>`
and one dependency, `FSharp.Core` 5.0.2 or later, for net8.0, .NETStandard2.0 and .NETStandard2.1. `FSharp.Core` comes
from `dotnet/fsharp`, MIT. The tests use FsCheck 3.6.0; neither library ships with the mod.
