<#
.SYNOPSIS
Builds the players' installer exe (#608, #613; ADR 0020) around the release zip, reproducibly, and prints its SHA-256.

.DESCRIPTION
Lists every file in -Zip with its SHA-256, in sha256sum's format and ordinal order, then builds the launcher
(installer/Metaphor.Installer, .NET Framework 4.8, x64) with the zip and that list as its two embedded resources. The
exe unpacks the zip, checks each file against the list, and runs the installer in it (installer/Metaphor.Installer/
Launcher.cs). The compiler is deterministic and the exe carries no PDB, so the same zip and commit give the same bytes
at any path. Only the exe is copied to -Destination; nothing else from the build ships.

Build output goes to stderr, so stdout holds the hash alone. Exit codes: 0 built; 1 refused (no zip, a destination that
isn't an .exe, an entry that is a folder) or the build failed.

.EXAMPLE
pwsh scripts/release/installer-exe.ps1 -Zip artifacts/release/mod.zip -Destination artifacts/release/mod-installer.exe
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Zip,
    [Parameter(Mandatory)] [string] $Destination,
    # The launcher's project. Defaults to the one in this script's own checkout.
    [string] $Project = (Join-Path $PSScriptRoot '../../installer/Metaphor.Installer/Metaphor.Installer.csproj')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Stop-Build([string] $Message) {
    [Console]::Error.WriteLine("installer-exe: $Message")
    exit 1
}

if (-not (Test-Path -LiteralPath $Zip -PathType Leaf)) { Stop-Build "no zip at $Zip" }
$zipPath = (Resolve-Path -LiteralPath $Zip).ProviderPath
$destination = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Destination)
if ([IO.Path]::GetExtension($destination) -cne '.exe') { Stop-Build "the destination must be an .exe, not $Destination" }

$work = Join-Path ([IO.Path]::GetTempPath()) "installer-exe-$([guid]::NewGuid())"
New-Item -ItemType Directory $work | Out-Null
try {
    # The list the exe checks its unpacked files by: "SHA-256  path" per entry, LF line endings, no BOM.
    $lines = [Collections.Generic.List[string]]::new()
    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.EndsWith('/')) { Stop-Build "the zip holds a folder entry, which the exe doesn't unpack: $($entry.FullName)" }
            $stream = $entry.Open()
            try { $hash = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash.ToLowerInvariant() }
            finally { $stream.Dispose() }
            $lines.Add("$hash  $($entry.FullName)")
        }
    } finally { $archive.Dispose() }
    $sorted = [string[]] $lines.ToArray()
    [array]::Sort($sorted, [StringComparer]::Ordinal)
    $manifest = Join-Path $work 'payload.sha256'
    [IO.File]::WriteAllText($manifest, (($sorted | ForEach-Object { "$_`n" }) -join ''), [Text.UTF8Encoding]::new($false))

    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    # Its own intermediate and output folders, so a build of the checkout's solution running beside it (check.sh full)
    # never shares a file with it, and nothing from an earlier build is reused.
    $output = Join-Path $work 'out'
    & dotnet build $Project -c Release -f net48 -v q --nologo -o $output "-p:BaseIntermediateOutputPath=$work\obj\" `
        "-p:InstallerPayload=$zipPath" "-p:InstallerManifest=$manifest" 2>&1 |
        ForEach-Object { [Console]::Error.WriteLine($_) }
    if ($LASTEXITCODE -ne 0) { Stop-Build 'the launcher did not build' }

    New-Item -ItemType Directory -Force (Split-Path $destination) | Out-Null
    Copy-Item -LiteralPath (Join-Path $output 'Metaphor.Installer.exe') -Destination $destination -Force
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
