# Finds the game and Reloaded-II on this machine (#267). One module for every script that needs them: onboard.ps1 now,
# and the players' install check (#283) and installer (#303) when they come.
#
# The game: METAPHOR_GAME_EXE, then the game registered in Reloaded-II (the precedence deploy-dev.ps1 and listen.ps1
# use, through ReloadedDev.psm1's Resolve-LoaderPaths), then METAPHOR.exe in Steam's libraries. How Steam lists its
# libraries: docs/raw-sources/steam-library-folders-2026-11-18.md.
# Reloaded-II: METAPHOR_RELOADED_EXE, then LauncherPath in ReloadedII.json, which every launch rewrites
# (docs/raw-sources/reloaded-ii-config-location-2026-11-13.md). There's no install folder to probe: Reloaded-II is a
# Release.zip extracted wherever the user chooses (docs/raw-sources/reloaded-ii-log-on-normal-run-2026-11-14.md, step
# 2), so one that has never been launched isn't found.

Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ReloadedDev.psm1')

$GameExeName = 'METAPHOR.exe'

# Steam's folder from the registry, or $null when Steam isn't installed.
function Get-SteamRoot {
    $key = Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue
    if ($key -and $key.PSObject.Properties['SteamPath']) { return [IO.Path]::GetFullPath($key.SteamPath) }
    return $null
}

# Each Steam library folder that exists: Steam's own, then those in libraryfolders.vdf, once each.
function Get-SteamLibrary([string] $SteamRoot) {
    if (-not $SteamRoot -or -not (Test-Path -LiteralPath $SteamRoot -PathType Container)) { return }
    $folders = [Collections.Generic.List[string]]::new()
    $folders.Add($SteamRoot)
    $vdf = Join-Path $SteamRoot 'steamapps/libraryfolders.vdf'
    if (Test-Path -LiteralPath $vdf) {
        foreach ($line in Get-Content -LiteralPath $vdf) {
            if ($line -match '^\s*"path"\s+"(.+)"\s*$') { $folders.Add(($Matches[1] -replace '\\\\', '\')) }
        }
    }
    $seen = @{}
    foreach ($folder in $folders) {
        if (-not (Test-Path -LiteralPath $folder -PathType Container)) { continue }
        $full = [IO.Path]::GetFullPath($folder).TrimEnd('\')
        if (-not $seen.ContainsKey($full)) { $seen[$full] = $true; $full }
    }
}

# Each game exe in a Steam library's steamapps\common\<folder>, whatever the folder is called.
function Find-SteamGame([string] $SteamRoot) {
    foreach ($library in Get-SteamLibrary $SteamRoot) {
        $common = Join-Path $library 'steamapps\common'
        if (-not (Test-Path -LiteralPath $common)) { continue }
        foreach ($folder in Get-ChildItem -LiteralPath $common -Directory) {
            $exe = Join-Path $folder.FullName $GameExeName
            if (Test-Path -LiteralPath $exe -PathType Leaf) { $exe }
        }
    }
}

# The game and launcher Reloaded-II records (Resolve-LoaderPaths), each $null when it isn't recorded. The environment
# variables are left to the callers, which ask about them differently.
function Get-LoaderRecord([string] $ConfigPath) {
    $none = [pscustomobject]@{ GameExe = $null; LauncherExe = $null }
    if (-not $ConfigPath -or -not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) { return $none }
    try { return Resolve-LoaderPaths (Read-Json $ConfigPath) $null $null } catch { return $none }
}

function Test-File([string] $Path) { [bool]($Path -and (Test-Path -LiteralPath $Path -PathType Leaf)) }

# Reloaded-II's launcher: METAPHOR_RELOADED_EXE, then the config's LauncherPath; $null when neither exists.
function Find-ReloadedLauncher([string] $ConfigPath = (Get-ReloadedConfigPath)) {
    if (Test-File $env:METAPHOR_RELOADED_EXE) { return $env:METAPHOR_RELOADED_EXE }
    $launcher = (Get-LoaderRecord $ConfigPath).LauncherExe
    if (Test-File $launcher) { return $launcher }
    return $null
}

# The game exe to use, as { Path; Saved }: Saved is true when it came from METAPHOR_GAME_EXE. $null when there is none.
# Otherwise it offers what Reloaded-II and Steam know of: one is shown and confirmed ([Y/n]), several are listed to pick
# from, none asks for the path. ASK takes a question and returns the answer, '' for Enter, or $null for no answer
# (no console), which counts as no.
function Select-MetaphorGame([scriptblock] $Ask, [string] $SteamRoot = (Get-SteamRoot),
    [string] $ConfigPath = (Get-ReloadedConfigPath)) {
    if (Test-File $env:METAPHOR_GAME_EXE) {
        Write-Host "METAPHOR_GAME_EXE already points at the game: $env:METAPHOR_GAME_EXE"
        return [pscustomobject]@{ Path = $env:METAPHOR_GAME_EXE; Saved = $true }
    }
    if ($env:METAPHOR_GAME_EXE) { Write-Host 'METAPHOR_GAME_EXE points at a file that is not there; looking again.' }

    $found = [Collections.Generic.List[string]]::new()
    foreach ($exe in @((Get-LoaderRecord $ConfigPath).GameExe) + @(Find-SteamGame $SteamRoot)) {
        if (-not (Test-File $exe)) { continue }
        $full = [IO.Path]::GetFullPath($exe)
        if (-not ($found | Where-Object { $_ -eq $full })) { $found.Add($full) }
    }

    $path = $null
    if ($found.Count -eq 1) {
        Write-Host "Found the game: $($found[0])"
        $answer = & $Ask 'Use it, saving its path in your Windows user''s METAPHOR_GAME_EXE environment variable? [Y/n]'
        if ($null -ne $answer -and $answer -match '^\s*(y|yes)?\s*$') { $path = $found[0] }
    }
    elseif ($found.Count -gt 1) {
        Write-Host 'Found the game in more than one place:'
        for ($i = 0; $i -lt $found.Count; $i++) { Write-Host "  $($i + 1). $($found[$i])" }
        $answer = & $Ask "Which one? [1-$($found.Count), Enter to skip]"
        if ($answer -match '^\s*\d+\s*$' -and [int]$answer -ge 1 -and [int]$answer -le $found.Count) {
            $path = $found[[int]$answer - 1]
        }
    }
    else {
        Write-Host "Did not find $GameExeName in Reloaded-II or a Steam library. You do not need the game to contribute."
        $answer = & $Ask "Full path to $GameExeName (Enter to skip):"
        $typed = if ($answer) { $answer.Trim().Trim('"') } else { '' }
        if ($typed -and (Split-Path -Leaf $typed) -eq $GameExeName -and (Test-File $typed)) { $path = $typed }
        elseif ($typed) { Write-Host "That is not a $GameExeName that exists; skipping the game." }
    }
    if ($path) { return [pscustomobject]@{ Path = $path; Saved = $false } }
    return $null
}

Export-ModuleMember -Function Select-MetaphorGame, Find-ReloadedLauncher
