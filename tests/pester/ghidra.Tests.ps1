# Tests for scripts/dev/ghidra.ps1's one-time move of the Ghidra project from ghidra/ to out/ghidra/ (#411), in a
# stand-in clone with no Ghidra, so the script stops right after the move. Run: Invoke-Pester tests/pester (Pester 5+).

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path

    # A git clone holding the script and the modules it imports; Ghidra and the JDK point at an empty folder. Its path
    # has a space and non-ASCII letters, which git's output carries back to the script (#459).
    function New-Clone {
        $script:root = (New-Item -ItemType Directory (Join-Path $TestDrive "$(New-Guid)/Bibliothèque 日本")).FullName
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

    # A stand-in Ghidra (its launcher and application.properties naming Java MIN) and JDK folders under a stand-in
    # %LOCALAPPDATA%\Programs, each with bin\java.exe and a release file saying its version. No game exe is found, so
    # the script stops right after choosing the JDK. JAVA_HOME is JAVAHOME, or unset.
    function Invoke-JdkChoice([string[]] $Jdks, [string] $JavaHome = '', [int] $Min = 21) {
        $programs = New-Item -ItemType Directory -Force (Join-Path $root 'Local/Programs')
        $ghidra = New-Item -ItemType Directory -Force (Join-Path $programs 'ghidra_12.2_PUBLIC')
        New-Item -ItemType File -Force (Join-Path $ghidra 'support/analyzeHeadless.bat') | Out-Null
        New-Item -ItemType Directory -Force (Join-Path $ghidra 'Ghidra') | Out-Null
        Set-Content -LiteralPath (Join-Path $ghidra 'Ghidra/application.properties') -Value @(
            'application.name=Ghidra', "application.java.min=$Min", 'application.java.max=')
        foreach ($jdk in $Jdks) {
            $name, $version = $jdk -split '=', 2
            $folder = New-Item -ItemType Directory -Force (Join-Path $programs $name)
            New-Item -ItemType File -Force (Join-Path $folder 'bin/java.exe') | Out-Null
            Set-Content -LiteralPath (Join-Path $folder 'release') -Value @('IMPLEMENTOR="Stand-in"', "JAVA_VERSION=`"$version`"")
        }
        $saved = $env:GHIDRA_INSTALL_DIR, $env:JAVA_HOME, $env:LOCALAPPDATA, $env:METAPHOR_GAME_EXE
        try {
            $env:GHIDRA_INSTALL_DIR = $env:METAPHOR_GAME_EXE = $null
            $env:JAVA_HOME = if ($JavaHome) { Join-Path $programs $JavaHome } else { $null }
            $env:LOCALAPPDATA = Join-Path $root 'Local'
            $output = & pwsh -NoProfile -NonInteractive -File (Join-Path $clone 'scripts/dev/ghidra.ps1') build `
                -ReloadedConfig (Join-Path $root 'no-reloaded.json') 2>&1
        }
        finally { $env:GHIDRA_INSTALL_DIR, $env:JAVA_HOME, $env:LOCALAPPDATA, $env:METAPHOR_GAME_EXE = $saved }
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

# #456: the JDK by its version, not its folder name's spelling, checked against the Java version Ghidra says it needs.
Describe 'the JDK choice' {
    BeforeEach { New-Clone }

    It 'picks the newest JDK folder by version number' {
        $result = Invoke-JdkChoice 'jdk-8=1.8.0_402', 'jdk-21=21', 'jdk-21.0.9=21.0.9', 'jdk-21.0.10=21.0.10'
        $result.ExitCode | Should -Be 2 -Because $result.Output
        $result.Output | Should -Match ([regex]::Escape('JDK: jdk-21.0.10 (Java 21, the newest jdk-* folder in %LOCALAPPDATA%\Programs; Ghidra needs Java 21 or later)'))
        $result.Output | Should -Match 'no game executable'
    }

    It 'uses JAVA_HOME when its Java is new enough' {
        $result = Invoke-JdkChoice 'jdk-21=21', 'jdk-21.0.10=21.0.10' -JavaHome 'jdk-21'
        $result.Output | Should -Match ([regex]::Escape('JDK: jdk-21 (Java 21, from JAVA_HOME; Ghidra needs Java 21 or later)'))
    }

    It 'skips a JAVA_HOME too old for Ghidra, and says so' {
        $result = Invoke-JdkChoice 'jdk-8=1.8.0_402', 'jdk-21.0.9=21.0.9' -JavaHome 'jdk-8'
        $result.Output | Should -Match ([regex]::Escape("JAVA_HOME's JDK, jdk-8, is Java 8, and Ghidra needs Java 21 or later: skipping it"))
        $result.Output | Should -Match ([regex]::Escape('JDK: jdk-21.0.9 (Java 21,'))
    }

    It 'stops when no JDK is new enough, naming each one found' {
        $result = Invoke-JdkChoice 'jdk-8=1.8.0_402', 'jdk-21.0.10=21.0.10' -Min 25
        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match ([regex]::Escape("ghidra can't start: no JDK with Java 25 or later, which Ghidra needs (found: jdk-21.0.10 (Java 21), jdk-8 (Java 8))"))
    }
}
