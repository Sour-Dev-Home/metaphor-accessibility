# Questions that are safe without a console (#267), shared by onboard.ps1 and setup-wsl.ps1. Under pwsh -NonInteractive,
# Read-Host throws; with input redirected or no user session, it can wait forever. Without a console these functions
# say so and take the answer as no instead. Must load in Windows PowerShell 5.1 too, so ASCII only.

Set-StrictMode -Version Latest

# True when someone can answer: a user session, input from a console, and no -NonInteractive on the command line.
function Test-Interactive {
    if (-not [Environment]::UserInteractive) { return $false }
    if ([Console]::IsInputRedirected) { return $false }
    if (@([Environment]::GetCommandLineArgs() | Where-Object { $_ -match '^-noni' }).Count -gt 0) { return $false }
    return $true
}

# Shows QUESTION and returns the line typed ('' for Enter), or $null without a console.
function Read-Answer([string] $Question) {
    Write-Host "$Question " -NoNewline
    if (-not (Test-Interactive)) {
        Write-Host '(no console to answer on, so no)'
        return $null
    }
    $answer = Read-Host
    if ($null -eq $answer) { return '' }
    return $answer
}

# True when the answer to QUESTION is y or yes; no without a console.
function Confirm-Step([string] $Question) {
    $answer = Read-Answer "$Question [y/N]"
    return [bool]($null -ne $answer -and $answer -match '^\s*(y|yes)\s*$')
}

# setup-wsl.ps1's exit code when this machine can't run WSL 2 yet; onboard.ps1 then gives the advice to install WSL
# (#360). Every other failure exits 1.
$WslUnavailableExitCode = 2

Export-ModuleMember -Function Test-Interactive, Read-Answer, Confirm-Step -Variable WslUnavailableExitCode
