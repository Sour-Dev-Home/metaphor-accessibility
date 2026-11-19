<#
.SYNOPSIS
Issue #21's run in one command: scans the game exe on disk and the running game for the same signatures, compares
them, and writes a raw-source draft.

.DESCRIPTION
1. Builds the mod (dotnet publish) into a temporary folder, with the probes file that asks it for one scan
   (tools/Metaphor.SignatureCheck/signature-probes.json, CriFs V2 Hook's release patterns).
2. Scans the exe on disk (tools/Metaphor.SignatureCheck --probes).
3. Runs scripts/dev/smoke-test.ps1 with only that mod enabled, so no other mod's hooks change the bytes, and collects the
   mod's signature-scan.json, which the mod writes before it installs its own hooks. The smoke test closes the game
   and restores the Reloaded-II config.
4. Writes a draft to -OutFile: the game build, the steps, a side-by-side table and both raw outputs. Review it, then
   save it under docs/raw-sources/ and record the result on #21.

Only reads the game (ADR 0002). Exit codes: 0 the draft was written; 1 the run failed; 2 it couldn't start.

.EXAMPLE
pwsh scripts/dev/signature-spike.ps1
#>
[CmdletBinding()]
param(
    # The game executable. Defaults to METAPHOR_GAME_EXE, then to the game as registered in Reloaded-II.
    [string] $GameExe = $env:METAPHOR_GAME_EXE,
    # Reloaded-II's launcher. Defaults to METAPHOR_RELOADED_EXE, then to LauncherPath in ReloadedII.json.
    [string] $LauncherExe = $env:METAPHOR_RELOADED_EXE,
    [string] $Project = (Join-Path $PSScriptRoot '../../src/Metaphor.Hooks'),
    [string] $ReloadedConfig = (Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Reloaded-Mod-Loader-II/ReloadedII.json'),
    [string] $KnownBuilds = (Join-Path $PSScriptRoot '../../src/Metaphor.Core/known-game-builds.json'),
    [string] $OutFile = (Join-Path $PSScriptRoot "../../artifacts/signature-spike/signature-spike-$(Get-Date -Format yyyy-MM-dd-HHmmss).md"),
    [int] $TimeoutSeconds = 120,
    # The probes to scan for, on disk and in the game.
    [string] $Probes = (Join-Path $PSScriptRoot '../../tools/Metaphor.SignatureCheck/signature-probes.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReloadedDev.psm1') -Force

$work = Join-Path ([IO.Path]::GetTempPath()) "metaphor-spike-$([guid]::NewGuid())"
$mod = Join-Path $work 'mod'
$collected = Join-Path $work 'collected'

# One line of the disk check: "ok   NAME: 1 match at 0xOFF (rva 0xRVA)", "FAIL NAME: N matches, ..." or "no match".
function Read-DiskLine([string] $Line) {
    if ($Line -match '^(ok  |FAIL) (?<name>\S+): (?:1 match at 0x[0-9a-f]+(?: \(rva 0x(?<rva>[0-9a-f]+)\))?|(?<count>\d+) matches|no match)') {
        [pscustomobject]@{
            Name  = $Matches.name
            Count = if ($Matches['count']) { [int]$Matches.count } elseif ($Line -match 'no match') { 0 } else { 1 }
            Rva   = $Matches['rva']
        }
    }
}

function Get-Verdict($Disk, $Memory) {
    if (-not $Disk -or -not $Memory) { return 'missing from one side' }
    if ($Disk.Count -eq 0 -and $Memory.matches -eq 0) { return 'absent from both' }
    if ($Disk.Count -eq 1 -and $Memory.matches -eq 1) {
        $memoryRva = '{0:x}' -f [long]$Memory.firstRvas[0]
        return $(if ($Disk.Rva -eq $memoryRva) { 'same place' } else { 'both once, different places' })
    }
    "differs ($($Disk.Count) on disk, $($Memory.matches) in memory)"
}

try {
    # 1. The mod, asked for one scan.
    dotnet publish $Project -c Release -o $mod -v q --nologo | Out-Host
    if ($LASTEXITCODE -ne 0) { Write-Host "signature spike can't start: the mod failed to build"; exit 2 }
    Copy-Item -LiteralPath $Probes (Join-Path $mod 'signature-probes.json')

    # 2. On disk. The smoke test resolves the game the same way; this needs it up front.
    if (-not (Test-Path -LiteralPath $ReloadedConfig)) { Write-Host "signature spike can't start: no Reloaded-II config at $ReloadedConfig"; exit 2 }
    $loaderPaths = Resolve-LoaderPaths (Read-Json $ReloadedConfig) $GameExe $LauncherExe
    if (-not $loaderPaths.GameExe -or -not (Test-Path -LiteralPath $loaderPaths.GameExe)) {
        Write-Host "signature spike can't start: no game executable (set -GameExe or METAPHOR_GAME_EXE)"; exit 2
    }
    $diskOutput = @(dotnet run --project (Join-Path $PSScriptRoot '../../tools/Metaphor.SignatureCheck') -c Release -v q -- `
            --probes $loaderPaths.GameExe $Probes)
    # 0: every probe matched once; 1: one didn't. Anything else (a build error, a crash) is output not to keep.
    if ($LASTEXITCODE -notin 0, 1) { Write-Host "signature spike can't start: the disk check failed`n$($diskOutput -join "`n")"; exit 2 }
    $diskOutput | Out-Host

    # 3. In the running game, with only our mod enabled.
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'smoke-test.ps1') -GameExe $loaderPaths.GameExe `
        -LauncherExe $loaderPaths.LauncherExe -ModFolders $mod -ReloadedConfig $ReloadedConfig -KnownBuilds $KnownBuilds `
        -TimeoutSeconds $TimeoutSeconds -CollectFiles 'signature-scan.json' -CollectTo $collected | Out-Host
    # Any failure, including a failed cleanup after the report was collected, stops here: check the game is closed and
    # the Reloaded-II config restored (see the smoke test's output above), then run again.
    if ($LASTEXITCODE -eq 2) { Write-Host "signature spike can't start: the smoke test couldn't (see above)"; exit 2 }
    if ($LASTEXITCODE -ne 0) { Write-Host 'signature spike FAILED: the smoke test failed (see above)'; exit 1 }
    $modId = (Read-Json (Join-Path $mod 'ModConfig.json')).ModId
    $reportFile = Join-Path $collected "$modId/signature-scan.json"
    if (-not (Test-Path -LiteralPath $reportFile)) { Write-Host 'signature spike FAILED: no scan report from the game'; exit 1 }
    $reportJson = Get-Content -Raw -LiteralPath $reportFile
    $report = $reportJson | ConvertFrom-Json

    # 4. The draft.
    $disk = @{}
    foreach ($line in $diskOutput) { if ($parsed = Read-DiskLine $line) { $disk[$parsed.Name] = $parsed } }
    $rows = foreach ($probe in $report.probes) {
        $d = $disk[$probe.name]
        # @($probe.firstRvas) and not $probe.firstRvas alone: PowerShell treats an array holding just 0 as false.
        $memoryAt = (@($probe.firstRvas) | ForEach-Object { '0x{0:x}' -f [long]$_ }) -join ', '
        "| ``$($probe.name)`` | $(if ($d) { $d.Count } else { '?' }) | $(if ($d -and $d.Rva) { "0x$($d.Rva)" }) | $($probe.matches) | $memoryAt | $(Get-Verdict $d $probe) |"
    }
    $commit = git -C $PSScriptRoot rev-parse --short HEAD
    # The file's name only, never its full path (ground rule 6), and where its probes came from.
    $probeSources = (@(Get-Content -Raw -LiteralPath $Probes | ConvertFrom-Json) | ForEach-Object { $_.source } |
        Select-Object -Unique | ForEach-Object { "``$_``" }) -join ', '
    $draft = @"
# Signature scans of the game on disk and in memory

- **Source:** a capture of one run of ``scripts/dev/signature-spike.ps1`` at commit ``$commit``, on the maintainer's
  machine. Probes: ``$(Split-Path -Leaf $Probes)`` ($probeSources).
- **Saved:** $(Get-Date -Format yyyy-MM-dd).
- **Game build:** ``$($report.gameBuildSha256)``.

## Steps

1. ``pwsh scripts/dev/signature-spike.ps1``, with the game and Reloaded-II closed. It ran the disk check
   (``tools/Metaphor.SignatureCheck --probes``), then the smoke test with only our mod enabled, whose mod copied the
   game's main module through ``ReadProcessMemory`` when Reloaded-II started it and scanned it (ADR 0002: read only).

## Result

The memory copy was made $($report.millisecondsSinceGameStart) ms after the game started: $($report.readableBytes) of
$($report.moduleBytes) bytes read, $($report.unreadableBytes) failed. Disk addresses are converted from file offsets
through the exe's section table.

| Probe | Disk matches | Disk RVA | Memory matches | Memory RVAs | Verdict |
|---|---|---|---|---|---|
$($rows -join "`n")

## Disk check output

``````text
$($diskOutput -join "`n")
``````

## Memory scan report

``````json
$($reportJson.Trim())
``````
"@
    # The draft is meant for docs/raw-sources/, so it must not name local folders (AGENTS.md ground rule 6).
    $localPaths = @($HOME, [IO.Path]::GetTempPath(), (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path, (Split-Path $loaderPaths.GameExe)) |
        Where-Object { $_ } | ForEach-Object { $_.TrimEnd('\', '/') }
    if ($leak = $localPaths | Where-Object { $draft.Contains($_, [StringComparison]::OrdinalIgnoreCase) -or $draft.Contains($_.Replace('\', '/'), [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1) {
        Write-Host 'signature spike FAILED: the draft would contain a local path; nothing was written'; exit 1
    }
    if (Test-Path -LiteralPath $OutFile) { Write-Host "signature spike FAILED: $OutFile already exists; nothing was written"; exit 1 }
    New-Item -ItemType Directory -Force (Split-Path $OutFile) | Out-Null
    Set-Content -LiteralPath $OutFile -Value $draft -Encoding utf8NoBOM
    Write-Host "draft written: $OutFile"
    exit 0
}
finally {
    # A failure here (say, a game still running holds a file) mustn't hide the message above.
    try { if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force } }
    catch { Write-Host "couldn't remove the temporary folder $work`: $($_.Exception.Message)" }
}
