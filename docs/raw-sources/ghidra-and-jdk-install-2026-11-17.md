# Ghidra 12.2 and Temurin JDK 25: where they came from and their hashes

- **Source:** Ghidra's GitHub release <https://github.com/NationalSecurityAgency/ghidra/releases/tag/Ghidra_12.2_build>
  and its `GhidraDocs/GettingStarted.md` at tag `Ghidra_12.2_build`; Eclipse Adoptium's GitHub release
  <https://github.com/adoptium/temurin25-binaries/releases/tag/jdk-25.0.5%2B8>; the winget manifest of
  `EclipseAdoptium.Temurin.25.JDK` 25.0.5.8 (`winget show`, winget source, read 2026-11-17).
- **Saved:** 2026-11-17.
- **Game build:** none. These are the tools for the local Ghidra project (#290, `scripts/ghidra.ps1`).

## What was checked first (rule 3)

- No Ghidra (`analyzeHeadless`), no Java and no Python were installed. `winget search ghidra` finds no package.
- The modding tools survey (`modding-tools-survey-2026-11-13.md`) names no shared disassembler database for this game.

## Ghidra 12.2

- Release asset `ghidra_12.2_PUBLIC_20261016.zip`, published 2026-10-22, 569,712,708 bytes.
- SHA-256 published in the release notes and as the asset digest:
  `2c2cd776db3c1f6a097ba418922378ac3649cadd5e844a6f5f088ae628a7be67`. `sha256sum` of the download matched.
- Downloaded with `gh release download Ghidra_12.2_build -R NationalSecurityAgency/ghidra`, extracted to
  `%LOCALAPPDATA%\Programs\ghidra_12.2_PUBLIC`. Licence: Apache-2.0.
- `GettingStarted.md` at that tag: "Java 25 64-bit Runtime and Development Kit (JDK)", with Adoptium Temurin and
  Amazon Corretto named as free LTS providers; Python 3.9 to 3.15 only for PyGhidra. The project's scripts are Java,
  compiled by Ghidra, so no Python is needed.

## Temurin JDK 25.0.5+8

- winget has `EclipseAdoptium.Temurin.25.JDK` 25.0.5.8 (released 2026-10-26), an MSI whose manifest SHA-256
  `e1d3c94032579dc16af9abb311f4552ba271466b74c8767e8ce39585a4c64d40` matches Adoptium's `.sha256.txt` and the GitHub
  asset digest. `winget install --scope machine` waited on an elevation prompt for 25 minutes and was stopped, so it
  installed nothing.
- Installed instead from the same release as a zip, which needs no elevation:
  `OpenJDK25U-jdk_x64_windows_hotspot_25.0.5_8.zip`, SHA-256
  `636bdc13dc649ddc7d5921c5c7d23cca6e44fd19d662b45cc9c02135a6c96c42`. The GitHub asset digest, Adoptium's
  `.zip.sha256.txt` and `Get-FileHash` of the download all agree. Extracted to `%LOCALAPPDATA%\Programs\jdk-25.0.5+8`.
  `java -version`: `OpenJDK Runtime Environment Temurin-25.0.5+8 (build 25.0.5+8-LTS)`. Licence: GPL-2.0 with the
  Classpath Exception.
- The `.sig` file next to the zip was downloaded but not checked against Adoptium's signing key:
  [NEEDS VERIFICATION].
