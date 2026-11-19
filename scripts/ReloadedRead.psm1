# Read-only Reloaded-II helpers: what the players' install check (#283) and installer (#303) need, and ship with. Nothing here writes,
# starts a process or reaches the network; ReloadedDev.psm1 imports this and adds the developer helpers, which don't
# ship. Loads in Windows PowerShell 5.1, so ASCII only.
# Loader facts: docs/raw-sources/reloaded-ii-config-location-2026-11-13.md and
# reloaded-ii-app-and-mod-config-2026-11-15.md.

Set-StrictMode -Version Latest

$StatusFileName = 'mod-status.json' # Metaphor.Core.ModStatusFile.FileName

# Dates stay strings, so a config written back keeps them as the loader wrote them. Windows PowerShell 5.1 has no
# -DateKind and leaves dates as strings anyway. An array comes back one item at a time in both.
$JsonKeepsDates = (Get-Command ConvertFrom-Json).Parameters.ContainsKey('DateKind')
function Read-Json([string] $Path) {
    $text = Get-Content -Raw -LiteralPath $Path
    if ($JsonKeepsDates) { $text | ConvertFrom-Json -DateKind String } else { foreach ($item in ($text | ConvertFrom-Json)) { $item } }
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

# True when reading PATH could go over the network: a UNC path (\\host\share, or \\?\UNC\...) or a mapped network
# drive. Decided from the text and the drive's type, without touching the path itself.
function Test-NetworkPath([string] $Path) {
    if (-not $Path) { return $false }
    if ($Path -match '^[\\/]{2}') { return $true }
    if ($Path -match '^([A-Za-z]):') {
        try { return ([IO.DriveInfo]::new($Matches[1])).DriveType -eq [IO.DriveType]::Network } catch { return $false }
    }
    return $false
}

# A file's SHA-256 in lowercase hex. Through .NET rather than Get-FileHash: Windows PowerShell 5.1 started from
# PowerShell 7 inherits 7's module path and can't load Get-FileHash's module
# (docs/raw-sources/install-check-sources-2026-11-19.md).
function Get-Sha256([string] $Path) {
    $stream = [IO.File]::OpenRead($Path)
    $hash = [Security.Cryptography.SHA256]::Create()
    try { -join ($hash.ComputeHash($stream) | ForEach-Object { $_.ToString('x2') }) }
    finally { $hash.Dispose(); $stream.Dispose() }
}

# The game exe's SHA-256, and its entry in the known-builds list ($null when it isn't listed).
function Get-GameBuild([string] $GameExe, [string] $KnownBuilds) {
    $sha256 = Get-Sha256 $GameExe
    [pscustomobject]@{ Sha256 = $sha256; Known = @(Read-Json $KnownBuilds) | Where-Object sha256 -EQ $sha256 }
}

# The AppConfig.json file of the application registered as metaphor.exe, the AppId Metaphor mods target
# (reloaded-ii-mod-entry-point-2026-11-15.md); $null when none is.
function Find-MetaphorApplication($Loader) {
    Get-ChildItem -LiteralPath $Loader.ApplicationConfigDirectory -Filter AppConfig.json -Recurse -Depth 1 |
        Where-Object { (Read-Json $_.FullName).AppId -eq 'metaphor.exe' } | Select-Object -First 1
}

# The launcher and game exe to use: those given, or else Reloaded-II's own records of them. The launcher from
# ReloadedII.json's LauncherPath (rewritten on each launch: reloaded-ii-config-location-2026-11-13.md), the game from the
# application registered as metaphor.exe (the AppId Metaphor mods target: reloaded-ii-mod-entry-point-2026-11-15.md).
function Resolve-LoaderPaths($Loader, [string] $GameExe, [string] $LauncherExe) {
    if (-not $LauncherExe -and $Loader.PSObject.Properties['LauncherPath']) { $LauncherExe = $Loader.LauncherPath }
    if (-not $GameExe) {
        $game = Find-MetaphorApplication $Loader
        if ($game) { $GameExe = Resolve-AppLocation $game.FullName (Read-Json $game.FullName).AppLocation }
    }
    [pscustomobject]@{ GameExe = $GameExe; LauncherExe = $LauncherExe }
}

Export-ModuleMember -Function * -Variable StatusFileName
