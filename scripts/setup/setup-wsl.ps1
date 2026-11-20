# Sets up the metaphor-checks WSL distro that the check-script suites run in (#341;
# docs/contributors/how-to/set-up-a-clone.md). Run it from PowerShell in the repository root:
#   .\scripts\setup\setup-wsl.ps1            create the distro from Debian (it asks first), then install what's missing
#   .\scripts\setup\setup-wsl.ps1 -Update    also upgrade the distro's packages and reinstall the pinned docs tools
#   .\scripts\setup\setup-wsl.ps1 -Yes       don't ask before creating the distro (without a console and -Yes, it doesn't
#                                      create it)
#   -RetryDelaySeconds N               wait N seconds before retrying a download that failed on the network (default 15)
# It never touches your other distros, WSL's settings or git's config, and never installs, converts or upgrades WSL
# itself: on a machine without WSL 2 it stops with the reason and $WslUnavailableExitCode (MetaphorInstall.psm1), and the
# suites keep running in Git Bash. Any other failure exits 1, and onboard.ps1 gives the install advice only for the
# first (#360), and only when the last line names no fix of its own, such as a restart or virtualization (#402). The
# last line reads "setup-wsl: <reason>. [<fix>] The check suites keep running in Git Bash." The tools,
# listed in scripts/setup/wsl-tools.txt, are installed as root inside the distro, so there's no sudo prompt.
# Remove the distro with: wsl --unregister metaphor-checks
[CmdletBinding()]
param([switch]$Update, [switch]$Yes, [ValidateRange(0, 600)][int]$RetryDelaySeconds = 15)

$ErrorActionPreference = 'Stop'
$distro = 'metaphor-checks'
$env:WSL_UTF8 = '1' # wsl.exe prints UTF-16 otherwise
Import-Module (Join-Path $PSScriptRoot '../player/MetaphorInstall.psm1') -Force

function Stop-Setup([string]$reason, [int]$code = 1) {
    Write-Host "setup-wsl: $reason The check suites keep running in Git Bash."
    exit $code
}

# Unsupported machines: say why and stop, changing nothing.
if (-not (Get-Command wsl.exe -ErrorAction SilentlyContinue)) {
    Stop-Setup 'WSL is not installed.' $WslUnavailableExitCode
}
# The WSL from the Store answers --version; the old built-in one, WSL 1 only, doesn't. Windows PowerShell throws on a
# native command's stderr under 'Stop', hence the catch.
$storeWsl = $false
try { $null = & wsl.exe --version 2>&1; $storeWsl = $LASTEXITCODE -eq 0 } catch { }
if (-not $storeWsl) { Stop-Setup 'this WSL is the old built-in one, which runs WSL 1 only.' $WslUnavailableExitCode }
$vmPlatform = Get-CimInstance Win32_OptionalFeature -Filter "Name='VirtualMachinePlatform'"
if (-not $vmPlatform -or $vmPlatform.InstallState -ne 1) {
    Stop-Setup "Windows' Virtual Machine Platform is off, so WSL 2 can't run." $WslUnavailableExitCode
}
# A restart WSL is waiting on, and WSL 2 failing to start with virtualization off (#402,
# docs/raw-sources/wsl-restart-and-virtualization-messages-2026-11-19.md), are told apart by what WSL checks, not by its
# messages, which are in the user's language (#457; docs/raw-sources/wsl-restart-and-virtualization-source-2026-11-19.md).
$restartFix = 'Restart Windows, then run .\scripts\setup\setup-wsl.ps1.'
$firmwareSteps = ('turn on virtualization in the firmware settings; on a Hyper-V VM, shut it down and run ' +
    'Set-VMProcessor -VMName <vm> -ExposeVirtualizationExtensions $true on the host. Then run .\scripts\setup\setup-wsl.ps1.')

if (-not (Get-CimInstance Win32_ComputerSystem).HypervisorPresent) {
    Stop-Setup "virtualization is off in the firmware or Windows, so WSL 2 can't run. To fix it, $firmwareSteps" `
        $WslUnavailableExitCode
}
# HypervisorPresent is true in any Hyper-V guest, nested virtualization or not (#402). `wsl --status` says WSL 2 can't
# start when the vmcompute service is missing, and exits 0, so the service is checked here instead. It can also be
# missing while the Virtual Machine Platform, just turned on, waits on a restart, so a restart comes first.
if (-not (Get-Service -Name vmcompute -ErrorAction SilentlyContinue)) {
    Stop-Setup ("WSL 2 can't start: Windows may still need a restart, or virtualization is off. Restart Windows, then " +
        "run .\scripts\setup\setup-wsl.ps1; if WSL 2 still can't start, $firmwareSteps") `
        $WslUnavailableExitCode
}

# Runs wsl.exe --install for the distro, printing WSL's own output as it comes; returns that output as lines. Under
# 'Continue' a native command's stderr is output here, not a terminating error (Windows PowerShell).
function Invoke-DistroInstall {
    $ErrorActionPreference = 'Continue'
    & wsl.exe --install Debian --name $distro --version 2 --no-launch 2>&1 | ForEach-Object {
        $line = "$_"
        Write-Host $line
        $line
    }
}

# A download that failed on the network, not on WSL. The one seen so far, on a fresh VM (#360,
# docs/raw-sources/wsl-install-network-failure-2026-11-19.md): "Failed to fetch the distribution list from
# '...DistributionInfo.json'. The server name or address could not be resolved", error code
# Wsl/InstallDistro/WININET_E_NAME_NOT_RESOLVED. Matching any WININET_E_ code assumes WSL names WinINet's other
# download errors the same way. [NEEDS VERIFICATION]
function Test-NetworkFailure([string[]]$lines) {
    return [bool](@($lines | Where-Object { $_ -match 'WININET_E_|Failed to fetch' }).Count -gt 0)
}

# The distro's row in `wsl --list --verbose`: "[*] NAME STATE VERSION".
function Get-DistroVersion {
    foreach ($line in (& wsl.exe --list --verbose)) {
        $fields = -split ($line -replace '^\*', '')
        if ($fields.Count -ge 3 -and $fields[0] -eq $distro) { return $fields[-1] }
    }
    return $null
}

$version = Get-DistroVersion
if ($null -eq $version) {
    if (-not $Yes) {
        # Without a console Confirm-Step answers no (MetaphorInstall.psm1), so a scripted run never hangs here.
        $console = Test-Interactive
        if (-not (Confirm-Step "Create the WSL distro '$distro' from Debian? It downloads Debian (about 100 MB).")) {
            if ($console) { Write-Host 'setup-wsl: nothing changed.' }
            else { Write-Host 'setup-wsl: nothing changed. Run it in a PowerShell window, or with -Yes.' }
            exit 1
        }
    }
    Write-Host "setup-wsl: downloading Debian (about 100 MB) and making $distro from it; this takes a few minutes."
    $lines = @(Invoke-DistroInstall)
    if ($LASTEXITCODE -ne 0 -and (Test-NetworkFailure $lines)) {
        # A passing network fault (#360): one more try, after a pause.
        Write-Host "setup-wsl: the download failed on a network error; trying once more in $RetryDelaySeconds seconds."
        Start-Sleep -Seconds $RetryDelaySeconds
        $lines = @(Invoke-DistroInstall)
        if ($LASTEXITCODE -ne 0 -and (Test-NetworkFailure $lines)) {
            Stop-Setup ('downloading Debian failed twice on a network error. Check the network and run ' +
                '.\scripts\setup\setup-wsl.ps1 again.')
        }
    }
    if ($LASTEXITCODE -ne 0) { Stop-Setup "creating $distro failed (wsl.exe exit code $LASTEXITCODE)." }
    # WSL installs no distro while a restart is pending, and still exits 0: the only sign, so it is probable, not certain.
    $version = Get-DistroVersion
    if ($null -eq $version) {
        Stop-Setup "WSL is probably waiting for Windows to restart before it can create $distro. $restartFix" `
            $WslUnavailableExitCode
    }
    Write-Host "setup-wsl: created $distro."
}
if ($version -ne '2') {
    Stop-Setup "$distro runs under WSL $version, not 2. Remove it (wsl --unregister $distro) and run this again."
}

# Install the tools as root, from the checkout's root.
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$installArgs = @('--distribution', $distro, '--user', 'root', '--cd', $root, '--exec', '/bin/bash',
    'scripts/check/wsl-run.sh', 'install')
if ($Update) { $installArgs += '--update' }
Write-Host ("setup-wsl: installing the check tools in $distro (scripts/setup/wsl-tools.txt) that are missing; the first " +
    'run takes a few minutes, later ones seconds.')
& wsl.exe @installArgs
if ($LASTEXITCODE -ne 0) {
    Stop-Setup ("installing the tools in $distro failed (exit code $LASTEXITCODE). See the output above, then run " +
        '.\scripts\setup\setup-wsl.ps1 again.')
}
Write-Host "setup-wsl: $distro is ready; the check suites run in it from Git Bash."
