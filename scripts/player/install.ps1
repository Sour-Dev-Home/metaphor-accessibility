<#
.SYNOPSIS
Installs the mod by ear (#303): Reloaded-II if it's missing, the mod, turned on for the game, and a desktop shortcut that
starts the game with it. Run it from the mod's folder in the release zip.

.DESCRIPTION
It says each step as it goes, through Windows' own voice as well as on screen, and whether it worked. A step that fails
says what to do and stops the steps after it. Running it again repairs or updates an install: a step with nothing to do
says so, and nothing it didn't put there is removed.

1. Finds the game: the one added to Reloaded-II, else METAPHOR.exe in a Steam library (it asks before using it), else
   the path you type.
2. Reloaded-II: uses the one installed, or downloads Reloaded-II 1.31.0 from its GitHub releases and unpacks it into
   your user folder (%LOCALAPPDATA%\Programs\Reloaded-II). No admin rights needed.
3. Reloaded-II's settings: writes a settings file only when Reloaded-II has never started.
4. and 5. The .NET 9 Desktop Runtime and the Visual C++ Redistributable, which Reloaded-II needs: when one is missing
   it downloads Microsoft's installer and runs it quietly. These two need admin rights: Windows asks for permission.
6. Reloaded-II's hooks library, which the mod needs: downloaded from its GitHub releases when missing.
7. Copies the mod into Reloaded-II's mods folder: the files package-files.txt lists, only those missing or differing.
8. Adds the game to Reloaded-II with the mod turned on, keeping the other mods you have turned on.
9. Makes the desktop shortcut "Metaphor ReFantazio with accessibility", which starts the game through Reloaded-II.
10. Runs the install check (check-install.ps1's checks) and says what it found.

It asks before it starts, and again before each download. Every download is pinned in install-pins.json to one version
and its SHA-256 (and Microsoft's signature for the two runtimes), and a file that doesn't match is deleted unused. It
refuses to run as administrator. With no answer to give (no console, -NonInteractive),
every question counts as no. With a screen reader running it prints only, and the reader reads the window; -Speak adds
Windows' voice anyway. Runs in Windows PowerShell 5.1 and PowerShell 7.

Exit codes: 0 installed (the check may still say the game hasn't started with the mod yet); 1 a step failed, an answer
was no, or the check found a problem.

With -Uninstall it removes the mod instead (#575), saying each step the same way: it turns the mod off for the game,
removes the mod's folder and its settings from Reloaded-II and deletes the desktop shortcut. It asks before removing a
Reloaded-II this installer installed, defaulting to keep, and keeps one it didn't install and both runtimes, which other
programs may use. It removes nothing outside Reloaded-II's folder and nothing that holds a link. A second run says
there was nothing left to remove. Exit codes: 0 done; 1 the answer was no, or a step failed (the others still run).

.EXAMPLE
powershell -ExecutionPolicy Bypass -NoExit -File install.ps1

.EXAMPLE
powershell -ExecutionPolicy Bypass -NoExit -File install.ps1 -Uninstall

uninstall.cmd in the same folder runs this for a player who opens it with Enter.
#>
[CmdletBinding()]
param(
    # Speak with Windows' voice even while a screen reader is running.
    [switch] $Speak,
    # Print only.
    [switch] $NoSpeech,
    # Answers to the questions, in order, for a scripted run: the run then counts as having a console. A question past
    # the last answer is answered no.
    [string[]] $Answers,
    # Reloaded-II's settings file, at its fixed AppData path (MetaphorInstall.psm1's Get-ReloadedConfigPath).
    [string] $ReloadedConfig = (Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Reloaded-Mod-Loader-II/ReloadedII.json'),
    # Where to put Reloaded-II when it isn't installed.
    [string] $ReloadedFolder = (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Reloaded-II'),
    # Steam's folder. Defaults to the one in the registry.
    [string] $SteamRoot = '',
    # Where the shortcut goes.
    [string] $Desktop = [Environment]::GetFolderPath('Desktop'),
    # The 64-bit .NET install folder, where the runtimes are looked for.
    [string] $DotnetRoot = (Join-Path $(if ($env:ProgramW6432) { $env:ProgramW6432 } else { $env:ProgramFiles }) 'dotnet'),
    # The registry key the Visual C++ Redistributable (x64) records its version in
    # (docs/raw-sources/spoken-installer-sources-2026-11-19.md).
    [string] $VcRuntimeKey = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64',
    # What may be downloaded: URLs, file names and SHA-256s. Defaults to install-pins.json beside this script.
    [string] $Pins = '',
    # Remove the mod instead of installing it.
    [switch] $Uninstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Set here, not as the parameter's default: Windows PowerShell 5.1 leaves $PSScriptRoot empty in an advanced script's
# parameter defaults, so the installer stopped before its first line (#574).
if (-not $Pins) { $Pins = Join-Path $PSScriptRoot 'install-pins.json' }
Import-Module (Join-Path $PSScriptRoot 'MetaphorInstall.psm1')

$scripted = $PSBoundParameters.ContainsKey('Answers')
$answerQueue = [Collections.Generic.Queue[string]]::new()
if ($scripted) { foreach ($answer in $Answers) { $answerQueue.Enqueue($answer) } }

$readers = @(Find-ScreenReader)
$speech = Select-Speech -Speak $Speak.IsPresent -NoSpeech $NoSpeech.IsPresent -Interactive ($scripted -or (Test-Interactive)) -Readers $readers
$voice = Start-Voice $speech

function Out-Line([string] $Printed, [string] $Spoken = $Printed) {
    Write-Host $Printed
    if ($voice) { $voice.Speak($Spoken) }
}

# Speaks QUESTION, then returns the answer: from -Answers, or else the console (MetaphorInstall.psm1). '' for Enter, $null
# for none, which counts as no.
function Read-InstallAnswer([string] $Question) {
    if ($voice) { $voice.Speak(($Question -replace '\s*\[[^\]]*\]$', '')) }
    if (-not $scripted) { return Read-Answer $Question }
    Write-Host "$Question " -NoNewline
    if ($answerQueue.Count -eq 0) { Write-Host '(no answer given, so no)'; return $null }
    $answer = $answerQueue.Dequeue()
    Write-Host $answer
    $answer
}

# The uninstall: says each step's result, goes on past a failed one, then says what it kept. Returns the exit code.
function Invoke-Uninstall($Context) {
    # The installed mod's own copy of this script may be the folder it removes, which Windows won't do while it is the
    # current folder.
    $temp = [IO.Path]::GetTempPath()
    Set-Location -LiteralPath $temp
    [Environment]::CurrentDirectory = $temp
    Out-Line "Uninstall for $($Context.ModName). It says each step, and asks before removing anything other mods may use."
    $go = Read-InstallAnswer (Format-YesNoQuestion 'Uninstall the mod? It turns the mod off for the game, removes it and its settings from Reloaded-II, and deletes its desktop shortcut.')
    if (-not (Test-Yes $go)) {
        Out-Line 'Nothing was changed, because the answer was no.'
        return 1
    }
    $steps = @(Get-UninstallStep $Context)
    $removed = 0
    $failed = 0
    for ($i = 0; $i -lt $steps.Count; $i++) {
        Out-Line "Step $($i + 1) of $($steps.Count): $($steps[$i].Doing)"
        $step = & $steps[$i].Run
        $label = switch ($step.State) { 'done' { 'Done'; $removed++ } 'unchanged' { 'Nothing to remove' } default { 'Failed'; $failed++ } }
        $printed = "${label}: $($step.Line)"
        if ($step.Detail) { $printed += " $($step.Detail)" }
        Out-Line $printed "${label}: $($step.Line)"
        if ($step.State -eq 'failed') { Out-Line "To fix it: $($step.Fix)" }
    }
    Out-Line 'Kept the .NET 9 Desktop Runtime and the Visual C++ Redistributable: other programs may use them, and the uninstall cannot tell whether this installer installed them. To remove them, use Installed apps in Windows Settings.'
    if ($failed -gt 0) {
        Out-Line 'The uninstall did not remove everything. Do what the failed steps said, then run the uninstall again.'
        return 1
    }
    Out-Line $(if ($removed -eq 0) { 'There was nothing left to remove.' } else { 'The uninstall is done.' })
    0
}

$workFolder = $null
$exitCode = 1
try {
    $say = { param($Printed, $Spoken) if ($Spoken) { Out-Line $Printed $Spoken } else { Out-Line $Printed } }
    $ask = { param($Question) Read-InstallAnswer $Question }
    if (-not (Confirm-RunWindow -Say $say -Ask $ask)) { exit 1 }
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'ModConfig.json') -PathType Leaf)) {
        Out-Line 'This installer is not in the mod''s folder. Open install.cmd in the mod''s folder from the release zip.'
        exit 1
    }
    $workFolder = (New-Item -ItemType Directory (Join-Path ([IO.Path]::GetTempPath()) "metaphor-install-$([guid]::NewGuid())")).FullName
    $context = New-InstallContext -ReleaseFolder $PSScriptRoot -PinsPath $Pins -ConfigPath $ReloadedConfig `
        -ReloadedFolder $ReloadedFolder -SteamRoot $SteamRoot -Desktop $Desktop -DotnetRoot $DotnetRoot `
        -VcRuntimeKey $VcRuntimeKey -WorkFolder $workFolder -Ask $ask -Say $say
    if ($Uninstall) { exit (Invoke-Uninstall $context) }

    Out-Line "Installer for $($context.ModName) $($context.ModVersion). It says each step, and asks before each download."
    $go = Read-InstallAnswer (Format-YesNoQuestion 'Install the mod? It puts the mod in Reloaded-II, turns it on for the game, and puts a shortcut on your desktop.')
    if (-not (Test-Yes $go)) {
        Out-Line 'Nothing was changed, because the answer was no.'
        exit 1
    }

    $steps = @(Get-InstallStep $context)
    $total = $steps.Count + 1
    $noShortcut = $false
    for ($i = 0; $i -lt $steps.Count; $i++) {
        Out-Line "Step $($i + 1) of ${total}: $($steps[$i].Doing)"
        $step = & $steps[$i].Run
        $label = switch ($step.State) { 'done' { 'Done' } 'unchanged' { 'Nothing to do' } default { 'Failed' } }
        $printed = "${label}: $($step.Line)"
        if ($step.Detail) { $printed += " $($step.Detail)" }
        Out-Line $printed "${label}: $($step.Line)"
        if ($step.State -eq 'failed') {
            Out-Line "To fix it: $($step.Fix)"
            if ($steps[$i].Optional) {
                Out-Line 'The installer goes on without the shortcut, which the mod does not need.'
                $noShortcut = $true
                continue
            }
            Out-Line "The installer stopped at step $($i + 1), so the steps after it did not run."
            exit 1
        }
    }

    Out-Line "Step $total of ${total}: Checking the install."
    $checks = @(Get-InstallCheck -ReleaseFolder $context.InstalledMod -ConfigPath $ReloadedConfig -FindScreenReader { $readers }.GetNewClosure())
    foreach ($check in $checks) { Out-Line (Format-Check $check) (Format-SpokenCheck $check) }
    # Before the first start with the mod, the check can't find the status file the mod writes as it starts.
    $problems = @($checks | Where-Object { $_.State -eq 'problem' -and $_.Name -ne 'Last start' })
    $shortcut = [IO.Path]::GetFileNameWithoutExtension($ShortcutName)
    if ($problems.Count -gt 0) {
        Out-Line "The install finished, but the check found a problem. First thing to fix: $($problems[0].Fix)"
        exit 1
    }
    $start = if ($noShortcut) { 'The install is done, but the desktop shortcut was not made. The mod still works. Start the game from Reloaded-II.' }
    else { "The install is done. Start the game from the desktop shortcut, $shortcut." }
    Out-Line "$start Run check-install.ps1 in the mod's folder if the mod stays silent."
    $exitCode = 0
}
finally {
    if ($workFolder) { Remove-Item -LiteralPath $workFolder -Recurse -Force -ErrorAction SilentlyContinue }
    if ($voice) { $voice.Dispose() }
}
exit $exitCode
