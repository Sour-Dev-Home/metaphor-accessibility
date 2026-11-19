# Tests for scripts/dev/ghidra.ps1's one-time move of the Ghidra project from ghidra/ to out/ghidra/ (#411), in a
# stand-in clone with no Ghidra, so the script stops right after the move. Run: Invoke-Pester tests/pester (Pester 5+).

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path

    # A git clone holding the script and the modules it imports; Ghidra and the JDK point at an empty folder.
    function New-Clone {
        $script:root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $script:clone = (New-Item -ItemType Directory (Join-Path $root 'clone')).FullName
        $script:empty = (New-Item -ItemType Directory (Join-Path $root 'empty')).FullName
        git -C $clone init -q
        foreach ($file in 'dev/ghidra.ps1', 'dev/ReloadedDev.psm1', 'player/MetaphorInstall.psm1') {
            $target = Join-Path $clone "scripts/$file"
            New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
            Copy-Item (Join-Path $repo "scripts/$file") $target
        }
    }

    function Invoke-Ghidra {
        $saved = $env:GHIDRA_INSTALL_DIR, $env:JAVA_HOME, $env:LOCALAPPDATA
        try {
            $env:GHIDRA_INSTALL_DIR = $env:JAVA_HOME = $env:LOCALAPPDATA = $empty
            $output = & pwsh -NoProfile -NonInteractive -File (Join-Path $clone 'scripts/dev/ghidra.ps1') build 2>&1
        }
        finally { $env:GHIDRA_INSTALL_DIR, $env:JAVA_HOME, $env:LOCALAPPDATA = $saved }
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
    }

    function New-Project([string] $Folder, [string] $Text) {
        New-Item -ItemType Directory -Force (Join-Path $clone "$Folder/0123456789abcdef") | Out-Null
        Set-Content -LiteralPath (Join-Path $clone "$Folder/0123456789abcdef/ready.txt") -Value $Text
    }
}

Describe 'the Ghidra project folder' {
    It 'moves a project from ghidra/ to out/ghidra/ once' {
        New-Clone
        New-Project 'ghidra' 'old'

        $result = Invoke-Ghidra
        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'moved the Ghidra project from ghidra/ to out/ghidra/'
        Join-Path $clone 'ghidra' | Should -Not -Exist
        Get-Content -LiteralPath (Join-Path $clone 'out/ghidra/0123456789abcdef/ready.txt') | Should -Be 'old'

        $again = Invoke-Ghidra
        $again.ExitCode | Should -Be 2
        $again.Output | Should -Not -Match 'moved'
        Get-Content -LiteralPath (Join-Path $clone 'out/ghidra/0123456789abcdef/ready.txt') | Should -Be 'old'
    }

    It 'leaves both folders alone when out/ghidra/ already exists' {
        New-Clone
        New-Project 'ghidra' 'old'
        New-Project 'out/ghidra' 'new'

        $result = Invoke-Ghidra
        $result.ExitCode | Should -Be 2
        $result.Output | Should -Not -Match 'moved'
        Get-Content -LiteralPath (Join-Path $clone 'ghidra/0123456789abcdef/ready.txt') | Should -Be 'old'
        Get-Content -LiteralPath (Join-Path $clone 'out/ghidra/0123456789abcdef/ready.txt') | Should -Be 'new'
    }

    It 'does nothing without an old project' {
        New-Clone

        $result = Invoke-Ghidra
        $result.ExitCode | Should -Be 2
        $result.Output | Should -Not -Match 'moved'
        Join-Path $clone 'ghidra' | Should -Not -Exist
    }
}
