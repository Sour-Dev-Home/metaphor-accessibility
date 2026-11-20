<#
.SYNOPSIS
Says why the mod didn't load (#283). Run it from the mod's folder, downloaded or installed; nothing needs installing.

.DESCRIPTION
Checks that Reloaded-II is set up, the mod is in its mods folder and turned on for the game, the game's version is one
the mod knows, the speech library is in place, and how the mod last started, and notes whether a screen reader it
supports is running (MetaphorInstall.psm1 lists what each reads). Prints each result, then names the first thing to fix.

With no screen reader running it also speaks each result with Windows' own voice, so it works when the mod's speech
doesn't. With one running it prints only, and the reader reads the window; -Speak adds Windows' voice anyway. Spoken
lines leave out paths and hashes; the printed lines keep them.

It changes no file or setting and reads nothing over the network. In Windows PowerShell 5.1 the search for JAWS and
ZoomText compiles a small helper, which writes temporary files in your temp folder and removes them. Runs in Windows
PowerShell 5.1 and PowerShell 7.

Exit codes: 0 nothing to fix (notes don't count); 1 something to fix.

.EXAMPLE
powershell -ExecutionPolicy Bypass -NoExit -File check-install.ps1

.EXAMPLE
powershell -ExecutionPolicy Bypass -NoExit -File check-install.ps1 -Speak
#>
[CmdletBinding()]
param(
    # Speak with Windows' voice even while a screen reader is running.
    [switch] $Speak,
    # Print only. Without a console to speak to (as in tests, or -NonInteractive), it prints only anyway.
    [switch] $NoSpeech,
    # Reloaded-II's settings file, at its fixed AppData path (MetaphorInstall.psm1's Get-ReloadedConfigPath).
    [string] $ReloadedConfig = (Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Reloaded-Mod-Loader-II/ReloadedII.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'MetaphorInstall.psm1')

$readers = @(Find-ScreenReader)
$speech = Select-Speech -Speak $Speak.IsPresent -NoSpeech $NoSpeech.IsPresent -Interactive (Test-Interactive) -Readers $readers
$voice = Start-Voice $speech

function Out-Line([string] $Printed, [string] $Spoken) {
    Write-Host $Printed
    if ($voice) { $voice.Speak($Spoken) }
}

try {
    $checks = @(Get-InstallCheck -ReleaseFolder $PSScriptRoot -ConfigPath $ReloadedConfig -FindScreenReader { $readers }.GetNewClosure())
    foreach ($check in $checks) { Out-Line (Format-Check $check) (Format-SpokenCheck $check) }
    $summary = Get-InstallSummary $checks
    Out-Line $summary $summary
}
finally { if ($voice) { $voice.Dispose() } }

if (@($checks | Where-Object State -EQ 'problem').Count -gt 0) { exit 1 }
exit 0
