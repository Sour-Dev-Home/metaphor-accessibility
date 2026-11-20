# Tests for scripts/setup/install-pester.ps1 (#477). Save-PSResource is mocked, so nothing is downloaded or installed:
# it "saves" a stand-in package, either a copy of the Pester running these tests (signed by its publisher) or an
# unsigned one, and the script installs into a module folder under TestDrive.
# Run: Invoke-Pester tests/pester (Pester 5 or later).

BeforeAll {
    $script:installer = (Resolve-Path "$PSScriptRoot/../../scripts/setup/install-pester.ps1").Path
    $script:realPester = (Get-Module Pester).ModuleBase
    $script:realSigner = (Get-AuthenticodeSignature -LiteralPath (Join-Path $realPester 'Pester.psd1')).SignerCertificate
    $script:standInVersion = '9.9.9'

    # Runs the script into a fresh module folder; returns its exit code, its output and that folder.
    function Invoke-Installer([string] $Signer) {
        $modules = Join-Path $TestDrive "modules-$([guid]::NewGuid())"
        $output = & $installer -Version $standInVersion -Signer $Signer -ModulePath $modules 6>&1 | Out-String
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $output; Modules = $modules }
    }
}

Describe 'install-pester.ps1' {
    Context 'a package signed by the running Pester''s publisher' {
        BeforeEach {
            if (-not $realSigner) { Set-ItResult -Skipped -Because 'the Pester running these tests is unsigned' }
            # A test sets $addToPackage to put more files in the package.
            $script:addToPackage = {}
            Mock Save-PSResource {
                $folder = New-Item -ItemType Directory -Force (Join-Path $Path "Pester\$($Version.Trim('[]'))")
                # .NET, not Copy-Item, which a test mocks.
                foreach ($file in [IO.Directory]::GetFiles($realPester, '*', 'AllDirectories')) {
                    $copy = Join-Path $folder.FullName $file.Substring($realPester.Length + 1)
                    [IO.Directory]::CreateDirectory((Split-Path $copy)) | Out-Null
                    [IO.File]::Copy($file, $copy)
                }
                & $addToPackage $folder.FullName
            }
        }

        It 'refuses a hidden unsigned script among the signed files' {
            $script:addToPackage = {
                param($Folder)
                $file = New-Item -ItemType File (Join-Path $Folder 'bin\evil.ps1') -Value 'Write-Host owned'
                $file.Attributes = 'Hidden'
            }
            $result = Invoke-Installer $realSigner.Thumbprint
            $result.ExitCode | Should -Be 1 -Because $result.Output
            $result.Output | Should -Match 'bin\\evil\.ps1: NotSigned, signer none'
            Join-Path $result.Modules 'Pester' | Should -Not -Exist
        }

        It 'refuses an unsigned file of a type PowerShell loads that isn''t on the inert list' {
            $script:addToPackage = { param($Folder) Set-Content (Join-Path $Folder 'Evil.cdxml') '<PowerShellMetadata />' }
            $result = Invoke-Installer $realSigner.Thumbprint
            $result.ExitCode | Should -Be 1 -Because $result.Output
            $result.Output | Should -Match 'Evil\.cdxml: '
            Join-Path $result.Modules 'Pester' | Should -Not -Exist
        }

        It 'leaves no Pester version folder when the copy fails midway' {
            # The script's first copy into the module folder lands, then fails.
            Mock Copy-Item { [IO.File]::Copy($LiteralPath, $Destination); throw 'the disk is full' }
            $result = Invoke-Installer $realSigner.Thumbprint
            $result.ExitCode | Should -Be 1 -Because $result.Output
            $result.Output | Should -Match 'failed: the disk is full'
            Join-Path $result.Modules "Pester\$standInVersion" | Should -Not -Exist
            @(Get-ChildItem -Force (Join-Path $result.Modules 'Pester') -ErrorAction SilentlyContinue) | Should -HaveCount 0
        }

        It 'installs it when the pinned signer matches, saving with -AuthenticodeCheck from PSGallery' {
            $result = Invoke-Installer $realSigner.Thumbprint
            $result.ExitCode | Should -Be 0 -Because $result.Output
            Join-Path $result.Modules "Pester\$standInVersion\Pester.psd1" | Should -Exist
            Should -Invoke Save-PSResource -Times 1 -Exactly -ParameterFilter {
                $AuthenticodeCheck -and $Repository -eq 'PSGallery' -and $Version -eq "[$standInVersion]"
            }
        }

        It 'refuses it when the pinned signer is another certificate, and installs nothing' {
            $result = Invoke-Installer ('0' * 40)
            $result.ExitCode | Should -Be 1
            $result.Output | Should -Match "refused Pester $([regex]::Escape($standInVersion))"
            $result.Output | Should -Match ([regex]::Escape("Pester.psd1: Valid, signer $($realSigner.Thumbprint)"))
            Join-Path $result.Modules 'Pester' | Should -Not -Exist
        }
    }

    Context 'an unsigned package' {
        BeforeEach {
            Mock Save-PSResource {
                $folder = New-Item -ItemType Directory -Force (Join-Path $Path "Pester\$($Version.Trim('[]'))")
                Set-Content (Join-Path $folder 'Pester.psd1') "@{ ModuleVersion = '$($Version.Trim('[]'))' }"
                Set-Content (Join-Path $folder 'Pester.psm1') 'function Invoke-Pester {}'
            }
        }

        It 'refuses it and installs nothing' {
            $result = Invoke-Installer ('0' * 40)
            $result.ExitCode | Should -Be 1
            $result.Output | Should -Match 'Pester\.psd1: NotSigned, signer none'
            $result.Output | Should -Match 'Pester\.psm1: NotSigned, signer none'
            Join-Path $result.Modules 'Pester' | Should -Not -Exist
        }
    }

    It 'stops when the save fails, and installs nothing' {
        Mock Save-PSResource { throw 'The signature status for ''Pester'' file ''Pester.psm1'' is ''HashMismatch''.' }
        $result = Invoke-Installer ('0' * 40)
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'saving Pester .* failed: .*HashMismatch'
        Join-Path $result.Modules 'Pester' | Should -Not -Exist
    }

    It 'leaves an existing install of the version alone' {
        Mock Save-PSResource {}
        $modules = Join-Path $TestDrive 'existing'
        New-Item -ItemType File -Force (Join-Path $modules "Pester\$standInVersion\Pester.psd1") | Out-Null
        $output = & $installer -Version $standInVersion -Signer ('0' * 40) -ModulePath $modules 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 1
        $output | Should -Match 'already exists'
        Should -Invoke Save-PSResource -Times 0 -Exactly
    }
}
