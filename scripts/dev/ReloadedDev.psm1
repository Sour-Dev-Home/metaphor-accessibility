# The developer scripts' module (#412), under GameSession.psm1 (scripts/dev/session.ps1) and ghidra.ps1: the Reloaded-II
# reading and the game locator from the player module (MetaphorInstall.psm1, re-exported here), and the developer
# helpers, which write configs, find the game's process and keep local paths out of output. Doesn't ship.
# Loader facts: docs/raw-sources/reloaded-ii-config-location-2026-11-13.md and
# reloaded-ii-app-and-mod-config-2026-11-15.md.

Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot '../player/MetaphorInstall.psm1')

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

# The AppConfig.json file registered for the game exe ($null when none is), with the exe's path as registered in its
# GameExe property. Windows paths ignore case, so the match does too (#457); --launch compares the path exactly
# (reloaded-ii-app-and-mod-config-2026-11-15.md), so a launch passes the registered one.
function Find-Application($Loader, [string] $GameExe) {
    foreach ($file in Get-ChildItem -LiteralPath $Loader.ApplicationConfigDirectory -Filter AppConfig.json -Recurse -Depth 1) {
        $registered = Resolve-AppLocation $file.FullName (Read-Json $file.FullName).AppLocation
        if ($registered -eq $GameExe) { return $file | Add-Member -NotePropertyName GameExe -NotePropertyValue $registered -PassThru }
    }
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

# The interface: reading the loader's records (from the player module), then the developer helpers, then keeping local
# paths out of what a script prints. Starting the game and waiting on the mods is GameSession.psm1's (#464).
Export-ModuleMember -Function Read-Json, Resolve-LoaderPaths, Get-GameBuild, Find-MetaphorGame, Find-ReloadedLauncher,
    Get-Game, Find-Application, Write-Json, Add-PathName, Hide-LocalPaths -Variable StatusFileName
