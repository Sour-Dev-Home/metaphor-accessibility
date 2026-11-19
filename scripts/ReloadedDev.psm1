# Reloaded-II helpers shared by smoke-test.ps1, deploy-dev.ps1, listen.ps1 and capture.ps1: the read-only ones from
# ReloadedRead.psm1 (re-exported here), and the developer ones, which write configs and start the game.
# Loader facts: docs/raw-sources/reloaded-ii-config-location-2026-11-13.md and
# reloaded-ii-app-and-mod-config-2026-11-15.md.

Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ReloadedRead.psm1')

$PollMilliseconds = 100
# How long the game must stay gone before a wait counts it as exited: at least the grace that two missed polls 500 ms
# apart gave before #339 made polling faster.
$GameGoneMilliseconds = 1000

function Write-Json([string] $Path, $Value) {
    New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
    $Value | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}

# The running processes started from this exe. Only those with the exe's name are asked for their path: asking all of
# them took up to 1.7 s on a busy machine, against about 6 ms this way (#339). A process's name is its exe's file name
# without ".exe".
function Get-Game([string] $Path) {
    [Diagnostics.Process]::GetProcessesByName(([IO.Path]::GetFileName($Path) -replace '\.exe$')) | Where-Object { $_.Path -eq $Path }
}

# The AppConfig.json file registered for the game exe, as --launch finds it ($null when none is).
function Find-Application($Loader, [string] $GameExe) {
    Get-ChildItem -LiteralPath $Loader.ApplicationConfigDirectory -Filter AppConfig.json -Recurse -Depth 1 |
        Where-Object { (Resolve-AppLocation $_.FullName (Read-Json $_.FullName).AppLocation) -ceq $GameExe } |
        Select-Object -First 1
}

# Waits for each mod's status file (each $Mods item has Id and Folder), the game exiting, or the deadline, then prints
# one line per mod. True when every mod is ready on $Build with all its hooks.
function Wait-ModStatus($Mods, [string] $GameExe, [string] $Build, [datetime] $Deadline, [int] $TimeoutSeconds) {
    $passed = $true
    $goneFor = $null
    do {
        $pending = @($Mods | Where-Object { -not (Test-Path -LiteralPath (Join-Path $_.Folder $StatusFileName)) })
        if ($pending.Count -eq 0) { break }
        if (Get-Game $GameExe) { $goneFor = $null }
        elseif (-not $goneFor) { $goneFor = [Diagnostics.Stopwatch]::StartNew() }
        elseif ($goneFor.ElapsedMilliseconds -ge $GameGoneMilliseconds) {
            Write-Host 'the game exited before every mod reported'; $passed = $false; break
        }
        Start-Sleep -Milliseconds $PollMilliseconds
    } while ((Get-Date) -lt $Deadline)

    foreach ($mod in $Mods) {
        $file = Join-Path $mod.Folder $StatusFileName
        if (-not (Test-Path -LiteralPath $file)) {
            Write-Host "$($mod.Id): no status within $TimeoutSeconds s"
            $passed = $false
            continue
        }
        $status = Read-Json $file
        if ($status.modId -ne $mod.Id) {
            Write-Host "$($mod.Id): status file names another mod, $($status.modId)"
            $passed = $false
        }
        elseif ($status.state -eq 'ready') {
            Write-Host "$($mod.Id): ready on $($status.gameBuildSha256), hooks $($status.hooksLoaded)/$($status.hooksExpected)"
            if ($status.hooksLoaded -ne $status.hooksExpected -or $status.gameBuildSha256 -ne $Build) { $passed = $false }
        }
        else {
            Write-Host "$($mod.Id): $($status.state) on $($status.gameBuildSha256) ($($status.reason))"
            $passed = $false
        }
    }
    $passed
}

# Launches the game through Reloaded-II (--launch) and waits for the launcher, which exits by itself once the game has
# started. False when it doesn't exit in time.
function Start-WithLoader([string] $LauncherExe, [string] $GameExe, [int] $TimeoutSeconds) {
    $launcher = Start-Process -FilePath $LauncherExe -ArgumentList '--launch', "`"$GameExe`"" -PassThru
    $launcher.WaitForExit($TimeoutSeconds * 1000)
}

# Each folder that can show up in a script's output, with the name it is shown as, so the output can go into an issue
# or a raw source as it is. Longer paths go first, so the mods folder inside the Reloaded-II folder is named as itself.
function Add-PathName($Names, [string] $Name, [string] $Path) {
    if ($Path) { $Names[$Name] = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/') }
}

function Hide-LocalPaths($Names, [string] $Text) {
    foreach ($entry in $Names.GetEnumerator() | Sort-Object { $_.Value.Length } -Descending) {
        foreach ($form in $entry.Value, $entry.Value.Replace('\', '/')) {
            $Text = [regex]::Replace($Text, [regex]::Escape($form), $entry.Key, 'IgnoreCase')
        }
    }
    $Text
}

# The record counts in a message log, by the word after "<n> ms" (Metaphor.Core.MessageLog), and its requests by the
# function named in via=. Source lines ("  | ...") are counted, not shown, and no line is printed: the log holds game text.
function Get-MessageLogSummary([string] $Path) {
    # The game holds the log open for writing while it runs.
    $stream = [IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
    try { $lines = (New-Object IO.StreamReader($stream)).ReadToEnd() -split "\r?\n" }
    finally { $stream.Dispose() }

    $types = @{}
    $requests = @{}
    $sourceLines = 0
    $unrecognised = 0
    foreach ($line in $lines) {
        if ($line -eq '') { continue }
        if ($line.StartsWith('  | ')) { $sourceLines++; continue }
        if ($line -match '^\d+ ms ([a-z]+)\b') {
            $types[$Matches[1]] = 1 + $types[$Matches[1]]
            if ($Matches[1] -eq 'request' -and $line -match ' via=([A-Za-z0-9_]+)') { $requests[$Matches[1]] = 1 + $requests[$Matches[1]] }
            continue
        }
        $unrecognised++
    }

    $counts = foreach ($type in $types.Keys | Sort-Object) {
        if ($type -eq 'compile') { "$($types[$type]) compile ($sourceLines source lines)" } else { "$($types[$type]) $type" }
    }
    if ($unrecognised) { $counts = @($counts) + "$unrecognised unrecognised line$(if ($unrecognised -ne 1) { 's' })" }
    "message log: $(if ($counts) { $counts -join ', ' } else { 'empty' })"
    if ($requests.Count) {
        "requests by function: $(($requests.Keys | Sort-Object | ForEach-Object { "$_ $($requests[$_])" }) -join ', ')"
    }
}

Export-ModuleMember -Function * -Variable StatusFileName
