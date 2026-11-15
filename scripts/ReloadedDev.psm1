# Reloaded-II helpers shared by smoke-test.ps1 and deploy-dev.ps1.
# Loader facts: docs/raw-sources/reloaded-ii-config-location-2026-11-13.md and
# reloaded-ii-app-and-mod-config-2026-11-15.md.

Set-StrictMode -Version Latest

$StatusFileName = 'mod-status.json' # Metaphor.Core.ModStatusFile.FileName
$PollMilliseconds = 500

# Dates stay strings, so a config written back keeps them as the loader wrote them.
function Read-Json([string] $Path) { Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json -DateKind String }

function Write-Json([string] $Path, $Value) {
    New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
    $Value | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}

# As Reloaded-II's ApplicationConfig.GetAbsoluteAppLocation: a relative AppLocation is relative to its AppConfig.json.
function Resolve-AppLocation([string] $ConfigFile, [string] $Location) {
    $folder = Split-Path $ConfigFile
    if (-not [IO.Path]::IsPathRooted($Location)) { $Location = Join-Path $folder $Location }
    elseif ([IO.Path]::GetPathRoot($Location) -eq '\') { $Location = Join-Path ([IO.Path]::GetPathRoot($folder)) $Location.TrimStart('\') }
    [IO.Path]::GetFullPath($Location)
}

# The running processes started from this exe.
function Get-Game([string] $Path) {
    Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $Path }
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
    $missedPolls = 0
    do {
        $pending = @($Mods | Where-Object { -not (Test-Path -LiteralPath (Join-Path $_.Folder $StatusFileName)) })
        if ($pending.Count -eq 0) { break }
        $missedPolls = if (Get-Game $GameExe) { 0 } else { $missedPolls + 1 }
        if ($missedPolls -ge 2) { Write-Host 'the game exited before every mod reported'; $passed = $false; break }
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

Export-ModuleMember -Function * -Variable StatusFileName
