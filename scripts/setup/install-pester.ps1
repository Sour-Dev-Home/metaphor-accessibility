# Installs the pinned Pester for this Windows user only if its files carry the pinned publisher's signature (#477).
# onboard.ps1 runs it after the contributor's yes, and CI's pester job before its tests; both pass the pins from
# onboard.ps1's $pesterVersion and $pesterSigner lines. Run it in PowerShell 7 (PSResourceGet ships with 7.4 and later):
#   pwsh -NoProfile -File scripts/setup/install-pester.ps1 -Version <version> -Signer <thumbprint>
# It saves the package from the PowerShell Gallery into a temporary folder with -AuthenticodeCheck, which refuses a
# script file whose signature isn't valid but accepts any valid signer and skips DLLs
# (docs/raw-sources/pester-signature-2026-11-19.md). So it then lists every file, hidden and system ones included, and
# allows each only when it is validly signed by the certificate with the pinned thumbprint or is one of a few inert data
# types. It copies exactly that list into a temporary folder beside the target and renames it to Pester\<version>, where
# Install-PSResource -Scope CurrentUser would put it. Anything else exits 1 with the reason, and the module folder is
# left as it was.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Version,
    # The SHA-1 thumbprint of the certificate that must have signed every file that isn't inert data.
    [Parameter(Mandatory)] [string] $Signer,
    # The module folder to install into. Defaults to the one Install-PSResource -Scope CurrentUser uses on Windows.
    [string] $ModulePath = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'PowerShell\Modules')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Data PowerShell never runs: help text, schemas and PSResourceGet's own metadata. Every other file must be signed.
$inertTypes = '.txt', '.md', '.xsd', '.dtd', '.xml', '.nuspec'
$target = Join-Path $ModulePath "Pester\$Version"
if (Test-Path -LiteralPath $target) {
    Write-Host "install-pester: $target already exists; remove it first to reinstall Pester $Version."
    exit 1
}

$staging = Join-Path ([IO.Path]::GetTempPath()) "install-pester-$([guid]::NewGuid())"
$partial = Join-Path (Split-Path $target) ".install-pester-$([guid]::NewGuid())"
New-Item -ItemType Directory $staging | Out-Null
try {
    try {
        Save-PSResource -Name Pester -Version "[$Version]" -Repository PSGallery -TrustRepository -Path $staging `
            -IncludeXml -AuthenticodeCheck -Quiet -ErrorAction Stop
    } catch {
        Write-Host "install-pester: saving Pester $Version from the PowerShell Gallery failed: $($_.Exception.Message)"
        exit 1
    }
    $saved = Join-Path $staging "Pester\$Version"
    if (-not (Test-Path -LiteralPath (Join-Path $saved 'Pester.psd1'))) {
        Write-Host "install-pester: the saved package has no Pester\$Version\Pester.psd1."
        exit 1
    }
    # -Force lists hidden and system files too: the list checked is the list copied.
    $files = @(Get-ChildItem -LiteralPath $saved -Recurse -Force -File)
    $bad = @(foreach ($file in $files) {
            if ($file.Extension.ToLowerInvariant() -in $inertTypes) { continue }
            $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
            $thumbprint = if ($signature.SignerCertificate) { $signature.SignerCertificate.Thumbprint } else { 'none' }
            if ($signature.Status -ne 'Valid' -or $thumbprint -ne $Signer) {
                "  $($file.FullName.Substring($saved.Length + 1)): $($signature.Status), signer $thumbprint"
            }
        })
    if ($bad) {
        Write-Host "install-pester: refused Pester $Version, as these files aren't validly signed by $Signer`:"
        $bad | ForEach-Object { Write-Host $_ }
        exit 1
    }
    try {
        foreach ($file in $files) {
            $destination = Join-Path $partial $file.FullName.Substring($saved.Length + 1)
            New-Item -ItemType Directory -Force (Split-Path $destination) | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
        }
        Move-Item -LiteralPath $partial -Destination $target
    } catch {
        Write-Host "install-pester: copying Pester $Version into $(Split-Path $target) failed: $($_.Exception.Message)"
        exit 1
    }
    Write-Host "install-pester: installed Pester $Version, signed by $Signer, at $target."
} finally {
    Remove-Item -LiteralPath $staging, $partial -Recurse -Force -ErrorAction SilentlyContinue
}
exit 0
