<#
.SYNOPSIS
Fetches the pinned Prism release for the mod (ADR 0005): prism.dll and its licences, checked by SHA-256.

.DESCRIPTION
Reads the pin from src/Metaphor.Hooks/prism.json. Downloads the release zip into a cache (once), refuses it unless its
SHA-256 matches, extracts only prism.dll and LICENSES/, and refuses the DLL unless its SHA-256 matches too. The result
goes to artifacts/native/prism/ (git-ignored), which publishing the mod copies from. Nothing is ever committed.

Runs before every publish of Metaphor.Hooks (its FetchPrism target). Does nothing when the right DLL is already there.
Safe to run several at once: fetches sharing a cache take turns.
Exit codes: 0 the pinned files are in place; 1 a download or hash check failed.

.EXAMPLE
pwsh scripts/fetch-prism.ps1
#>
[CmdletBinding()]
param(
    [string] $Pin = (Join-Path $PSScriptRoot '../src/Metaphor.Hooks/prism.json'),
    [string] $OutDir = (Join-Path $PSScriptRoot '../artifacts/native/prism'),
    # Where downloaded zips are kept between runs; CI points both of its checkouts at one cache.
    [string] $Cache = $(if ($env:PRISM_CACHE) { $env:PRISM_CACHE } else { Join-Path $PSScriptRoot '../artifacts/native/cache' })
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-Sha256([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

$pinned = Get-Content -Raw -LiteralPath $Pin | ConvertFrom-Json
$dll = Join-Path $OutDir 'prism.dll'
$licences = Join-Path $OutDir 'LICENSES'
function Test-Complete {
    (Test-Path -LiteralPath $dll) -and (Get-Sha256 $dll) -eq $pinned.dllSha256 -and
    (Test-Path -LiteralPath (Join-Path $licences 'prism/mpl-2.0.txt')) -and (Test-Path -LiteralPath (Join-Path $licences 'SOURCE.md'))
}
if (Test-Complete) { exit 0 }

# One fetch at a time per cache (#333): parallel mod builds in one checkout share the cache's partial file and the output
# folder. The lock is an exclusive handle on a file in the cache, which the OS releases if this process dies; whoever
# waited then finds the files in place and stops there.
New-Item -ItemType Directory -Force $Cache | Out-Null
$lockPath = Join-Path $Cache 'fetch-prism.lock'
$waitUntil = [DateTime]::UtcNow.AddMinutes(10)
$lock = $null
while (-not $lock) {
    try { $lock = [IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None') }
    catch [IO.IOException] {
        if ([DateTime]::UtcNow -gt $waitUntil) { Write-Host "fetch-prism: another fetch has held $lockPath for 10 minutes"; exit 1 }
        Start-Sleep -Milliseconds 200
    }
}
if (Test-Complete) { exit 0 }

$zip = Join-Path $Cache "prism-$($pinned.zipSha256).zip"
if (-not (Test-Path -LiteralPath $zip) -or (Get-Sha256 $zip) -ne $pinned.zipSha256) {
    Write-Host "fetching Prism $($pinned.version)"
    # A file:// URL is copied (an offline mirror, or the tests); anything else is downloaded. Either way the hash decides.
    $uri = [Uri]$pinned.url
    if ($uri.IsFile) { Copy-Item -LiteralPath $uri.LocalPath "$zip.part" -Force }
    else { Invoke-WebRequest -Uri $uri -OutFile "$zip.part" -UseBasicParsing }
    if ((Get-Sha256 "$zip.part") -ne $pinned.zipSha256) {
        Remove-Item -LiteralPath "$zip.part" -Force
        Write-Host "fetch-prism: the download's SHA-256 doesn't match the pin in $Pin; nothing was extracted"
        exit 1
    }
    Move-Item -LiteralPath "$zip.part" $zip -Force
}

# Extract only what ships: the release DLL and the licences. Entry names come from the pin or a fixed prefix, and each
# is written under the output folder, never elsewhere.
$staging = Join-Path ([IO.Path]::GetTempPath()) "metaphor-prism-$([guid]::NewGuid())"
try {
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        foreach ($entry in $archive.Entries) {
            $target = if ($entry.FullName -eq $pinned.dllPath) { 'prism.dll' }
                      elseif ($entry.FullName -like 'LICENSES/*' -and $entry.Name) { $entry.FullName }
                      else { $null }
            if (-not $target) { continue }
            $path = [IO.Path]::GetFullPath((Join-Path $staging $target))
            $inside = [IO.Path]::GetFullPath($staging) + [IO.Path]::DirectorySeparatorChar
            if (-not $path.StartsWith($inside, [StringComparison]::OrdinalIgnoreCase)) { throw "zip entry escapes the output: $($entry.FullName)" }
            New-Item -ItemType Directory -Force (Split-Path $path) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $path, $true)
        }
    }
    finally { $archive.Dispose() }

    $staged = Join-Path $staging 'prism.dll'
    if (-not (Test-Path -LiteralPath $staged) -or (Get-Sha256 $staged) -ne $pinned.dllSha256) {
        Write-Host "fetch-prism: prism.dll in the zip doesn't match the pin in $Pin"
        exit 1
    }
    # Where the source is, shipped with the licences: MPL-2.0 section 3.2(a), and LGPL-2.1 for NVDA's controller client
    # compiled into prism.dll (ADR 0005).
    New-Item -ItemType Directory -Force (Join-Path $staging 'LICENSES') | Out-Null
    Set-Content -LiteralPath (Join-Path $staging 'LICENSES/SOURCE.md') -Encoding utf8NoBOM -Value @"
# Source of prism.dll

``prism.dll`` is Prism $($pinned.version), unmodified, from $($pinned.url) (SHA-256 of the DLL: ``$($pinned.dllSha256)``).
Its source, including NVDA's controller client compiled into it, is at $($pinned.sourceUrl).
Prism is under the Mozilla Public License 2.0 (``prism/mpl-2.0.txt``); the other licences here cover the parts it
includes.
"@
    if (Test-Path -LiteralPath $OutDir) { Remove-Item -LiteralPath $OutDir -Recurse -Force }
    New-Item -ItemType Directory -Force (Split-Path $OutDir) | Out-Null
    Move-Item -LiteralPath $staging $OutDir
    Write-Host "Prism $($pinned.version) ready: $(Resolve-Path -Relative $OutDir)"
    exit 0
}
finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
