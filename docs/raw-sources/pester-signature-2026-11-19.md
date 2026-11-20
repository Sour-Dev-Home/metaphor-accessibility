# Pester 6.2.0's Authenticode signatures, and what PSResourceGet's -AuthenticodeCheck checks

- **Source:** `Get-AuthenticodeSignature` on every file of the Pester 6.2.0 that `Install-PSResource` installed from
  PSGallery at `Documents\PowerShell\Modules\Pester\6.2.0` (`Get-InstalledPSResource` lists it from PSGallery), read
  only, in PowerShell 7.6.10 on Windows 11; PSResourceGet's source, `src/code/Utils.cs` (`CheckAuthenticodeSignature`)
  and `src/code/InstallHelper.cs`, at tag v1.3.1 (commit `fa3e56b8970fd1f12a63627c7936839a684c4078`), v1.2.1
  (`b348280e0497b8aa18cc2a6ad84723f5b4007137`) and master (`7e057bcdb37c9605fdf43a975861ef1206978345`); the
  `Save-PSResource` reference on Microsoft Learn (ms.date 2025-12-10).
- **Saved:** 2026-11-19
- **Game build:** none (a test framework for the repository's own scripts, not the game)

## Pester 6.2.0 is signed by its maintainer's certificate

Every script and DLL file is `Valid` and signed by one certificate:

| Field | Value |
| ----- | ----- |
| Subject | `CN=Jakub Jareš, O=Jakub Jareš, L=Praha, C=CZ` |
| Issuer | `CN=DigiCert Trusted G4 Code Signing RSA4096 SHA384 2021 CA1, O="DigiCert, Inc.", C=US` |
| Thumbprint | `147C2FD397677DC76DD198E83E7D9D234AA59D1A` |
| Valid | 2024-07-01 to 2027-04-24 |
| Timestamp | `CN=DigiCert SHA256 RSA4096 Timestamp Responder 2026 1` |

The signed files: `Pester.psd1`, `Pester.psm1`, `Pester.ScriptScope.ps1`, `Pester.Format.ps1xml`,
`PesterConfiguration.Format.ps1xml`, `bin\net462\Pester.dll`, `bin\net8.0\Pester.dll`. The rest are text and schema
files that carry no signature (`UnknownError` from `Get-AuthenticodeSignature`): `ThirdPartyNotices.txt`,
`en-US\*.help.txt`, and the `.dtd` and `.xsd` files under `schemas\`. The package has no catalog (`.cat`) file.

The timestamp keeps the signatures valid after the certificate expires. A later Pester release may come with a new
certificate, so a change of `$pesterVersion` rechecks the thumbprint.

## What -AuthenticodeCheck checks

- Learn: "Validates the resource's signed files and catalog files on Windows."
- `CheckAuthenticodeSignature`, the same in all three versions read: on Linux and macOS it only warns ("Authenticode
  check cannot be performed on Linux or MacOS.") and passes. On Windows it runs `Get-AuthenticodeSignature` on files
  matching `*.ps1`, `*.psd1`, `*.psm1`, `*.mof`, `*.cat`, `*.ps1xml` and fails when any status isn't `Valid`.
- So it accepts any valid signer, and it doesn't look at DLLs. No PSResourceGet parameter names an expected signer.
- `InstallHelper.cs` at master runs the check on the package's temporary folder, before the files move to the install path, so a
  refused package isn't installed.
