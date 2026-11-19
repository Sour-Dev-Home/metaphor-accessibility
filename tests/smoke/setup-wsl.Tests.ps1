# Tests for scripts/setup-wsl.ps1 (#341, #360, #402). Each test runs the script in a child pwsh -NonInteractive, with
# stand-in functions for wsl.exe and Get-CimInstance defined first (a function wins over an application of the same
# name), so nothing touches the machine's WSL. The wsl.exe stand-in logs its arguments to calls.log and answers
# --install from STUB_INSTALL, one comma-separated result per call: ok, net (the DNS failure of #360), reboot (the
# restart WSL waits on, #402) or fail; --status answers from STUB_STATUS: ok, or novirt (virtualization off, #402).
# Get-CimInstance always reports a hypervisor, as inside a Hyper-V guest.
# Run: Invoke-Pester tests/smoke (Pester 5 or later).

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path
    $pwsh = Join-Path $PSHOME 'pwsh.exe'
    $setupScript = Join-Path $repo 'scripts/setup-wsl.ps1'

    $standIns = Join-Path $TestDrive 'stand-ins.ps1'
    Set-Content -LiteralPath $standIns -Value @'
function global:Get-CimInstance {
    param([string] $ClassName, [string] $Filter)
    if ($ClassName -eq 'Win32_OptionalFeature') { [pscustomobject] @{ InstallState = 1 } }
    else { [pscustomobject] @{ HypervisorPresent = $true } }
}
function global:wsl.exe {
    Add-Content $env:STUB_CALLS "wsl $args"
    $created = Join-Path $env:STUB_STATE 'created'
    $global:LASTEXITCODE = 0
    switch ($args[0]) {
        '--version' { 'WSL version: stand-in' }
        '--status' {
            if ($env:STUB_STATUS -eq 'novirt') {
                Write-Error 'WSL2 is unable to start since virtualization is not enabled on this machine.'
                $global:LASTEXITCODE = 1
            }
            else { 'Default Version: 2' }
        }
        '--list' {
            '  NAME            STATE           VERSION'
            if (Test-Path $created) { '  metaphor-checks Stopped         2' }
        }
        '--install' {
            $n = @(Get-Content $env:STUB_CALLS | Where-Object { $_ -like 'wsl --install*' }).Count
            switch (@($env:STUB_INSTALL -split ',')[$n - 1]) {
                'ok' { 'Installing: Debian GNU/Linux'; New-Item -ItemType File $created | Out-Null }
                'net' {
                    Write-Error ("Failed to fetch the distribution list from 'https://raw.githubusercontent.com/" +
                        "microsoft/WSL/master/distributions/DistributionInfo.json'. The server name or address " +
                        'could not be resolved')
                    Write-Error 'Error code: Wsl/InstallDistro/WININET_E_NAME_NOT_RESOLVED'
                    $global:LASTEXITCODE = 1
                }
                'reboot' {
                    'The requested operation is successful. Changes will not be effective until the system is rebooted.'
                }
                default { 'The operation failed: stand-in'; $global:LASTEXITCODE = 1 }
            }
        }
        '--distribution' { 'wsl-run install: stand-in'; $global:LASTEXITCODE = [int] $env:STUB_TOOLS_CODE }
    }
}
'@

    # Runs setup-wsl.ps1 with ARGUMENTS; returns its output and exit code. A PATH set in STUB_PATH replaces PATH.
    function Invoke-Setup([string[]] $Arguments = @('-Yes', '-RetryDelaySeconds', '0')) {
        $quoted = $Arguments -join ' '
        $command = "`$env:STUB_CALLS = '$calls'; `$env:STUB_STATE = '$state'; " +
            "`$env:STUB_INSTALL = '$($script:install)'; `$env:STUB_TOOLS_CODE = '$($script:toolsCode)'; " +
            "`$env:STUB_STATUS = '$($script:status)'; " +
            $(if ($script:path) { "`$env:PATH = '$($script:path)'; " } else { '' }) +
            $(if ($script:standIn) { ". '$standIns'; " } else { '' }) +
            "& '$setupScript' $quoted; if (`$? -or `$LASTEXITCODE) { exit `$LASTEXITCODE } else { exit 99 }"
        $output = & $pwsh -NoProfile -NonInteractive -Command $command *>&1 | Out-String
        [pscustomobject] @{ Output = $output; ExitCode = $LASTEXITCODE }
    }

    function Get-Calls { @(if (Test-Path $calls) { Get-Content $calls }) }
}

Describe 'setup-wsl.ps1' {
    BeforeEach {
        $script:state = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $script:calls = Join-Path $state 'calls.log'
        $script:install = 'ok'
        $script:toolsCode = '0'
        $script:status = 'ok'
        $script:path = $null
        $script:standIn = $true
    }

    It 'says what each long step does, and how long, before it starts' {
        $result = Invoke-Setup

        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Match '(?s)setup-wsl: downloading Debian \(about 100 MB\).*few minutes.*Installing: Debian'
        $result.Output | Should -Match '(?s)setup-wsl: created metaphor-checks\.'
        $result.Output | Should -Match '(?s)setup-wsl: installing the check tools.*few minutes.*wsl-run install: stand-in'
        $result.Output | Should -Match 'setup-wsl: metaphor-checks is ready'
    }

    It 'retries once after a network error, then carries on' {
        $script:install = 'net,ok'
        $result = Invoke-Setup

        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Match 'WININET_E_NAME_NOT_RESOLVED'
        $result.Output | Should -Match 'setup-wsl: the download failed on a network error; trying once more'
        $result.Output | Should -Match 'setup-wsl: created metaphor-checks\.'
        @(Get-Calls | Where-Object { $_ -like 'wsl --install*' }).Count | Should -Be 2
    }

    It 'gives network advice, not install advice, after a second network error' {
        $script:install = 'net,net'
        $result = Invoke-Setup

        $result.ExitCode | Should -Be 1 -Because $result.Output
        $result.Output | Should -Match 'The server name or address could not be resolved'
        $result.Output | Should -Match 'check the network and run \.\\scripts\\setup-wsl\.ps1 again'
        $result.Output | Should -Not -Match 'wsl --install|restart|not installed'
        $result.Output | Should -Not -Match 'setup-wsl: created '
        @(Get-Calls | Where-Object { $_ -like 'wsl --install*' }).Count | Should -Be 2
    }

    It 'doesn''t retry a failure that isn''t a network error' {
        $script:install = 'fail,ok'
        $result = Invoke-Setup

        $result.ExitCode | Should -Be 1 -Because $result.Output
        $result.Output | Should -Match 'The operation failed: stand-in'
        $result.Output | Should -Match 'setup-wsl: creating metaphor-checks failed \(wsl.exe exit code 1\)'
        @(Get-Calls | Where-Object { $_ -like 'wsl --install*' }).Count | Should -Be 1
    }

    It 'says the tools failed and how to retry, without install advice' {
        $script:toolsCode = '1'
        $result = Invoke-Setup

        $result.ExitCode | Should -Be 1 -Because $result.Output
        $result.Output | Should -Match 'setup-wsl: installing the tools in metaphor-checks failed \(exit code 1\)'
        $result.Output | Should -Not -Match 'wsl --install|restart'
    }

    It 'without a console and -Yes, answers no and creates nothing' {
        $result = Invoke-Setup @('-RetryDelaySeconds', '0')

        $result.ExitCode | Should -Be 1 -Because $result.Output
        $result.Output | Should -Match '\(no console to answer on, so no\)'
        $result.Output | Should -Match 'setup-wsl: nothing changed\. Run it in a PowerShell window, or with -Yes\.'
        Get-Calls | Where-Object { $_ -like 'wsl --install*' } | Should -BeNullOrEmpty
    }

    It 'refuses a negative retry delay before doing anything' {
        $result = Invoke-Setup @('-Yes', '-RetryDelaySeconds', '-1')

        $result.ExitCode | Should -Be 99 -Because $result.Output
        $result.Output | Should -Match 'RetryDelaySeconds'
        Get-Calls | Should -BeNullOrEmpty
    }

    It 'says to restart Windows when WSL is waiting on a restart' {
        $script:install = 'reboot'
        $result = Invoke-Setup

        $result.ExitCode | Should -Be 2 -Because $result.Output
        $result.Output | Should -Match 'Changes will not be effective until the system is rebooted'
        $result.Output | Should -Match ('setup-wsl: WSL is waiting for Windows to restart before it can create ' +
            'metaphor-checks\. Restart Windows, then run \.\\scripts\\setup-wsl\.ps1\.')
        $result.Output | Should -Not -Match 'reported success|virtualization'
    }

    It 'names the virtualization fix, inside a VM too, when WSL 2 can''t start' {
        $script:status = 'novirt'
        $result = Invoke-Setup

        $result.ExitCode | Should -Be 2 -Because $result.Output
        $result.Output | Should -Match ('setup-wsl: WSL 2 can''t start because virtualization is off\. Turn on ' +
            'virtualization in the firmware settings')
        $result.Output | Should -Match 'Set-VMProcessor -VMName <vm> -ExposeVirtualizationExtensions \$true on the host'
        Get-Calls | Where-Object { $_ -like 'wsl --install*' } | Should -BeNullOrEmpty
    }

    It 'exits 2, for the install advice, when WSL is missing' {
        $script:standIn = $false
        $script:path = (New-Item -ItemType Directory (Join-Path $state 'empty-path')).FullName
        $result = Invoke-Setup

        $result.ExitCode | Should -Be 2 -Because $result.Output
        $result.Output | Should -Match 'setup-wsl: WSL is not installed\.'
    }
}
