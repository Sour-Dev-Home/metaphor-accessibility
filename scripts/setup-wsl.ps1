# Sets up the metaphor-checks WSL distro that the check-script suites run in (#341; CONTRIBUTING.md). Run it from
# PowerShell in the repository root:
#   .\scripts\setup-wsl.ps1            create the distro from Debian (it asks first), then install what's missing
#   .\scripts\setup-wsl.ps1 -Update    also upgrade the distro's packages and reinstall the pinned docs tools
#   .\scripts\setup-wsl.ps1 -Yes       don't ask before creating the distro
# It never touches your other distros, WSL's settings or git's config, and never installs, converts or upgrades WSL
# itself: on a machine without WSL 2 it stops with the reason, and the suites keep running in Git Bash. The tools,
# listed in scripts/wsl-tools.txt, are installed as root inside the distro, so there's no sudo prompt.
# Remove the distro with: wsl --unregister metaphor-checks
[CmdletBinding()]
param([switch]$Update, [switch]$Yes)

$ErrorActionPreference = 'Stop'
$distro = 'metaphor-checks'
$env:WSL_UTF8 = '1' # wsl.exe prints UTF-16 otherwise

function Stop-Setup([string]$reason) {
    Write-Host "setup-wsl: $reason The check suites keep running in Git Bash."
    exit 1
}

# Unsupported machines: say why and stop, changing nothing.
if (-not (Get-Command wsl.exe -ErrorAction SilentlyContinue)) { Stop-Setup 'WSL is not installed.' }
# The WSL from the Store answers --version; the old built-in one, WSL 1 only, doesn't. Windows PowerShell throws on a
# native command's stderr under 'Stop', hence the catch.
$storeWsl = $false
try { $null = & wsl.exe --version 2>&1; $storeWsl = $LASTEXITCODE -eq 0 } catch { }
if (-not $storeWsl) { Stop-Setup 'this WSL is the old built-in one, which runs WSL 1 only.' }
$vmPlatform = Get-CimInstance Win32_OptionalFeature -Filter "Name='VirtualMachinePlatform'"
if (-not $vmPlatform -or $vmPlatform.InstallState -ne 1) {
    Stop-Setup "Windows' Virtual Machine Platform is off, so WSL 2 can't run."
}
if (-not (Get-CimInstance Win32_ComputerSystem).HypervisorPresent) {
    Stop-Setup 'virtualization is off in the firmware or Windows, so WSL 2 can''t run.'
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
        $answer = Read-Host "Create the WSL distro '$distro' from Debian? It downloads Debian (about 100 MB). [y/N]"
        if ($answer -notmatch '^(y|yes)$') { Write-Host 'setup-wsl: nothing changed.'; exit 1 }
    }
    & wsl.exe --install Debian --name $distro --version 2 --no-launch
    if ($LASTEXITCODE -ne 0) { Stop-Setup "creating $distro failed (wsl.exe exit code $LASTEXITCODE)." }
    $version = Get-DistroVersion
    if ($null -eq $version) { Stop-Setup "WSL reported success but has no $distro distro." }
}
if ($version -ne '2') {
    Stop-Setup "$distro runs under WSL $version, not 2. Remove it (wsl --unregister $distro) and run this again."
}

# Install the tools as root, from the checkout's root.
$root = Split-Path -Parent $PSScriptRoot
$installArgs = @('--distribution', $distro, '--user', 'root', '--cd', $root, '--exec', '/bin/bash',
    'scripts/wsl-run.sh', 'install')
if ($Update) { $installArgs += '--update' }
& wsl.exe @installArgs
if ($LASTEXITCODE -ne 0) { Stop-Setup "installing the tools in $distro failed (exit code $LASTEXITCODE)." }
Write-Host "setup-wsl: $distro is ready; the check suites run in it from Git Bash."
