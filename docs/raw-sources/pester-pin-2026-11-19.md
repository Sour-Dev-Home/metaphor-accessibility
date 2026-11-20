# Pester: the pin onboarding and CI install, and how they install it

- **Source:** the PowerShell Gallery's Pester package, listed with `Find-Module Pester -AllVersions -Repository PSGallery`
  and saved with `Save-PSResource -Name Pester -Version '[5.9.1]' -Repository PSGallery -TrustRepository`
  (PSResourceGet 1.3.1, PowerShell 7.7.0, Windows 11); `Get-Module Pester -ListAvailable` on the same machine; a full
  `bash scripts/check/run-pester.sh` run on Pester 6.2.0.
- **Saved:** 2026-11-19
- **Game build:** none (a test framework for the repository's own scripts, not the game)

## Versions on the Gallery (2026-11-19)

| Version | Published |
| ------- | --------- |
| 6.2.0 | 2026-09-09 |
| 6.1.0 | 2026-08-11 |
| 6.0.1 | 2026-07-18 |
| 6.0.0 | 2026-07-07 |
| 5.9.1 | 2026-08-11 |
| 5.9.0 | 2026-07-07 |
| 5.8.0 | 2026-06-30 |
| 5.7.1 | 2025-01-08 |

The maintainer chose 6.2.0, the newest, as the pin (#456, 2026-11-19). `run-pester.sh` and CI take any 5.0 or later and
only warn off the pin.

## Pester 6.2.0 runs the suite

`bash scripts/check/run-pester.sh` with Pester 6.2.0 loaded (installed at `Documents\PowerShell\Modules\Pester\6.2.0`):
all 11 files passed, 225 tests, 0 failed.

## Layout and what is already there

- `Save-PSResource` put 5.9.1 at `<folder>\Pester\5.9.1\Pester.psd1`, and 6.2.0 sits at `<folder>\Pester\6.2.0\`: the
  layout onboarding looks for in each module folder. `-Version '[5.9.1]'` is an exact NuGet version range, so no other
  version is taken.
- On this machine `Get-Module Pester -ListAvailable` listed 3.4.0 at
  `%ProgramFiles%\WindowsPowerShell\Modules\Pester\3.4.0`, the copy that ships with Windows PowerShell 5.1. That folder
  is on PowerShell 7's module path too, so a pwsh without a newer Pester finds Pester 3.
- `Install-PSResource` shipped with PowerShell 7.7.0 here (module `Microsoft.PowerShell.PSResourceGet` 1.3.1). That it
  ships with every 7.4 and later, including onboarding's pinned 7.6.10 and the CI runner's pwsh: [NEEDS VERIFICATION].
- `-Scope CurrentUser` installs to the user's `Documents\PowerShell\Modules` with no admin prompt: an install of 5.9.1
  landed at `Documents\PowerShell\Modules\Pester\5.9.1`.
