<#
.SYNOPSIS
Zips the packaged mod for players, reproducibly (#308): the same files and time give the same bytes, so the zip's
SHA-256 identifies the build.

.DESCRIPTION
Puts every file of the package folder (scripts/package-mod.sh's output) under one folder named for the mod ID from its
ModConfig.json, the folder players copy into Reloaded-II's mods folder. Entries go in ordinal path order, stored
without compression (a compressor's output can change between versions; stored bytes can't), with every timestamp set
to -Timestamp. CI passes the commit's time. Nothing else from the disk (file times, attributes, creation order) reaches
the zip. Builds on one operating system match; .NET records the system that wrote a zip in each entry.

Prints the zip's SHA-256. Exit codes: 0 written; 1 refused (no ModConfig.json, a ModId that isn't a plain folder name,
a link in the package, the zip inside the package, or a time a zip can't hold).

.EXAMPLE
pwsh scripts/zip-package.ps1 -Path artifacts/mod -Destination artifacts/release/mod.zip -Timestamp (git log -1 --format=%ct)
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Path,
    [Parameter(Mandatory)] [string] $Destination,
    # Seconds since 1970 (UTC). A zip holds times from 1980 to 2107 only.
    [Parameter(Mandatory)] [long] $Timestamp
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression

function Stop-Zip([string] $Message) {
    [Console]::Error.WriteLine("zip-package: $Message")
    exit 1
}

$root = (Resolve-Path -LiteralPath $Path).ProviderPath.TrimEnd('\', '/')
$destination = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Destination)

$time = [DateTimeOffset]::FromUnixTimeSeconds($Timestamp)
if ($time.Year -lt 1980 -or $time.Year -gt 2107) { Stop-Zip "a zip holds times from 1980 to 2107, not $($time.ToString('u'))" }

$config = Join-Path $root 'ModConfig.json'
if (-not (Test-Path -LiteralPath $config -PathType Leaf)) { Stop-Zip "no ModConfig.json in $Path" }
$modId = (Get-Content -Raw -LiteralPath $config | ConvertFrom-Json).PSObject.Properties['ModId']?.Value
if ($modId -isnot [string] -or $modId -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') {
    Stop-Zip "ModConfig.json's ModId must be a plain folder name (letters, digits, '.', '_', '-'), not '$modId'"
}

$prefix = $root + [IO.Path]::DirectorySeparatorChar
if ($destination.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { Stop-Zip "the zip can't go inside $Path" }

$items = @(Get-ChildItem -LiteralPath $root -Recurse -Force)
$links = @($items | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })
if ($links) { Stop-Zip "the package holds a link, which a zip would follow: $($links[0].FullName)" }
$names = [string[]]@($items | Where-Object { -not $_.PSIsContainer } |
        ForEach-Object { $_.FullName.Substring($prefix.Length).Replace('\', '/') })
[array]::Sort($names, [StringComparer]::Ordinal)

New-Item -ItemType Directory -Force (Split-Path $destination) | Out-Null
$stream = [IO.File]::Open($destination, [IO.FileMode]::Create, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in $names) {
            $entry = $zip.CreateEntry("$modId/$name", [IO.Compression.CompressionLevel]::NoCompression)
            $entry.LastWriteTime = $time
            $entry.ExternalAttributes = 0
            $source = [IO.File]::OpenRead((Join-Path $root $name))
            try {
                $target = $entry.Open()
                try { $source.CopyTo($target) } finally { $target.Dispose() }
            } finally { $source.Dispose() }
        }
    } finally { $zip.Dispose() }
} finally { $stream.Dispose() }

(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
