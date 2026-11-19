# winget: onboarding's pinned packages and the already-installed return codes

- **Source:** winget-cli's
  [`doc/windows/package-manager/winget/returnCodes.md`](https://github.com/microsoft/winget-cli/blob/df1118d8ebefb748eca079e69077387a02a586fa/doc/windows/package-manager/winget/returnCodes.md)
  at commit `df1118d8ebefb748eca079e69077387a02a586fa` (its latest change, 2026-10-01), fetched with
  `gh api repos/microsoft/winget-cli/contents/doc/windows/package-manager/winget/returnCodes.md`; and
  `winget show --id <id> --exact --version <version> --source winget` with winget v1.29.380 on Windows 11.
- **Saved:** 2026-11-19
- **Game build:** none (these are setup tools, not the game)

## Return codes

The rows `scripts/onboard.ps1` treats as "the package is already installed" (#358), quoted from the file above:

```text
| 0x8A15002B | -1978335189 | APPINSTALLER_CLI_ERROR_UPDATE_NOT_APPLICABLE | No applicable update found |
| 0x8A15004F | -1978335153 | APPINSTALLER_CLI_ERROR_UPGRADE_VERSION_NOT_NEWER | The upgrade version is not newer than the installed version |
| 0x8A150061 | -1978335135 | APPINSTALLER_CLI_ERROR_PACKAGE_ALREADY_INSTALLED | Found at least one version of the package installed. |
| 0x8A15010D | -1978334963 | APPINSTALLER_CLI_ERROR_INSTALL_ALREADY_INSTALLED | Another version of this application is already installed. |
| 0x8A15010E | -1978334962 | APPINSTALLER_CLI_ERROR_INSTALL_DOWNGRADE | A higher version of this application is already installed. |
```

## Pinned packages

Each pin in `scripts/onboard.ps1` checked with `winget show` on the date above (the lines below are its output, trimmed
to these fields). The .NET SDK isn't listed: its version comes from `global.json`.

```text
Found Git [Git.Git]
Version: 2.56.0.4
Publisher: The Git Development Community
License: GPL-2.0
License Url: https://github.com/git-for-windows/build-extra/blob/HEAD/LICENSE.txt
Installer SHA256: 4961a4a8cc778d8b6e00a5819e5fe4d078b754555aff7aedc2d77a5d74c2de80

Found PowerShell [Microsoft.PowerShell]
Version: 7.6.10.0
Publisher: Microsoft Corporation
License: MIT
License Url: https://github.com/PowerShell/PowerShell/blob/master/LICENSE.txt
Installer SHA256: c6caa271d179dae426b3536fb17569707033519649b471ad52858bf11c46149d

Found GitHub CLI [GitHub.cli]
Version: 2.105.0
Publisher: GitHub, Inc.
License: MIT
License Url: https://github.com/cli/cli/blob/HEAD/LICENSE
Installer SHA256: d18367836c7dcf689ad2052730acf3ec6b9e8b1259a8bae3d512584f680d7525

Found Node.js (LTS) [OpenJS.NodeJS.LTS]
Version: 26.13.1
Publisher: Node.js Foundation
License: MIT
License Url: https://github.com/nodejs/node/blob/v26.13.1/LICENSE
Installer SHA256: 0398ced8446e119478c724d1aa37c5afd6aa1a76fceab12787eb9714c5ed8b45
```

The Node.js pin (#361) is the newest version `winget show --id OpenJS.NodeJS.LTS --exact --versions --source winget`
listed that day (26.13.1, then 26.13.0).
