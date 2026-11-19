# Reloaded-II helpers shared by smoke-test.ps1, deploy-dev.ps1, listen.ps1 and capture.ps1.
# Loader facts: docs/raw-sources/reloaded-ii-config-location-2026-11-13.md and
# reloaded-ii-app-and-mod-config-2026-11-15.md.

Set-StrictMode -Version Latest

$StatusFileName = 'mod-status.json' # Metaphor.Core.ModStatusFile.FileName
$PollMilliseconds = 100
# How long the game must stay gone before a wait counts it as exited: at least the grace that two missed polls 500 ms
# apart gave before #339 made polling faster.
$GameGoneMilliseconds = 1000

# Dates stay strings, so a config written back keeps them as the loader wrote them.
function Read-Json([string] $Path) { Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json -DateKind String }

function Write-Json([string] $Path, $Value) {
    New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
    $Value | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}

# Reloaded-II's root config, at a fixed AppData path the loader doesn't let us move
# (reloaded-ii-config-location-2026-11-13.md). Scripts that take it as a parameter default spell it out, since a
# default is evaluated before the module loads.
function Get-ReloadedConfigPath {
    Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Reloaded-Mod-Loader-II/ReloadedII.json'
}

# As Reloaded-II's ApplicationConfig.GetAbsoluteAppLocation: a relative AppLocation is relative to its AppConfig.json.
function Resolve-AppLocation([string] $ConfigFile, [string] $Location) {
    $folder = Split-Path $ConfigFile
    if (-not [IO.Path]::IsPathRooted($Location)) { $Location = Join-Path $folder $Location }
    elseif ([IO.Path]::GetPathRoot($Location) -eq '\') { $Location = Join-Path ([IO.Path]::GetPathRoot($folder)) $Location.TrimStart('\') }
    [IO.Path]::GetFullPath($Location)
}

# The running processes started from this exe. Only those with the exe's name are asked for their path: asking all of
# them took up to 1.7 s on a busy machine, against about 6 ms this way (#339). A process's name is its exe's file name
# without ".exe".
function Get-Game([string] $Path) {
    [Diagnostics.Process]::GetProcessesByName(([IO.Path]::GetFileName($Path) -replace '\.exe$')) | Where-Object { $_.Path -eq $Path }
}

# The game exe's SHA-256, and its entry in the known-builds list ($null when it isn't listed).
function Get-GameBuild([string] $GameExe, [string] $KnownBuilds) {
    $sha256 = (Get-FileHash -LiteralPath $GameExe -Algorithm SHA256).Hash.ToLowerInvariant()
    [pscustomobject]@{ Sha256 = $sha256; Known = @(Read-Json $KnownBuilds) | Where-Object sha256 -EQ $sha256 }
}

# The AppConfig.json file registered for the game exe, as --launch finds it ($null when none is).
function Find-Application($Loader, [string] $GameExe) {
    Get-ChildItem -LiteralPath $Loader.ApplicationConfigDirectory -Filter AppConfig.json -Recurse -Depth 1 |
        Where-Object { (Resolve-AppLocation $_.FullName (Read-Json $_.FullName).AppLocation) -ceq $GameExe } |
        Select-Object -First 1
}

# The launcher and game exe to use: those given, or else Reloaded-II's own records of them. The launcher from
# ReloadedII.json's LauncherPath (rewritten on each launch: reloaded-ii-config-location-2026-11-13.md), the game from the
# application registered as metaphor.exe (the AppId Metaphor mods target: reloaded-ii-mod-entry-point-2026-11-15.md).
function Resolve-LoaderPaths($Loader, [string] $GameExe, [string] $LauncherExe) {
    if (-not $LauncherExe -and $Loader.PSObject.Properties['LauncherPath']) { $LauncherExe = $Loader.LauncherPath }
    if (-not $GameExe) {
        $game = Get-ChildItem -LiteralPath $Loader.ApplicationConfigDirectory -Filter AppConfig.json -Recurse -Depth 1 |
            Where-Object { (Read-Json $_.FullName).AppId -eq 'metaphor.exe' } | Select-Object -First 1
        if ($game) { $GameExe = Resolve-AppLocation $game.FullName (Read-Json $game.FullName).AppLocation }
    }
    [pscustomobject]@{ GameExe = $GameExe; LauncherExe = $LauncherExe }
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
