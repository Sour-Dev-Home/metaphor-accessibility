# Tests for scripts/player/install.ps1 and the installer's steps in scripts/player/MetaphorInstall.psm1 (#303), against
# stand-ins in the test folder: a release folder laid out as the package is, a Steam library holding a stand-in game,
# Reloaded-II's settings and folders, and download pins pointing at stand-in files (file:// URLs): a Reloaded-II zip, a
# hooks library zip, and two "runtime installers" that are batch files recording themselves in a test folder and a test
# registry key under HKCU. No network, no speech, no admin prompt, and no real Reloaded-II, game or runtime. Run: Invoke-Pester tests/pester (Pester 5+).

BeforeAll {
    $script:repo = (Resolve-Path "$PSScriptRoot/../..").Path
    Import-Module (Join-Path $repo 'scripts/player/MetaphorInstall.psm1') -Force
    # What the package ships for the installer and the check, beside the mod's own files (Metaphor.Hooks.csproj).
    $script:shipped = 'install.ps1', 'MetaphorInstall.psm1', 'install-pins.json', 'check-install.ps1'
    $script:modId = 'metaphor.accessibility'
    $script:pwsh = (Get-Process -Id $PID).Path

    function Get-Sha256([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
    function ConvertTo-Literal([string] $Value) { "'" + ($Value -replace "'", "''") + "'" }

    function Write-JsonFile([string] $Path, $Value) {
        New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
        $Value | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Path
    }

    # A zip at PATH holding FILES (name to text) at its root.
    function New-Zip([string] $Path, [hashtable] $Files) {
        $folder = New-Item -ItemType Directory -Force (Join-Path $root "zip-$(New-Guid)")
        foreach ($name in $Files.Keys) { Set-Content -LiteralPath (Join-Path $folder $name) -Value $Files[$name] -NoNewline }
        Compress-Archive -Path (Join-Path $folder '*') -DestinationPath $Path
    }

    # A stand-in runtime installer: a batch file that runs COMMANDS, records its arguments and whether it could write to
    # itself while running (the installer holds it open against writes), and exits with CODE.
    function New-Installer([string] $Path, [string[]] $Commands, [int] $Code = 0) {
        $lines = @('@echo off', "echo %*>> `"$root\installer-calls.txt`"",
            "(type nul>>`"%~f0`") 2>nul && (echo writable>> `"$root\installer-lock.txt`") || (echo locked>> `"$root\installer-lock.txt`")") +
            $Commands + "exit /b $Code"
        Set-Content -LiteralPath $Path -Value $lines -Encoding ascii
    }

    # Git Bash beside git on Windows (git.exe in cmd\, bash.exe in bin\, as onboard.ps1's Find-GitBash looks), so a plain
    # pwsh never picks WSL's bash.exe from System32; bash elsewhere.
    function Find-Bash {
        # $IsWindows is unset in Windows PowerShell 5.1, so ask the OS.
        if ([Environment]::OSVersion.Platform -ne 'Win32NT') { return 'bash' }
        $git = Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($git) {
            $dir = Split-Path $git.Source
            foreach ($candidate in (Join-Path $dir 'bash.exe'), (Join-Path $dir '..\bin\bash.exe'), (Join-Path $dir '..\..\bin\bash.exe')) {
                if (Test-Path -LiteralPath $candidate) { return [IO.Path]::GetFullPath($candidate) }
            }
        }
        'bash'
    }
    $script:bash = Find-Bash

    # The release's package-files.txt, written by the package's own recipe (scripts/release/package-mod.sh --list), so the
    # installer reads the list a release ships and not a copy of its format.
    function Write-PackageList {
        $output = & $bash (Join-Path $repo 'scripts/release/package-mod.sh') --list $release 2>&1
        if ($LASTEXITCODE -ne 0) { throw "package-mod.sh --list failed: $output" }
    }

    function New-Pin([string] $Name, [string] $File, [hashtable] $Extra = @{}) {
        $pin = @{ name = $Name; version = '1.0'; url = ([Uri] $File).AbsoluteUri; fileName = (Split-Path -Leaf $File)
            sha256 = (Get-Sha256 $File) }
        foreach ($key in $Extra.Keys) { $pin[$key] = $Extra[$key] }
        $pin
    }

    # A computer under ROOT with the game in a Steam library and nothing else: no Reloaded-II, no runtimes. The pins
    # point at stand-ins that "install" into DOTNET and the test registry key.
    function New-Machine {
        $script:game = Join-Path $root 'Steam/steamapps/common/METAPHOR ReFantazio/METAPHOR.exe'
        New-Item -ItemType Directory -Force (Split-Path $game) | Out-Null
        Set-Content -LiteralPath $game -Value 'stand-in game' -NoNewline
        $script:steam = Join-Path $root 'Steam'
        $script:config = Join-Path $root 'AppData/Reloaded-Mod-Loader-II/ReloadedII.json'
        $script:reloadedFolder = Join-Path $root 'Programs/Reloaded-II'
        $script:desktop = Join-Path $root 'Desktop'
        $script:dotnet = Join-Path $root 'dotnet'
        $script:vcKeyPath = "Software\MetaphorInstallTest-$(New-Guid)"
        $script:vcKey = "HKCU:\$vcKeyPath\x64"

        $downloads = New-Item -ItemType Directory -Force (Join-Path $root 'Downloads-server')
        New-Zip (Join-Path $downloads 'Release.zip') @{ 'Reloaded-II.exe' = 'stand-in launcher'; 'LICENSE.txt' = 'stand-in' }
        New-Zip (Join-Path $downloads 'hooks.zip') @{ 'ModConfig.json' = '{ "ModId": "reloaded.sharedlib.hooks" }' }
        New-Installer (Join-Path $downloads 'dotnet.cmd') @(
            "mkdir `"$dotnet\shared\Microsoft.NETCore.App\9.0.23`"", "mkdir `"$dotnet\shared\Microsoft.WindowsDesktop.App\9.0.23`"")
        New-Installer (Join-Path $downloads 'vc.cmd') @("reg add `"HKCU\$vcKeyPath\x64`" /v Version /t REG_SZ /d v14.44.35211.00 /f >nul")
        $script:pins = Join-Path $root 'pins.json'
        Write-Pins @{
            reloaded = (New-Pin 'Reloaded-II' (Join-Path $downloads 'Release.zip'))
            hooksLibrary = (New-Pin 'Reloaded-II''s hooks library' (Join-Path $downloads 'hooks.zip') @{ modId = 'reloaded.sharedlib.hooks' })
            dotnetDesktop = (New-Pin '.NET 9 Desktop Runtime' (Join-Path $downloads 'dotnet.cmd') @{
                    minimumVersion = '9.0.8'; frameworks = @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App')
                    signer = 'CN=.NET, O=Microsoft Corporation, L=Redmond, S=Washington, C=US' })
            vcRedist = (New-Pin 'Microsoft Visual C++ Redistributable' (Join-Path $downloads 'vc.cmd') @{
                    signer = 'CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US' })
        }

        $script:release = Join-Path $root "Downloads/$modId"
        New-Item -ItemType Directory -Force $release | Out-Null
        foreach ($file in $shipped) { Copy-Item -LiteralPath (Join-Path $repo "scripts/player/$file") $release }
        Write-JsonFile (Join-Path $release 'ModConfig.json') @{ ModId = $modId; ModName = 'Metaphor Accessibility'
            ModVersion = '0.1.0'; ModDll = 'Metaphor.Hooks.dll' }
        Set-Content -LiteralPath (Join-Path $release 'Metaphor.Hooks.dll') -Value 'stand-in mod' -NoNewline
        New-Item -ItemType Directory -Force (Join-Path $release 'licenses') | Out-Null
        Set-Content -LiteralPath (Join-Path $release 'licenses/LICENSE.txt') -Value 'stand-in licence' -NoNewline
        Set-Content -LiteralPath (Join-Path $release 'prism.dll') -Value 'stand-in prism' -NoNewline
        Write-JsonFile (Join-Path $release 'prism.json') @{ version = 'v0'; dllSha256 = (Get-Sha256 (Join-Path $release 'prism.dll')) }
        Write-JsonFile (Join-Path $release 'known-game-builds.json') @(
            @{ sha256 = (Get-Sha256 $game); steamBuildId = 18330018; evidence = 'stand-in' })
        Write-PackageList
    }

    function Write-Pins([hashtable] $Pins) { Write-JsonFile $script:pins $Pins }

    # Reloaded-II set up already, as a player who used it before has it: its settings, the hooks library, the game
    # added with another mod turned on, and both runtimes.
    function New-ReloadedSetup {
        New-Item -ItemType Directory -Force $reloadedFolder | Out-Null
        $script:launcher = Join-Path $reloadedFolder 'Reloaded-II.exe'
        Set-Content -LiteralPath $launcher -Value 'stand-in launcher'
        $script:mods = (New-Item -ItemType Directory -Force (Join-Path $reloadedFolder 'Mods')).FullName
        $apps = (New-Item -ItemType Directory -Force (Join-Path $reloadedFolder 'Apps')).FullName
        Write-JsonFile $config @{ LauncherPath = $launcher; ApplicationConfigDirectory = $apps; ModConfigDirectory = $mods
            LogFileCompressTimeHours = 6 }
        Write-JsonFile (Join-Path $mods 'reloaded.sharedlib.hooks/ModConfig.json') @{ ModId = 'reloaded.sharedlib.hooks' }
        Write-JsonFile (Join-Path $mods 'other.mod/ModConfig.json') @{ ModId = 'other.mod' }
        $script:appConfig = Join-Path $apps 'metaphor.exe/AppConfig.json'
        Write-JsonFile $appConfig @{ AppId = 'metaphor.exe'; AppName = 'My Metaphor'; AppLocation = $game
            EnabledMods = @('other.mod'); SortedMods = @('other.mod') }
        New-Runtimes
    }

    function New-Runtimes {
        foreach ($framework in 'Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App') {
            New-Item -ItemType Directory -Force (Join-Path $dotnet "shared/$framework/9.0.21") | Out-Null
        }
        New-Item -Force $vcKey | Out-Null
        New-ItemProperty -LiteralPath $vcKey -Name Version -Value 'v14.40.0.0' -Force | Out-Null
    }

    # install.ps1 from the release folder in its own process, as a player runs it. With ANSWERS it's a scripted run;
    # with -NonInteractive, a run with no console and no answers. SHELL is pwsh or powershell (Windows PowerShell 5.1).
    # SWITCHES are the installer's test switches (MetaphorInstall.psm1's Test-TestSwitch) to turn on: by default local file
    # pins, no signature check on them (the stand-ins are unsigned), and running as admin (CI's runners are). Any switch
    # also sets CI=true, without which none counts. ENVIRONMENT sets more variables for the run.
    function Invoke-Install([string[]] $Answers = @(), [switch] $NonInteractive, [string] $Shell = $pwsh,
        [string] $Folder = $release, [string[]] $Switches = @('FILES', 'UNSIGNED', 'ALLOW_ADMIN'), [hashtable] $Environment = @{}) {
        $script = Join-Path $Folder 'install.ps1'
        $common = "-NoSpeech -ReloadedConfig $(ConvertTo-Literal $config) -ReloadedFolder $(ConvertTo-Literal $reloadedFolder) " +
            "-SteamRoot $(ConvertTo-Literal $steam) -Desktop $(ConvertTo-Literal $desktop) -DotnetRoot $(ConvertTo-Literal $dotnet) " +
            "-VcRuntimeKey $(ConvertTo-Literal $vcKey) -Pins $(ConvertTo-Literal $pins)"
        $vars = @{ METAPHOR_RELOADED_EXE = $null; METAPHOR_GAME_EXE = $null; CI = $null }
        foreach ($name in 'FILES', 'UNSIGNED', 'ALLOW_ADMIN') {
            $vars["METAPHOR_INSTALL_TEST_$name"] = if ($Switches -contains $name) { '1' } else { $null }
        }
        if ($Switches) { $vars.CI = 'true' }
        foreach ($name in $Environment.Keys) { $vars[$name] = $Environment[$name] }
        $saved = @{}
        foreach ($name in $vars.Keys) {
            $saved[$name] = [Environment]::GetEnvironmentVariable($name)
            [Environment]::SetEnvironmentVariable($name, $vars[$name])
        }
        try {
            if ($NonInteractive) { $command = "& $(ConvertTo-Literal $script) $common; exit `$LASTEXITCODE" }
            else {
                $list = ($Answers | ForEach-Object { ConvertTo-Literal $_ }) -join ','
                $command = "& $(ConvertTo-Literal $script) $common -Answers @($list); exit `$LASTEXITCODE"
            }
            $extra = @(if ($NonInteractive) { '-NonInteractive' })
            $output = & $Shell -NoProfile @extra -ExecutionPolicy Bypass -Command $command 2>&1
            $code = $LASTEXITCODE
        }
        finally { foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) } }
        [pscustomobject]@{ ExitCode = $code; Output = (@($output | ForEach-Object { "$_" }) -join "`n") }
    }

    function Get-Shortcut([string] $Path) {
        $shell = New-Object -ComObject WScript.Shell
        try { $link = $shell.CreateShortcut($Path); [pscustomobject]@{ Target = $link.TargetPath; Arguments = $link.Arguments } }
        finally { [void] [Runtime.InteropServices.Marshal]::ReleaseComObject($shell) }
    }

    function Get-Snapshot {
        Get-ChildItem -LiteralPath $root -Recurse -File -Force | Where-Object { $_.FullName -notlike "$root\Downloads*" } |
            ForEach-Object { "$($_.FullName) $($_.LastWriteTimeUtc.Ticks)" }
    }

    # Denies this user new files and folders in FOLDER and below, as Windows Security's Controlled folder access does to
    # a program it blocks: writes fail with access denied (0x80070005). Not the whole W right, whose SYNCHRONIZE would
    # also stop reads. Unblock-Write lifts it.
    $script:user = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    function Block-Write([string] $Folder) {
        New-Item -ItemType Directory -Force $Folder | Out-Null
        icacls $Folder /deny "*${user}:(OI)(CI)(WD,AD)" | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "icacls could not deny writes in $Folder" }
    }
    function Unblock-Write([string] $Folder) { icacls $Folder /remove:d "*$user" | Out-Null }

    # Answers for a run on a computer with nothing installed: go ahead, use the game found, then yes to each download.
    $script:freshAnswers = 'y', '', 'y', 'y', 'y', 'y'
}

Describe 'install.ps1' {
    BeforeEach {
        $script:root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        New-Machine
    }
    AfterEach { Remove-Item -LiteralPath "HKCU:\$vcKeyPath" -Recurse -Force -ErrorAction SilentlyContinue }

    It 'installs Reloaded-II, its runtimes and hooks library, the mod and the shortcut on a computer without them' {
        $result = Invoke-Install $freshAnswers
        $result.Output | Should -Match 'The install is done'
        $result.ExitCode | Should -Be 0

        $launcher = Join-Path $reloadedFolder 'Reloaded-II.exe'
        $launcher | Should -Exist
        $loader = Get-Content -Raw $config | ConvertFrom-Json
        $loader.LauncherPath | Should -Be $launcher
        $loader.ModConfigDirectory | Should -Be (Join-Path $reloadedFolder 'Mods')
        Join-Path $dotnet 'shared/Microsoft.WindowsDesktop.App/9.0.23' | Should -Exist
        (Get-ItemProperty -LiteralPath $vcKey).Version | Should -Be 'v14.44.35211.00'
        Get-Content (Join-Path $root 'installer-calls.txt') | Should -Be @('/install /quiet /norestart', '/install /quiet /norestart')
        Join-Path $reloadedFolder 'Mods/reloaded.sharedlib.hooks/ModConfig.json' | Should -Exist

        $installed = Join-Path $reloadedFolder "Mods/$modId"
        foreach ($file in Get-ChildItem -LiteralPath $release -Recurse -File) {
            Get-Sha256 ($file.FullName.Replace($release, $installed)) | Should -Be (Get-Sha256 $file.FullName)
        }
        $app = Get-Content -Raw (Join-Path $reloadedFolder 'Apps/metaphor.exe/AppConfig.json') | ConvertFrom-Json
        $app.AppId | Should -Be 'metaphor.exe'
        $app.AppLocation | Should -Be $game
        @($app.EnabledMods) | Should -Be @($modId)

        $shortcut = Get-Shortcut (Join-Path $desktop 'Metaphor ReFantazio with accessibility.lnk')
        $shortcut.Target | Should -Be $launcher
        $shortcut.Arguments | Should -Be "--launch `"$game`""
    }

    It 'says each step and its result, and ends with the install check' {
        $result = Invoke-Install $freshAnswers
        foreach ($n in 1..10) { $result.Output | Should -Match "Step $n of 10: " }
        ([regex]::Matches($result.Output, '(?m)^Done: ')).Count | Should -Be 9
        $result.Output | Should -Match 'Pass: The mod is turned on for the game\.'
        $result.Output | Should -Match 'Problem: The mod has not reported a start'
    }

    It 'changes nothing on a second run, says so, and asks only whether to go ahead' {
        (Invoke-Install $freshAnswers).ExitCode | Should -Be 0
        $before = Get-Snapshot
        $result = Invoke-Install @('y')
        $result.ExitCode | Should -Be 0
        ([regex]::Matches($result.Output, '(?m)^Nothing to do: ')).Count | Should -Be 9
        $result.Output | Should -Not -Match '(?m)^Done: '
        $result.Output | Should -Not -Match 'no answer given'
        Get-Snapshot | Should -Be $before
    }

    It 'adds the mod to a game already in Reloaded-II, keeping its other mods and settings' {
        New-ReloadedSetup
        $loaderBefore = Get-Sha256 $config
        $result = Invoke-Install @('y')
        $result.ExitCode | Should -Be 0
        $app = Get-Content -Raw $appConfig | ConvertFrom-Json
        @($app.EnabledMods) | Should -Be @('other.mod', $modId)
        @($app.SortedMods) | Should -Be @('other.mod')
        $app.AppName | Should -Be 'My Metaphor'
        Get-Sha256 $config | Should -Be $loaderBefore
        Join-Path $mods 'other.mod/ModConfig.json' | Should -Exist
        Test-Path (Join-Path $root 'installer-calls.txt') | Should -BeFalse
    }

    It 'copies only the files that changed, and keeps files the mod wrote' {
        New-ReloadedSetup
        (Invoke-Install @('y')).ExitCode | Should -Be 0
        $installed = Join-Path $mods $modId
        Set-Content -LiteralPath (Join-Path $installed 'mod-status.json') -Value '{}'
        Set-Content -LiteralPath (Join-Path $release 'Metaphor.Hooks.dll') -Value 'a newer stand-in mod' -NoNewline
        $result = Invoke-Install @('y')
        $result.Output | Should -Match 'Copied 1 of the mod''s files'
        Get-Content -Raw (Join-Path $installed 'Metaphor.Hooks.dll') | Should -Be 'a newer stand-in mod'
        Join-Path $installed 'mod-status.json' | Should -Exist
    }

    It 'stops at a download answered no, says how to fix it, and changes nothing after it' {
        $result = Invoke-Install @('y', '', 'n')
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Failed: Reloaded-II was not installed, because the answer was no\.'
        $result.Output | Should -Match 'To fix it: Run the installer again and answer yes'
        $result.Output | Should -Match 'stopped at step 2'
        $result.Output | Should -Not -Match 'Step 3 of'
        $reloadedFolder | Should -Not -Exist
        $config | Should -Not -Exist
        $desktop | Should -Not -Exist
    }

    It 'deletes a download whose SHA-256 is not the pinned one, unused' {
        $pinned = Get-Content -Raw $pins | ConvertFrom-Json
        $pinned.reloaded.sha256 = '0' * 64
        Write-JsonFile $pins $pinned
        $result = Invoke-Install $freshAnswers
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'is not the file this installer expects, so it was deleted unused'
        $reloadedFolder | Should -Not -Exist
    }

    It 'refuses a pin that is not https' {
        $pinned = Get-Content -Raw $pins | ConvertFrom-Json
        $pinned.reloaded.url = 'http://example.invalid/Release.zip'
        Write-JsonFile $pins $pinned
        $result = Invoke-Install $freshAnswers
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'only https downloads'
        $reloadedFolder | Should -Not -Exist
    }

    It 'stops when a runtime installer fails, with its exit code, before the next step' {
        New-Installer (Join-Path $root 'Downloads-server/dotnet.cmd') @() 1603
        $pinned = Get-Content -Raw $pins | ConvertFrom-Json
        $pinned.dotnetDesktop.sha256 = Get-Sha256 (Join-Path $root 'Downloads-server/dotnet.cmd')
        Write-JsonFile $pins $pinned
        $result = Invoke-Install $freshAnswers
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Failed: The install of \.NET 9 Desktop Runtime did not work\. Exit code: 1603'
        $result.Output | Should -Match 'stopped at step 4'
        Test-Path -LiteralPath $vcKey | Should -BeFalse
    }

    It 'says plainly that a runtime install needs administrator rights before asking' {
        $result = Invoke-Install $freshAnswers
        $result.Output | Should -Match 'This needs administrator rights: Windows will ask for permission'
        $result.Output | Should -Match 'This does not need administrator rights'
    }

    It 'asks for the game''s path when Steam has none, and downloads nothing without one' {
        Remove-Item -LiteralPath (Split-Path $game) -Recurse -Force
        $result = Invoke-Install @('y', '')
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Type the full path to METAPHOR.exe'
        $result.Output | Should -Match 'stopped at step 1'
        $reloadedFolder | Should -Not -Exist
    }

    It 'leaves a non-empty folder where it would put Reloaded-II alone' {
        New-Item -ItemType Directory -Force $reloadedFolder | Out-Null
        Set-Content -LiteralPath (Join-Path $reloadedFolder 'notes.txt') -Value 'mine'
        $result = Invoke-Install $freshAnswers
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'already holds other files, so it was left alone'
        @(Get-ChildItem -LiteralPath $reloadedFolder).Name | Should -Be @('notes.txt')
    }

    It 'changes nothing without a console to answer on' {
        $result = Invoke-Install -NonInteractive
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Nothing was changed, because the answer was no\.'
        $reloadedFolder | Should -Not -Exist
        $config | Should -Not -Exist
    }

    It 'says to run it from the mod''s folder when it is not there' {
        $alone = New-Item -ItemType Directory (Join-Path $root 'alone')
        foreach ($file in $shipped) { Copy-Item -LiteralPath (Join-Path $repo "scripts/player/$file") $alone }
        $result = Invoke-Install @('y') -Folder $alone
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Run install.ps1 from the mod''s folder in the release zip'
    }

    It 'holds each runtime installer open against writes while it runs' {
        (Invoke-Install $freshAnswers).ExitCode | Should -Be 0
        Get-Content (Join-Path $root 'installer-lock.txt') | Should -Be @('locked', 'locked')
    }

    It 'refuses a runtime installer without the pinned Microsoft signature, before running it' {
        $result = Invoke-Install $freshAnswers -Switches 'FILES', 'ALLOW_ADMIN'
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'The downloaded \.NET 9 Desktop Runtime is not the file this installer expects, so it was deleted unused\. .*signature \w+, signer none, expected CN=\.NET'
        $result.Output | Should -Match 'stopped at step 4'
        Test-Path (Join-Path $root 'installer-calls.txt') | Should -BeFalse
    }

    It 'refuses a file:// pin unless the tests turn it on' {
        $result = Invoke-Install $freshAnswers -Switches 'ALLOW_ADMIN'
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'only https downloads'
        $reloadedFolder | Should -Not -Exist
    }

    It 'refuses a file:// pin on a network share, even in the tests' {
        $pinned = Get-Content -Raw $pins | ConvertFrom-Json
        $pinned.reloaded.url = 'file://server/share/Release.zip'
        Write-JsonFile $pins $pinned
        $result = Invoke-Install $freshAnswers
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'no network file URLs'
    }

    It 'keeps the game''s old settings file as a backup when it rewrites it, and leaves no temporary file' {
        New-ReloadedSetup
        $old = Get-Content -Raw $appConfig
        (Invoke-Install @('y')).ExitCode | Should -Be 0
        Get-Content -Raw "$appConfig.bak" | Should -Be $old
        "$appConfig.tmp" | Should -Not -Exist
    }

    It 'keeps the first backup of the game''s settings file when it rewrites it again' {
        New-ReloadedSetup
        $old = Get-Content -Raw $appConfig
        (Invoke-Install @('y')).ExitCode | Should -Be 0
        $app = Get-Content -Raw $appConfig | ConvertFrom-Json
        $app.EnabledMods = @('other.mod')
        Write-JsonFile $appConfig $app
        $result = Invoke-Install @('y')
        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'turned the mod on'
        Get-Content -Raw "$appConfig.bak" | Should -Be $old
    }

    It 'ignores the developer scripts'' environment variables for Reloaded-II and the game' {
        $devLauncher = Join-Path $root 'Dev/Reloaded-II/Reloaded-II.exe'
        $devGame = Join-Path $root 'Dev/Game/METAPHOR.exe'
        foreach ($file in $devLauncher, $devGame) {
            New-Item -ItemType Directory -Force (Split-Path $file) | Out-Null
            Set-Content -LiteralPath $file -Value 'a developer''s copy' -NoNewline
        }
        $result = Invoke-Install $freshAnswers -Environment @{ METAPHOR_RELOADED_EXE = $devLauncher; METAPHOR_GAME_EXE = $devGame }
        $result.ExitCode | Should -Be 0
        $shortcut = Get-Shortcut (Join-Path $desktop 'Metaphor ReFantazio with accessibility.lnk')
        $shortcut.Target | Should -Be (Join-Path $reloadedFolder 'Reloaded-II.exe')
        $shortcut.Arguments | Should -Be "--launch `"$game`""
    }

    It 'copies only the files the package lists' {
        New-ReloadedSetup
        Set-Content -LiteralPath (Join-Path $release 'notes.txt') -Value 'not the package''s'
        (Invoke-Install @('y')).ExitCode | Should -Be 0
        Join-Path $mods "$modId/notes.txt" | Should -Not -Exist
        Join-Path $mods "$modId/package-files.txt" | Should -Exist
    }

    It 'stops at the mod''s files when the package list is missing' {
        New-ReloadedSetup
        Remove-Item -LiteralPath (Join-Path $release 'package-files.txt')
        $result = Invoke-Install @('y')
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'missing its list of files'
        Join-Path $mods $modId | Should -Not -Exist
    }

    It 'ignores a recorded launcher that is not Reloaded-II.exe' {
        New-Item -ItemType Directory -Force (Split-Path $config) | Out-Null
        $other = Join-Path $root 'Other/Other.exe'
        New-Item -ItemType Directory -Force (Split-Path $other) | Out-Null
        Set-Content -LiteralPath $other -Value 'not the launcher'
        Write-JsonFile $config @{ LauncherPath = $other }
        $saved = $env:METAPHOR_RELOADED_EXE
        $env:METAPHOR_RELOADED_EXE = $null
        try { Find-ReloadedLauncher -ConfigPath $config | Should -BeNullOrEmpty }
        finally { $env:METAPHOR_RELOADED_EXE = $saved }
    }

    # Checked in the body: scripts/check/run-pester.sh picks tests by line, which runs a test whatever its -Skip says.
    It 'refuses to run as administrator' {
        $principal = [Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
        if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
            Set-ItResult -Skipped -Because 'this process is not running as administrator'
            return
        }
        $result = Invoke-Install $freshAnswers -Switches 'FILES', 'UNSIGNED'
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'runs as administrator, so the installer stopped without changing anything'
        $reloadedFolder | Should -Not -Exist
    }

    It 'finishes the install without the shortcut when Windows blocks it, and names Controlled folder access' {
        New-ReloadedSetup
        Block-Write $desktop
        try { $result = Invoke-Install @('y') }
        finally { Unblock-Write $desktop }
        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'Failed: Windows did not let the installer save this step''s files\. Windows Security''s Controlled folder access may have blocked it\.'
        $result.Output | Should -Match 'To fix it: Windows Security may show a notification about a blocked change; it explains your choices there\. Then run the installer again\.'
        $result.Output | Should -Not -Match 'once Windows lets it save there'
        $result.Output | Should -Not -Match 'This step stopped with an error'
        $result.Output | Should -Match 'The install is done, but the desktop shortcut was not made\. The mod still works'
        @((Get-Content -Raw $appConfig | ConvertFrom-Json).EnabledMods) | Should -Be @('other.mod', $modId)
    }

    It 'stops when Windows blocks the copy into Reloaded-II, and names Controlled folder access' {
        New-ReloadedSetup
        Block-Write $mods
        try { $result = Invoke-Install @('y') }
        finally { Unblock-Write $mods }
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Failed: Windows did not let the installer save this step''s files\. Windows Security''s Controlled folder access may have blocked it\.'
        $result.Output | Should -Match 'To fix it: Windows Security may show a notification about a blocked change; it explains your choices there\. Then run the installer again\. If it stops here again, report it with the alpha feedback form'
        $result.Output | Should -Not -Match 'once Windows lets it save there'
        $result.Output | Should -Not -Match 'This step stopped with an error'
        $result.Output | Should -Match 'The installer stopped at step 7'
    }

    It 'keeps the report-it message for an error that is not a blocked write' {
        New-ReloadedSetup
        Set-Content -LiteralPath (Join-Path $mods $modId) -Value 'a file where the mod''s folder goes'
        $result = Invoke-Install @('y')
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Failed: This step stopped with an error\.'
        $result.Output | Should -Match 'To fix it: Report it with the alpha feedback form'
        $result.Output | Should -Not -Match 'Controlled folder access'
    }

    It 'runs in Windows PowerShell 5.1' -Skip:(-not (Get-Command powershell.exe -ErrorAction SilentlyContinue)) {
        $result = Invoke-Install $freshAnswers -Shell 'powershell.exe'
        $result.Output | Should -Match 'The install is done'
        $result.ExitCode | Should -Be 0
        $app = Get-Content -Raw (Join-Path $reloadedFolder 'Apps/metaphor.exe/AppConfig.json') | ConvertFrom-Json
        @($app.EnabledMods) | Should -Be @($modId)
        [IO.File]::ReadAllBytes($config)[0] | Should -Not -Be 0xEF
    }
}

Describe 'Test-DotnetRuntime' {
    BeforeAll {
        # The module's own function, not part of its interface.
        function Test-Runtime {
            InModuleScope MetaphorInstall -Parameters @{ Dotnet = $dotnet; Frameworks = $frameworks } {
                Test-DotnetRuntime $Dotnet $Frameworks ([version] '9.0.8')
            }
        }
    }
    BeforeEach { $script:dotnet = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName }

    It 'needs every framework at the minimum version or a later one of the same major' {
        $frameworks = 'Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App'
        New-Item -ItemType Directory -Force (Join-Path $dotnet 'shared/Microsoft.NETCore.App/9.0.8') | Out-Null
        Test-Runtime | Should -BeFalse
        New-Item -ItemType Directory -Force (Join-Path $dotnet 'shared/Microsoft.WindowsDesktop.App/9.0.7') | Out-Null
        Test-Runtime | Should -BeFalse
        New-Item -ItemType Directory -Force (Join-Path $dotnet 'shared/Microsoft.WindowsDesktop.App/10.0.1') | Out-Null
        Test-Runtime | Should -BeFalse
        New-Item -ItemType Directory -Force (Join-Path $dotnet 'shared/Microsoft.WindowsDesktop.App/9.0.23') | Out-Null
        Test-Runtime | Should -BeTrue
    }
}

Describe 'Test-Elevated' {
    BeforeAll {
        $script:savedEnvironment = @{}
        foreach ($name in 'METAPHOR_INSTALL_TEST_ALLOW_ADMIN', 'CI') { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
        Mock -ModuleName MetaphorInstall Test-AdministratorRole { $true }
    }
    AfterAll { foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) } }
    BeforeEach { $env:METAPHOR_INSTALL_TEST_ALLOW_ADMIN = '1' }

    It 'lets the tests run as administrator only on CI' {
        $env:CI = 'true'
        Test-Elevated | Should -BeFalse
    }

    It 'still refuses administrator off CI, even with the test switch set' {
        $env:CI = $null
        Test-Elevated | Should -BeTrue
        $env:CI = '1'
        Test-Elevated | Should -BeTrue
    }
}

Describe 'Get-PinnedFile' {
    It 'lets go of the download when hashing it fails' {
        $folder = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $server = (New-Item -ItemType Directory (Join-Path $folder 'server')).FullName
        $work = (New-Item -ItemType Directory (Join-Path $folder 'work')).FullName
        Set-Content -LiteralPath (Join-Path $server 'file.zip') -Value 'stand-in' -NoNewline
        $pin = [pscustomobject]@{ name = 'stand-in'; url = ([Uri] (Join-Path $server 'file.zip')).AbsoluteUri
            fileName = 'file.zip'; sha256 = '0' * 64 }
        Mock -ModuleName MetaphorInstall Get-StreamSha256 { throw 'hash failed' }
        $saved = @{ FILES = $env:METAPHOR_INSTALL_TEST_FILES; CI = $env:CI }
        $env:METAPHOR_INSTALL_TEST_FILES = '1'
        $env:CI = 'true'
        try {
            { InModuleScope MetaphorInstall -Parameters @{ Pin = $pin; Work = $work } {
                    Get-PinnedFile ([pscustomobject]@{ WorkFolder = $Work }) $Pin } } | Should -Throw '*hash failed*'
        }
        finally { $env:METAPHOR_INSTALL_TEST_FILES = $saved.FILES; $env:CI = $saved.CI }
        { [IO.File]::Open((Join-Path $work 'file.zip'), 'Open', 'ReadWrite', 'None').Dispose() } | Should -Not -Throw
    }

    It 'lets go of the download when checking its signature fails' {
        $folder = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $server = (New-Item -ItemType Directory (Join-Path $folder 'server')).FullName
        $work = (New-Item -ItemType Directory (Join-Path $folder 'work')).FullName
        Set-Content -LiteralPath (Join-Path $server 'file.exe') -Value 'stand-in' -NoNewline
        $pin = [pscustomobject]@{ name = 'stand-in'; url = ([Uri] (Join-Path $server 'file.exe')).AbsoluteUri
            fileName = 'file.exe'; sha256 = (Get-Sha256 (Join-Path $server 'file.exe')); signer = 'CN=stand-in' }
        # A certificate without a Subject, which strict mode refuses to read.
        Mock -ModuleName MetaphorInstall Get-AuthenticodeSignature {
            [pscustomobject]@{ Status = 'Valid'; SignerCertificate = [pscustomobject]@{ Thumbprint = '0' } }
        }
        $saved = @{ FILES = $env:METAPHOR_INSTALL_TEST_FILES; CI = $env:CI }
        $env:METAPHOR_INSTALL_TEST_FILES = '1'
        $env:CI = 'true'
        try {
            { InModuleScope MetaphorInstall -Parameters @{ Pin = $pin; Work = $work } {
                    Get-PinnedFile ([pscustomobject]@{ WorkFolder = $Work }) $Pin } } | Should -Throw '*Subject*'
        }
        finally { $env:METAPHOR_INSTALL_TEST_FILES = $saved.FILES; $env:CI = $saved.CI }
        { [IO.File]::Open((Join-Path $work 'file.exe'), 'Open', 'ReadWrite', 'None').Dispose() } | Should -Not -Throw
    }
}

Describe 'Test-TestSwitch' {
    BeforeAll {
        $script:savedEnvironment = @{}
        foreach ($name in 'METAPHOR_INSTALL_TEST_FILES', 'METAPHOR_INSTALL_TEST_UNSIGNED', 'CI') {
            $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
        }
    }
    AfterAll { foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) } }

    It 'counts <Name> only on CI' -ForEach @(@{ Name = 'FILES' }, @{ Name = 'UNSIGNED' }) {
        [Environment]::SetEnvironmentVariable("METAPHOR_INSTALL_TEST_$Name", '1')
        foreach ($ci in $null, '1') {
            $env:CI = $ci
            InModuleScope MetaphorInstall -Parameters @{ Name = $Name } { Test-TestSwitch $Name } | Should -BeFalse
        }
        $env:CI = 'true'
        InModuleScope MetaphorInstall -Parameters @{ Name = $Name } { Test-TestSwitch $Name } | Should -BeTrue
    }
}

Describe 'Find-Bash' {
    It 'finds the same Bash in Windows PowerShell 5.1, where $IsWindows is unset' -Skip:(
        -not (Get-Command powershell.exe -ErrorAction SilentlyContinue)) {
        $command = "function Find-Bash {${function:Find-Bash}}; Find-Bash"
        $found = & powershell.exe -NoProfile -EncodedCommand ([Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command)))
        $LASTEXITCODE | Should -Be 0
        $found | Should -Be $bash
    }
}

Describe 'package-files.txt' {
    It 'is read by the installer as package-mod.sh writes it' {
        $release = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        foreach ($file in 'Metaphor.Hooks.dll', 'ModConfig.json', 'licenses/LICENSE.txt', 'runtimes/win-x64/native/a.dll') {
            New-Item -ItemType Directory -Force (Split-Path (Join-Path $release $file)) | Out-Null
            Set-Content -LiteralPath (Join-Path $release $file) -Value 'stand-in' -NoNewline
        }
        Write-PackageList
        InModuleScope MetaphorInstall -Parameters @{ Release = $release } { Get-PackageFile $Release } | Should -Be @(
            'package-files.txt', 'Metaphor.Hooks.dll', 'ModConfig.json', 'licenses/LICENSE.txt', 'runtimes/win-x64/native/a.dll')
    }
}

Describe 'the package' {
    It 'ships the installer, every module and file it reads from its folder' {
        $project = Get-Content -Raw -LiteralPath (Join-Path $repo 'src/Metaphor.Hooks/Metaphor.Hooks.csproj')
        foreach ($file in $shipped) {
            $project | Should -Match ([regex]::Escape("../../scripts/player/$file"))
            if ($file -notlike '*.ps*1') { continue }
            foreach ($import in [regex]::Matches((Get-Content -Raw (Join-Path $repo "scripts/player/$file")), "Join-Path \`$PSScriptRoot '([^']+)'")) {
                # ModConfig.json is the mod's own, at the package's root.
                if ($import.Groups[1].Value -ne 'ModConfig.json') { $import.Groups[1].Value | Should -BeIn $shipped }
            }
        }
        $project | Should -Not -Match 'scripts/dev/ReloadedDev\.psm1'
    }

    It 'ships exactly the player folder' {
        @(git -C $repo ls-files scripts/player | ForEach-Object { Split-Path -Leaf $_ } | Sort-Object) |
            Should -Be @($shipped | Sort-Object)
    }

    It 'holds only ASCII in what Windows PowerShell 5.1 reads' {
        foreach ($file in $shipped) {
            [IO.File]::ReadAllBytes((Join-Path $repo "scripts/player/$file")) | Where-Object { $_ -gt 127 } | Should -BeNullOrEmpty
        }
    }

    It 'pins every download to an https URL, a plain file name and a SHA-256, from the raw source' {
        $pins = Get-Content -Raw (Join-Path $repo 'scripts/player/install-pins.json') | ConvertFrom-Json
        $source = Get-Content -Raw (Join-Path $repo $pins.source)
        foreach ($name in 'reloaded', 'hooksLibrary', 'dotnetDesktop', 'vcRedist') {
            $pin = $pins.$name
            $pin.url | Should -Match '^https://'
            $pin.fileName | Should -Match '^[A-Za-z0-9][A-Za-z0-9._-]*$'
            $pin.sha256 | Should -Match '^[0-9a-f]{64}$'
            $source | Should -Match $pin.sha256
            $source | Should -Match ([regex]::Escape($pin.url))
        }
    }
}
