# winget: the Visual C++ Redistributable pin, and the runtime lychee's Windows build needs

- **Source:** `winget show --id Microsoft.VCRedist.2015+.x64 --exact --versions --source winget` and
  `winget show --id Microsoft.VCRedist.2015+.x64 --exact --version 14.52.37029.0 --source winget` with winget
  v1.29.380 on Windows 11; the package's installer manifest in microsoft/winget-pkgs,
  [`manifests/m/Microsoft/VCRedist/2015+/x64/14.52.37029.0/Microsoft.VCRedist.2015+.x64.installer.yaml`](https://github.com/microsoft/winget-pkgs/blob/f4ca2b2c028ab30018a6f7a53e62286005487c57/manifests/m/Microsoft/VCRedist/2015+/x64/14.52.37029.0/Microsoft.VCRedist.2015+.x64.installer.yaml)
  at commit `f4ca2b2c028ab30018a6f7a53e62286005487c57`, fetched with `gh api`; and the DLL names in lychee 0.24.2's
  `lychee-x86_64-pc-windows-msvc.zip` build (SHA-256 `32975d1493ee1a975d6bb41e4fb56fe419cb442ded628bb772ba2e614acfacad`,
  as `scripts/install-docs-tools.sh` pins it), listed with
  `grep -aoiE "(vcruntime|msvcp|api-ms-win-crt)[a-z0-9_.-]*\.dll" lychee.exe | sort -u` in Git Bash.
- **Saved:** 2026-11-19
- **Game build:** none (these are setup tools, not the game)

## The pinned package (`scripts/onboard.ps1`, #385)

`winget show` output, trimmed to these fields:

```text
Found Microsoft Visual C++ v14 Redistributable (x64) [Microsoft.VCRedist.2015+.x64]
Version: 14.52.37029.0
Publisher: Microsoft Corporation
Homepage: https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist
License: Proprietary (Freeware)
License Url: https://www.microsoft.com/legal/terms-of-use
Tags:
  vcruntime140.dll
Installer:
  Installer Type: burn
  Installer SHA256: 9c02c9ed120f671b900e6b9471d890e3b2414117a7e53f4ae2e855466fa2b7c9
  Release Date: 2026-11-06
```

`--versions` listed 14.52.37029.0 first, then 14.51.36247.0 and 14.51.36231.0. The pin is the newest.

The installer manifest's lines on scope and elevation (the installer raises its own admin prompt):

```yaml
InstallerType: burn
Scope: machine
InstallerSuccessCodes:
- 3010
ElevationRequirement: elevatesSelf
```

## What lychee's Windows build loads

The C runtime DLLs named in `lychee.exe` 0.24.2:

```text
VCRUNTIME140.dll
api-ms-win-crt-convert-l1-1-0.dll
api-ms-win-crt-environment-l1-1-0.dll
api-ms-win-crt-heap-l1-1-0.dll
api-ms-win-crt-locale-l1-1-0.dll
api-ms-win-crt-math-l1-1-0.dll
api-ms-win-crt-runtime-l1-1-0.dll
api-ms-win-crt-stdio-l1-1-0.dll
api-ms-win-crt-string-l1-1-0.dll
api-ms-win-crt-utility-l1-1-0.dll
```

`VCRUNTIME140.dll` comes from the Visual C++ Redistributable, not from Windows, and a clean Windows 11 VM had no
`C:\Windows\System32\vcruntime140.dll`: there, Git Bash ran `lychee --version` with no output and exit code 127 (#385).
