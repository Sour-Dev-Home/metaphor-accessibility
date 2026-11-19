# Tests for the game locator and the console questions in scripts/player/MetaphorInstall.psm1, against stand-in Steam
# libraries and Reloaded-II configs in the test folder. Run: Invoke-Pester tests/pester (Pester 5 or later).

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path
    Import-Module (Join-Path $repo 'scripts/player/MetaphorInstall.psm1') -Force

    # A Steam library at PATH holding the game in steamapps/common/FOLDER, when FOLDER is given.
    function New-Library([string] $Path, [string] $Folder) {
        New-Item -ItemType Directory -Force (Join-Path $Path 'steamapps/common') | Out-Null
        if ($Folder) {
            $game = New-Item -ItemType Directory -Force (Join-Path $Path "steamapps/common/$Folder")
            Set-Content -LiteralPath (Join-Path $game 'METAPHOR.exe') -Value 'stand-in'
        }
    }

    # Steam's libraryfolders.vdf listing LIBRARIES, with backslashes doubled as Steam writes them
    # (docs/raw-sources/steam-library-folders-2026-11-18.md).
    function Write-LibraryFolders([string] $SteamRoot, [string[]] $Libraries) {
        $entries = for ($i = 0; $i -lt $Libraries.Count; $i++) {
            "`t`"$i`"`n`t{`n`t`t`"path`"`t`t`"$($Libraries[$i] -replace '\\', '\\')`"`n`t`t`"apps`"`n`t`t{`n`t`t}`n`t}"
        }
        Set-Content -LiteralPath (Join-Path $SteamRoot 'steamapps/libraryfolders.vdf') `
            -Value "`"libraryfolders`"`n{`n$($entries -join "`n")`n}"
    }

    # A ReloadedII.json naming LAUNCHER, with the game registered at GAME when it's given.
    function Write-Loader([string] $Launcher, [string] $Game) {
        $apps = New-Item -ItemType Directory -Force (Join-Path $root 'Apps')
        @{ LauncherPath = $Launcher; ApplicationConfigDirectory = $apps.FullName } | ConvertTo-Json |
            Set-Content -LiteralPath $config
        if ($Game) {
            $app = New-Item -ItemType Directory -Force (Join-Path $apps 'metaphor.exe')
            @{ AppId = 'metaphor.exe'; AppLocation = $Game } | ConvertTo-Json | Set-Content (Join-Path $app 'AppConfig.json')
        }
    }

    # Select-MetaphorGame with ANSWERS given in order, $null past the last; QUESTIONS records what it asked.
    function Invoke-Select([object[]] $Answers = @()) {
        $questions = [Collections.Generic.List[string]]::new()
        $script:questions = $questions
        $queue = [Collections.Generic.Queue[object]]::new($Answers)
        $ask = { param($Question) $questions.Add($Question); if ($queue.Count) { $queue.Dequeue() } else { $null } }.GetNewClosure()
        Select-MetaphorGame -Ask $ask -SteamRoot $steam -ConfigPath $config 6> $null
    }
}

Describe 'Reading UTF-8 files' {
    BeforeAll {
        # A name with e acute, n tilde, the two kanji of "Japan" and a space, spelt in code so this file stays ASCII.
        $script:name = "Jos$([char]0xE9) Mu$([char]0xF1)oz $([char]0x65E5)$([char]0x672C)"

        # TEXT written as UTF-8, as Reloaded-II and Steam write their files: no byte order mark (both checked on the
        # owner's machine, 2026-11-19), or with one when BOM is set.
        function Write-Utf8([string] $Path, [string] $Text, [bool] $Bom) {
            [IO.File]::WriteAllText($Path, $Text, (New-Object Text.UTF8Encoding $Bom))
        }

        # BODY run inside the module in a new SHELL process, as a player's Windows PowerShell 5.1 runs the installer;
        # its output, which the caller keeps ASCII.
        function Invoke-InModule([string] $Shell, [string] $Body) {
            $module = Join-Path $repo 'scripts/player/MetaphorInstall.psm1'
            $command = "`$ProgressPreference = 'SilentlyContinue'; Import-Module '$($module -replace "'", "''")'; & (Get-Module MetaphorInstall) { $Body }"
            $output = & $Shell -NoProfile -NonInteractive -EncodedCommand (
                [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command)))
            $LASTEXITCODE | Should -Be 0
            $output
        }
        function ConvertTo-Quoted([string] $Text) { "'$($Text -replace "'", "''")'" }
    }
    BeforeEach {
        if (-not (Get-Command $Shell -ErrorAction SilentlyContinue)) { Set-ItResult -Skipped -Because "$Shell is not here" }
        $script:root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
    }

    It 'reads a Reloaded-II config holding a non-ASCII path in <Shell> (byte order mark: <Bom>)' -ForEach @(
        @{ Shell = 'pwsh'; Bom = $false }, @{ Shell = 'pwsh'; Bom = $true }
        @{ Shell = 'powershell.exe'; Bom = $false }, @{ Shell = 'powershell.exe'; Bom = $true }) {
        $launcher = "C:\Users\$name\Reloaded-II\Reloaded-II.exe"
        $config = Join-Path $root 'ReloadedII.json'
        Write-Utf8 $config (@{ LauncherPath = $launcher } | ConvertTo-Json) $Bom

        Invoke-InModule $Shell "(Read-Json $(ConvertTo-Quoted $config)).LauncherPath -ceq $(ConvertTo-Quoted $launcher)" |
            Should -Be 'True'
    }

    It 'finds a Steam library at a non-ASCII path in <Shell> (byte order mark: <Bom>)' -ForEach @(
        @{ Shell = 'pwsh'; Bom = $false }, @{ Shell = 'pwsh'; Bom = $true }
        @{ Shell = 'powershell.exe'; Bom = $false }, @{ Shell = 'powershell.exe'; Bom = $true }) {
        $steam = Join-Path $root 'Steam'
        $library = Join-Path $root "Biblioth$([char]0xE8)que $name"
        New-Library $steam
        New-Library $library 'METAPHOR'
        Write-Utf8 (Join-Path $steam 'steamapps/libraryfolders.vdf') (
            "`"libraryfolders`"`n{`n`t`"0`"`n`t{`n`t`t`"path`"`t`t`"$($library -replace '\\', '\\')`"`n`t}`n}") $Bom

        Invoke-InModule $Shell "@(Get-SteamLibrary $(ConvertTo-Quoted $steam)) -ccontains $(ConvertTo-Quoted $library)" |
            Should -Be 'True'
    }
}

Describe 'Select-MetaphorGame' {
    BeforeEach {
        $script:root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $script:steam = Join-Path $root 'Steam'
        $script:config = Join-Path $root 'ReloadedII.json'
        New-Library $steam
        $script:savedGame = $env:METAPHOR_GAME_EXE
        $env:METAPHOR_GAME_EXE = $null
    }
    AfterEach { $env:METAPHOR_GAME_EXE = $script:savedGame }

    It 'finds the game in a second Steam library and asks before using it' {
        $library = Join-Path $root 'Games'
        New-Library $library 'METAPHOR'
        Write-LibraryFolders $steam @($steam, $library)

        (Invoke-Select @('')).Path | Should -Be (Join-Path $library 'steamapps\common\METAPHOR\METAPHOR.exe')
        $questions | Should -Be @(
            'Use it, saving its path in your Windows user''s METAPHOR_GAME_EXE environment variable? [Y/n]')
    }

    It 'uses nothing after a no, or with no answer (no console)' {
        New-Library $steam 'Some Folder'

        Invoke-Select @('n') | Should -BeNullOrEmpty
        Invoke-Select @() | Should -BeNullOrEmpty
    }

    It 'offers the game registered in Reloaded-II, and lists each copy once to pick from' {
        $library = Join-Path $root 'Games'
        New-Library $steam 'METAPHOR'
        New-Library $library 'METAPHOR'
        Write-LibraryFolders $steam @($steam, $library, $library)
        $registered = Join-Path $root 'Registered\METAPHOR.exe'
        New-Item -ItemType File -Force $registered | Out-Null
        Write-Loader (Join-Path $root 'Reloaded-II.exe') $registered

        (Invoke-Select @('1')).Path | Should -Be $registered
        $questions | Should -Be @('Which one? [1-3, Enter to skip]')
    }

    It 'asks for the path when nothing is found, and takes only an existing game exe' {
        Write-LibraryFolders $steam @($steam, (Join-Path $root 'gone'))
        $typed = Join-Path $root 'Elsewhere\METAPHOR.exe'
        New-Item -ItemType File -Force $typed | Out-Null

        (Invoke-Select @("`"$typed`"")).Path | Should -Be $typed
        Invoke-Select @((Join-Path $root 'missing\METAPHOR.exe')) | Should -BeNullOrEmpty
        Invoke-Select @('') | Should -BeNullOrEmpty
    }

    It 'keeps METAPHOR_GAME_EXE without asking' {
        $env:METAPHOR_GAME_EXE = Join-Path $root 'Env\METAPHOR.exe'
        New-Item -ItemType File -Force $env:METAPHOR_GAME_EXE | Out-Null
        New-Library $steam 'METAPHOR'

        $choice = Invoke-Select
        $choice.Path | Should -Be $env:METAPHOR_GAME_EXE
        $choice.Saved | Should -BeTrue
        $questions.Count | Should -Be 0
    }
}

Describe 'Find-ReloadedLauncher' {
    BeforeEach {
        $script:root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $script:config = Join-Path $root 'ReloadedII.json'
        $script:launcher = Join-Path $root 'Reloaded-II.exe'
        $script:savedLauncher = $env:METAPHOR_RELOADED_EXE
        $env:METAPHOR_RELOADED_EXE = $null
    }
    AfterEach { $env:METAPHOR_RELOADED_EXE = $script:savedLauncher }

    It 'returns the launcher named by LauncherPath in ReloadedII.json' {
        Set-Content -LiteralPath $launcher -Value 'stand-in'
        Write-Loader $launcher

        Find-ReloadedLauncher -ConfigPath $config | Should -Be $launcher
    }

    It 'prefers METAPHOR_RELOADED_EXE, as deploy-dev.ps1 does' {
        $env:METAPHOR_RELOADED_EXE = Join-Path $root 'Other\Reloaded-II.exe'
        New-Item -ItemType File -Force $env:METAPHOR_RELOADED_EXE | Out-Null
        Set-Content -LiteralPath $launcher -Value 'stand-in'
        Write-Loader $launcher

        Find-ReloadedLauncher -ConfigPath $config | Should -Be $env:METAPHOR_RELOADED_EXE
    }

    It 'returns nothing when the config or the launcher is missing' {
        Find-ReloadedLauncher -ConfigPath $config | Should -BeNullOrEmpty
        Write-Loader $launcher
        Find-ReloadedLauncher -ConfigPath $config | Should -BeNullOrEmpty
    }
}

Describe 'the console questions' {
    It 'answers no instead of failing under -NonInteractive' {
        $module = Join-Path $repo 'scripts/player/MetaphorInstall.psm1'
        $output = & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -NonInteractive -Command (
            "Import-Module '$module'; 'interactive=' + (Test-Interactive); 'confirmed=' + (Confirm-Step 'Go?')") 2>&1
        ($output -join "`n") | Should -Match 'interactive=False'
        ($output -join "`n") | Should -Match 'confirmed=False'
        ($output -join "`n") | Should -Match 'no console'
        $LASTEXITCODE | Should -Be 0
    }

    It 'is how setup-wsl.ps1 asks: through Confirm-Step, never Read-Host' {
        $ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $repo 'scripts/setup/setup-wsl.ps1'),
            [ref]$null, [ref]$null)
        $commands = @($ast.FindAll({ param($n) $n -is [Management.Automation.Language.CommandAst] }, $true) |
                ForEach-Object { $_.GetCommandName() })
        $commands | Should -Contain 'Confirm-Step'
        $commands | Should -Not -Contain 'Read-Host'
    }
}
