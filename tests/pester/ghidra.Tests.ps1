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

    # #508: every process in a run shares one console, so setting [Console]::OutputEncoding to read git's UTF-8 also
    # switched the code page under the Pester processes running beside it, and a program they started then wrote output
    # they misread (onboard.Tests' summary marks). A stand-in git records the code page it starts under.
    It 'reads the git dir from git without changing the console''s code page' {
        New-Clone
        New-Project 'ghidra' 'old'
        $bin = New-Item -ItemType Directory (Join-Path $root 'bin')
        $git = (Get-Command git -CommandType Application | Select-Object -First 1).Source
        # The output code page, which chcp doesn't show (it shows the input one).
        $codePage = "`"$(Join-Path $PSHOME 'pwsh.exe')`" -NoProfile -Command [Console]::OutputEncoding.CodePage"
        Set-Content -LiteralPath (Join-Path $bin 'git.cmd') -Encoding ascii -Value @(
            '@echo off', ">`"%~dp0codepage.txt`" 2>&1 $codePage", "`"$git`" %*")
        $before = cmd /c $codePage
        $saved = $env:PATH
        try {
            $env:PATH = "$bin;$env:PATH"
            $result = Invoke-Ghidra
        }
        finally { $env:PATH = $saved }

        $result.ExitCode | Should -Be 2 -Because $result.Output
        $result.Output | Should -Match 'moved the Ghidra project from ghidra/ to out/ghidra/'
        $after = Get-Content -LiteralPath (Join-Path $bin 'codepage.txt')
        $after | Should -Not -BeNullOrEmpty
        $after | Should -Be $before
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

# #796: `batch <file>` answers many queries in one headless run. A stand-in analyzeHeadless records each run's arguments
# and, for MetaphorQuery.java, writes each answer file as the real script does ("# error: ..." for a target named bad).
Describe 'batch mode' {
    BeforeAll {
        $script:fakeScript = @'
$all = $args
Add-Content -LiteralPath $env:FAKE_LOG -Value ($all -join ' ')
$i = [array]::IndexOf($all, 'MetaphorQuery.java')
if ($i -lt 0) { exit 0 }
function Answer($file, $command, $target) {
    if ($target -eq 'bad') { Set-Content -LiteralPath $file -Value "# error: no symbol named bad" }
    else { Set-Content -LiteralPath $file -Value "# $command $target answered" }
}
if ($all[$i + 2] -eq 'batch') {
    Copy-Item -LiteralPath $all[$i + 1] -Destination "$($all[$i + 1]).seen"
    foreach ($line in Get-Content -LiteralPath $all[$i + 1]) {
        $f = $line -split "`t"
        Answer $f[0] $f[1] $f[2]
    }
}
else { Answer $all[$i + 1] $all[$i + 2] $all[$i + 3] }
exit 0
'@

        function Invoke-Query([string[]] $Arguments) {
            $saved = $env:GHIDRA_INSTALL_DIR, $env:JAVA_HOME, $env:LOCALAPPDATA, $env:FAKE_LOG
            try {
                $env:GHIDRA_INSTALL_DIR = $env:JAVA_HOME = $null
                $env:LOCALAPPDATA = Join-Path $root 'Local'
                $env:FAKE_LOG = $fakeLog
                $output = & pwsh -NoProfile -NonInteractive -File (Join-Path $clone 'scripts/dev/ghidra.ps1') @Arguments `
                    -GameExe $exe -KnownBuilds $known 2>&1
            }
            finally { $env:GHIDRA_INSTALL_DIR, $env:JAVA_HOME, $env:LOCALAPPDATA, $env:FAKE_LOG = $saved }
            [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
        }

        function New-BatchFile([string[]] $Lines) {
            $path = Join-Path $root 'queries.txt'
            Set-Content -LiteralPath $path -Value $Lines
            $path
        }
    }

    BeforeEach {
        New-Clone
        $programs = New-Item -ItemType Directory -Force (Join-Path $root 'Local/Programs')
        $ghidra = New-Item -ItemType Directory -Force (Join-Path $programs 'ghidra_12.2_PUBLIC')
        New-Item -ItemType Directory -Force (Join-Path $ghidra 'support'), (Join-Path $ghidra 'Ghidra') | Out-Null
        Set-Content -LiteralPath (Join-Path $ghidra 'support/analyzeHeadless.bat') -Encoding ascii -Value @(
            '@echo off', "pwsh -NoProfile -NonInteractive -File `"%~dp0fake.ps1`" %*")
        Set-Content -LiteralPath (Join-Path $ghidra 'support/fake.ps1') -Value $fakeScript
        Set-Content -LiteralPath (Join-Path $ghidra 'Ghidra/application.properties') -Value 'application.java.min=21'
        $jdk = New-Item -ItemType Directory -Force (Join-Path $programs 'jdk-21')
        New-Item -ItemType File -Force (Join-Path $jdk 'bin/java.exe') | Out-Null
        Set-Content -LiteralPath (Join-Path $jdk 'release') -Value 'JAVA_VERSION="21"'
        $script:exe = Join-Path $root 'game.exe'
        Set-Content -LiteralPath $exe -Value 'stand-in exe'
        $script:known = Join-Path $root 'known.json'
        Set-Content -LiteralPath $known -Value '[]'
        $sha = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLower()
        $projectFolder = New-Item -ItemType Directory -Force (Join-Path $clone "out/ghidra/$($sha.Substring(0, 16))")
        Set-Content -LiteralPath (Join-Path $projectFolder 'ready.txt') -Value 'ready'
        $script:fakeLog = Join-Path $root 'fake.log'
    }

    It 'keeps the single query as it was: one run, the answer file, the command and the target' {
        $result = Invoke-Query 'disasm', '0x1000'
        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Match '# disasm 0x1000 answered'
        $result.Output | Should -Not -Match '==='
        $run = @(Get-Content -LiteralPath $fakeLog)
        $run.Count | Should -Be 1
        $run[0] | Should -Match 'MetaphorQuery\.java .*disasm-0x1000\.txt disasm 0x1000 -scriptPath'
    }

    It 'answers every query in one headless run, skipping blank lines and # comments' {
        $file = New-BatchFile '# the cursor', '', 'disasm 0x1000', '   ', 'offset 0x48 Cursor  ', '  # indented comment', 'callers 0x2000'
        $result = Invoke-Query 'batch', $file
        $result.ExitCode | Should -Be 0 -Because $result.Output
        @(Get-Content -LiteralPath $fakeLog).Count | Should -Be 1
        $work = @(Get-Content -LiteralPath (Join-Path $clone 'out/ghidra/out/batch-work.txt.seen'))
        $work.Count | Should -Be 3
        ($work[1] -split "`t")[1..3] | Should -Be 'offset', '0x48', 'Cursor'
        ($work[0] -split "`t")[0] | Should -Match 'disasm-0x1000\.txt$'
    }

    It 'prints each answer under a separator naming its query, and keeps each answer file' {
        $file = New-BatchFile 'disasm 0x1000', 'offset 0x48 Cursor'
        $result = Invoke-Query 'batch', $file
        $result.Output | Should -Match '(?s)=== disasm 0x1000 ===\s+# disasm 0x1000 answered\s+=== offset 0x48 Cursor ===\s+# offset 0x48 answered'
        Join-Path $clone 'out/ghidra/out/disasm-0x1000.txt' | Should -Exist
        Join-Path $clone 'out/ghidra/out/offset-0x48-Cursor.txt' | Should -Exist
    }

    It 'reports a failing query, carries on with the rest and exits 1' {
        $file = New-BatchFile 'disasm bad', 'disasm 0x1000'
        $result = Invoke-Query 'batch', $file
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match '# error: no symbol named bad'
        $result.Output | Should -Match '# disasm 0x1000 answered'
    }

    It 'reports a line it cannot read without running it, and still answers the others' {
        $file = New-BatchFile 'frobnicate 0x1', 'disasm', 'build 0x1', 'offset 1 2 3', 'disasm 0x1000'
        $result = Invoke-Query 'batch', $file
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match '# error: unknown command frobnicate'
        $result.Output | Should -Match '# error: disasm needs a target'
        $result.Output | Should -Match '# error: unknown command build'
        $result.Output | Should -Match '# error: too many words'
        $result.Output | Should -Match '# disasm 0x1000 answered'
        @(Get-Content -LiteralPath (Join-Path $clone 'out/ghidra/out/batch-work.txt.seen')).Count | Should -Be 1
    }

    It 'stops with exit 2 when the file is missing or holds no queries, without starting Ghidra' {
        $missing = Invoke-Query 'batch', (Join-Path $root 'nope.txt')
        $missing.ExitCode | Should -Be 2
        $missing.Output | Should -Match "ghidra can't start: no batch file"
        $empty = Invoke-Query 'batch', (New-BatchFile '# only a comment', '')
        $empty.ExitCode | Should -Be 2
        $empty.Output | Should -Match "ghidra can't start: .*no queries"
        $none = Invoke-Query 'batch'
        $none.ExitCode | Should -Be 2
        $fakeLog | Should -Not -Exist
    }

    It 'exits 1 when every line was bad and Ghidra was never needed' {
        $result = Invoke-Query 'batch', (New-BatchFile 'frobnicate 0x1')
        $result.ExitCode | Should -Be 1
        $fakeLog | Should -Not -Exist
    }
}
