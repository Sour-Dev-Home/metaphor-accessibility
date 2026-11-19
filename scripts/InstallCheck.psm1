# The players' install check (#283): says why the mod didn't load, for the one case the mod can't report on itself.
# check-install.ps1 prints and speaks what Get-InstallCheck finds. It reads files and lists processes and windows; it
# changes no file or setting. It reads nothing over the network: a Reloaded-II path on a network share or mapped
# network drive is reported, not read. In Windows PowerShell 5.1, looking for the JAWS and ZoomText windows compiles a
# small helper (Add-Type), which writes temporary files in the temp folder and removes them. Ships in the release
# beside the mod, so it must load in Windows PowerShell 5.1: ASCII only.
#
# The idea of a checker players run on their own install comes from BMMValidator
# (https://gitlab.com/BMMProject/Tools/BMMValidator, Apache-2.0, at d69404ee), a declarative install checker for other
# games' accessibility mods. No code is taken (docs/raw-sources/install-check-sources-2026-11-19.md).
#
# What it reads, all from the release folder it runs from (the package's own files, src/Metaphor.Hooks) and from
# Reloaded-II's records:
# - ReloadedII.json at its fixed AppData path, and the launcher its LauncherPath names;
# - the mods folder it names (ModConfigDirectory), for a ModConfig.json with this release's ModId, and the game's
#   AppConfig.json in the applications folder, for AppLocation and EnabledMods
#   (docs/raw-sources/reloaded-ii-config-location-2026-11-13.md, reloaded-ii-app-and-mod-config-2026-11-15.md);
# - the game exe's SHA-256 against known-game-builds.json, the same list the mod embeds (src/Metaphor.Core);
# - prism.dll's SHA-256 against prism.json, the pin the mod checks it by (ADR 0005);
# - mod-status.json, which the mod writes into its own folder at each start (Metaphor.Core.ModStatusFile);
# - running screen readers, as Prism finds them (Find-ScreenReader).
#
# Each check has a Line, spoken and printed, in plain words with no path or hash, and a Detail, printed only, with
# the path or hash a helper may ask for.

Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ReloadedRead.psm1')

# The readers the mod speaks through before Windows' own voices (ADR 0005's fixed list).
$SupportedReaders = 'NVDA, JAWS or ZoomText'
$CopyAgain = 'Copy the mod folder from the release zip into Reloaded-II''s mods folder again, with every file in it.'
$RunFromRelease = 'Run check-install.ps1 from the mod folder in the release zip.'
$KeepLocal = 'Keep Reloaded-II and the game on this computer''s own drives. This check does not read over the network.'

# Each running screen reader the mod supports, by name. JAWS and ZoomText by the windows Prism v0.18.3 looks for
# before it uses them; NVDA by its program, nvda.exe (docs/raw-sources/install-check-sources-2026-11-19.md). Prism
# asks NVDA itself over RPC, which a script can't do simply.
function Find-ScreenReader {
    if (@(Get-Process -Name nvda -ErrorAction SilentlyContinue).Count -gt 0) { 'NVDA' }
    try {
        if (-not ('MetaphorInstallCheck.Window' -as [type])) {
            Add-Type -Namespace MetaphorInstallCheck -Name Window -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
public static extern System.IntPtr FindWindow(string className, string windowName);
'@
        }
        if ([MetaphorInstallCheck.Window]::FindWindow('JFWUI2', $null) -ne [IntPtr]::Zero) { 'JAWS' }
        if ([MetaphorInstallCheck.Window]::FindWindow('ZXSPEECHWNDCLASS', 'ZoomText Speech Processor') -ne [IntPtr]::Zero) {
            'ZoomText'
        }
    }
    catch { Write-Verbose "could not look for JAWS and ZoomText windows: $_" }
}

# Whether to speak with Windows' voice, and the line saying why not. -NoSpeech wins, then -Speak. Without a console it
# prints only; with a screen reader running it prints only too, since the reader reads the window and two voices at
# once can't be followed.
function Select-Speech([bool] $Speak, [bool] $NoSpeech, [bool] $Interactive, [string[]] $Readers) {
    $result = { param($On, $Why) [pscustomobject]@{ Speak = $On; Message = $Why } }
    if ($NoSpeech) { return & $result $false $null }
    if ($Speak) { return & $result $true $null }
    if (-not $Interactive) { return & $result $false 'No console, so printing only.' }
    if (@($Readers).Count -gt 0) {
        return & $result $false "$(@($Readers)[0]) is running and reads this window, so printing only. Add -Speak to hear Windows' voice as well."
    }
    & $result $true $null
}

function New-Check([string] $Name, [string] $State, [string] $Line, [string] $Detail, [string] $Fix) {
    [pscustomobject]@{ Name = $Name; State = $State; Line = $Line; Detail = $Detail; Fix = $Fix }
}
function Pass([string] $Name, [string] $Line, [string] $Detail) { New-Check $Name 'pass' $Line $Detail $null }
function Problem([string] $Name, [string] $Line, [string] $Fix, [string] $Detail) { New-Check $Name 'problem' $Line $Detail $Fix }
function Skip([string] $Name, [string] $Line) { New-Check $Name 'not checked' $Line $null $null }

function Read-JsonOrNull([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    try { return Read-Json $Path } catch { return $null }
}

function Get-Property($Object, [string] $Name) {
    if ($null -ne $Object -and $Object.PSObject.Properties[$Name]) { return $Object.$Name }
    return $null
}

# Every check, in the order a fix is needed: { Name; State (pass, problem, note or not checked); Line; Detail; Fix }. A
# check that needs an earlier one to pass is "not checked". RELEASEFOLDER is the folder the script runs from,
# downloaded or installed; FINDSCREENREADER returns the names of the running readers.
function Get-InstallCheck([string] $ReleaseFolder, [string] $ConfigPath = (Get-ReloadedConfigPath),
    [scriptblock] $FindScreenReader = { Find-ScreenReader }) {
    $modConfig = Read-JsonOrNull (Join-Path $ReleaseFolder 'ModConfig.json')
    $modId = [string] (Get-Property $modConfig 'ModId')
    $modName = [string] (Get-Property $modConfig 'ModName')
    if (-not $modName) { $modName = $modId }

    # Reloaded-II.
    $loader = $null
    $launcher = $null
    if (Test-NetworkPath $ConfigPath) {
        Problem 'Reloaded-II' 'Reloaded-II''s settings file is on a network drive, so this check did not read it.' $KeepLocal "Settings file: $ConfigPath"
    }
    elseif (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
        Problem 'Reloaded-II' 'Reloaded-II is not installed, or has never been started.' 'Install Reloaded-II and start it once.' `
            "Settings file not found: $ConfigPath"
    }
    elseif ($null -eq ($loader = Read-JsonOrNull $ConfigPath)) {
        Problem 'Reloaded-II' 'Reloaded-II''s settings file could not be read.' 'Start Reloaded-II once, so it writes its settings file again.' `
            "Settings file: $ConfigPath"
    }
    elseif (Test-NetworkPath ($launcher = [string] (Get-Property $loader 'LauncherPath'))) {
        Problem 'Reloaded-II' 'Reloaded-II''s program is on a network drive, so this check did not look at it.' $KeepLocal "Program: $launcher"
    }
    elseif (-not $launcher -or -not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
        Problem 'Reloaded-II' 'Reloaded-II''s program is not where Reloaded-II last ran from.' 'Start Reloaded-II once from the folder it is in now.' `
            "Program not found: $launcher"
    }
    else { Pass 'Reloaded-II' 'Reloaded-II is set up.' }

    # The mod in the mods folder.
    $installed = $null
    $mods = [string] (Get-Property $loader 'ModConfigDirectory')
    if (-not $modId) {
        Problem 'Mod installed' 'This script is not in the mod''s folder, so it cannot tell which mod to look for.' $RunFromRelease `
            "No ModConfig.json naming the mod in: $ReleaseFolder"
    }
    elseif ($null -eq $loader) { Skip 'Mod installed' 'Whether the mod is installed, because Reloaded-II is not set up.' }
    elseif (Test-NetworkPath $mods) {
        Problem 'Mod installed' 'Reloaded-II''s mods folder is on a network drive, so this check did not look in it.' $KeepLocal "Mods folder: $mods"
    }
    elseif (-not $mods -or -not (Test-Path -LiteralPath $mods -PathType Container)) {
        Problem 'Mod installed' 'Reloaded-II''s mods folder is missing.' 'Start Reloaded-II once, so it makes its mods folder.' "Mods folder: $mods"
    }
    else {
        $installed = Get-ChildItem -LiteralPath $mods -Directory | Where-Object {
            [string] (Get-Property (Read-JsonOrNull (Join-Path $_.FullName 'ModConfig.json')) 'ModId') -eq $modId
        } | Select-Object -First 1
        if ($installed) { Pass 'Mod installed' 'The mod is in Reloaded-II''s mods folder.' "Folder: $($installed.FullName)" }
        else {
            Problem 'Mod installed' 'The mod is not in Reloaded-II''s mods folder.' `
                "Copy the $modId folder from the release zip into Reloaded-II's mods folder." "Mods folder: $mods"
        }
    }

    # The game in Reloaded-II, and the mod turned on for it.
    $application = $null
    $apps = [string] (Get-Property $loader 'ApplicationConfigDirectory')
    if ($null -eq $loader) { Skip 'Game added' 'Whether the game is in Reloaded-II, because Reloaded-II is not set up.' }
    elseif (Test-NetworkPath $apps) {
        Problem 'Game added' 'Reloaded-II''s list of games is on a network drive, so this check did not read it.' $KeepLocal "Applications folder: $apps"
    }
    else {
        if ($apps -and (Test-Path -LiteralPath $apps -PathType Container)) { $application = Find-MetaphorApplication $loader }
        if ($application) { Pass 'Game added' 'The game is added to Reloaded-II.' }
        else { Problem 'Game added' 'The game is not added to Reloaded-II.' 'In Reloaded-II, add the game''s program, METAPHOR.exe.' }
    }
    $app = if ($application) { Read-JsonOrNull $application.FullName } else { $null }
    if (-not $application) { Skip 'Mod enabled' 'Whether the mod is turned on, because the game is not in Reloaded-II.' }
    elseif (@(Get-Property $app 'EnabledMods') -contains $modId) { Pass 'Mod enabled' 'The mod is turned on for the game.' }
    else {
        Problem 'Mod enabled' 'The mod is not turned on for the game in Reloaded-II.' `
            "Turn the mod on: in Reloaded-II, open the game's page and tick $modName in its list of mods. The install guide has the steps for a screen reader."
    }

    # The game's build.
    $gameBuild = $null
    $location = [string] (Get-Property $app 'AppLocation')
    $gameExe = if ($location) { Resolve-AppLocation $application.FullName $location } else { $null }
    $knownBuilds = Join-Path $ReleaseFolder 'known-game-builds.json'
    if (-not $application) { Skip 'Game version' 'The game''s version, because the game is not in Reloaded-II.' }
    elseif (Test-NetworkPath $gameExe) {
        Problem 'Game version' 'The game is on a network drive, so this check did not read it.' $KeepLocal "Game program: $gameExe"
    }
    elseif (-not $gameExe -or -not (Test-Path -LiteralPath $gameExe -PathType Leaf)) {
        Problem 'Game version' 'The game''s program was not found where Reloaded-II points.' `
            'In Reloaded-II, point the game at METAPHOR.exe in the game''s install folder.' "Reloaded-II points at: $location"
    }
    elseif (-not (Test-Path -LiteralPath $knownBuilds -PathType Leaf)) {
        Problem 'Game version' 'This script''s folder has no list of game versions.' $RunFromRelease "Missing: $knownBuilds"
    }
    else {
        $gameBuild = Get-GameBuild $gameExe $knownBuilds
        if ($gameBuild.Known) {
            Pass 'Game version' "The game's version is one the mod knows, Steam build $(@($gameBuild.Known)[0].steamBuildId)."
        }
        else {
            Problem 'Game version' 'The game is a version this mod does not know yet, so the mod turns itself off.' `
                'Look for a newer release of the mod for this game version.' "Game SHA-256: $($gameBuild.Sha256)"
        }
    }

    # The speech library, in the folder Reloaded-II loads from, or this one while the mod isn't installed.
    $folder = if ($installed) { $installed.FullName } else { $ReleaseFolder }
    $prism = Join-Path $folder 'prism.dll'
    $pin = [string] (Get-Property (Read-JsonOrNull (Join-Path $ReleaseFolder 'prism.json')) 'dllSha256')
    if (-not (Test-Path -LiteralPath $prism -PathType Leaf)) {
        Problem 'Speech library' 'The speech library, prism.dll, is missing from the mod''s folder.' $CopyAgain "Folder: $folder"
    }
    elseif (-not $pin) {
        Problem 'Speech library' 'This script''s folder has nothing to check prism.dll by.' $RunFromRelease "Missing: prism.json in $ReleaseFolder"
    }
    elseif ((Get-Sha256 $prism) -ne $pin) {
        Problem 'Speech library' 'The speech library, prism.dll, is not the one this release ships, so the mod will not use it.' $CopyAgain `
            "File: $prism"
    }
    else { Pass 'Speech library' 'The speech library, prism.dll, is in place.' }

    # The last start.
    $statusFile = if ($installed) { Join-Path $installed.FullName $StatusFileName } else { $null }
    $status = if ($statusFile) { Read-JsonOrNull $statusFile } else { $null }
    $again = 'Start the game through Reloaded-II, then run this check again.'
    if (-not $installed) { Skip 'Last start' 'How the mod last started, because it is not installed.' }
    elseif (-not (Test-Path -LiteralPath $statusFile -PathType Leaf)) {
        Problem 'Last start' 'The mod has not reported a start. Either the game has not been started with the mod since it was installed, or Reloaded-II did not load it.' `
            $again "No $StatusFileName in: $($installed.FullName)"
    }
    elseif ($null -eq $status) { Problem 'Last start' 'The mod''s status file could not be read.' $again "File: $statusFile" }
    elseif ((Get-Property $status 'state') -eq 'ready') {
        $line = "The last time the game started, the mod loaded, with $(Get-Property $status 'hooksLoaded') of $(Get-Property $status 'hooksExpected') hooks."
        if ($gameBuild -and (Get-Property $status 'gameBuildSha256') -ne $gameBuild.Sha256) { $line += ' The game has changed since then.' }
        Pass 'Last start' $line
    }
    else {
        # Only the reason for an unknown build (Metaphor.Core.ModStartup.UnknownGameBuildReason) is spoken: the others
        # can hold an exception's message, with paths in it.
        $reason = [string] (Get-Property $status 'reason')
        $spoken = if ($reason -eq 'unknown game build') { ' because it does not know this game version' } else { '' }
        Problem 'Last start' "The last time the game started, the mod turned itself off$spoken." `
            'Fix the problem the mod gave, then start the game through Reloaded-II again.' "The mod's reason: $reason"
    }

    # A screen reader: a note, not a problem, since the mod falls back to Windows' own voices (ADR 0005).
    $readers = @(& $FindScreenReader)
    if ($readers.Count -gt 0) {
        Pass 'Screen reader' "$($readers -join ' and ') $(if ($readers.Count -gt 1) { 'are' } else { 'is' }) running; the mod speaks through it."
    }
    else {
        New-Check 'Screen reader' 'note' "No screen reader the mod supports is running. In the game the mod speaks through $SupportedReaders; without one it uses Windows' own voice. Start your screen reader before the game to hear the mod through it." $null $null
    }
}

function Get-Label($Check) {
    switch ($Check.State) { 'pass' { 'Pass' } 'problem' { 'Problem' } 'note' { 'Note' } default { 'Not checked' } }
}

# One check as it is printed: the spoken words, then any path or hash.
function Format-Check($Check) {
    $text = "$(Get-Label $Check): $($Check.Line)"
    if ($Check.Detail) { $text += " $($Check.Detail)" }
    $text
}

# One check as it is spoken: words only, no path or hash.
function Format-SpokenCheck($Check) { "$(Get-Label $Check): $($Check.Line)" }

# The last line, printed and spoken: the first problem's fix, or that everything is in order.
function Get-InstallSummary($Checks) {
    $first = @($Checks | Where-Object State -EQ 'problem') | Select-Object -First 1
    if ($first) { return "First thing to fix: $($first.Fix)" }
    'Everything this script checks is in order. If the mod is still silent, report it with the alpha feedback form.'
}

Export-ModuleMember -Function Get-InstallCheck, Get-InstallSummary, Format-Check, Format-SpokenCheck, Find-ScreenReader, Select-Speech
