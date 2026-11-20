# Tests for scripts/player/install.ps1 and the installer's steps in scripts/player/MetaphorInstall.psm1 (#303), against
# stand-ins in the test folder: a release folder laid out as the package is, a Steam library holding a stand-in game,
# Reloaded-II's settings and folders, and download pins pointing at stand-in files (file:// URLs): a Reloaded-II zip, a
# hooks library zip, and two "runtime installers" that are batch files recording themselves in a test folder and a test
# registry key under HKCU. No network, no speech, no admin prompt, and no real Reloaded-II, game or runtime. Run: Invoke-Pester tests/pester (Pester 5+).

BeforeAll {
    $script:repo = (Resolve-Path "$PSScriptRoot/../..").Path
    Import-Module (Join-Path $repo 'scripts/player/MetaphorInstall.psm1') -Force
    # What the package ships for the installer and the check, beside the mod's own files (Metaphor.Hooks.csproj).
    $script:shipped = 'install.ps1', 'install.cmd', 'uninstall.cmd', 'MetaphorInstall.psm1', 'install-pins.json', 'check-install.ps1'
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
    # itself while running (the installer holds it open against writes), and exits with CODE. cmd reads a batch file in
    # the system's OEM code page, so a test folder with é in its path is written in that (#459).
    function New-Installer([string] $Path, [string[]] $Commands, [int] $Code = 0) {
        $lines = @('@echo off', "echo %*>> `"$root\installer-calls.txt`"",
            "(type nul>>`"%~f0`") 2>nul && (echo writable>> `"$root\installer-lock.txt`") || (echo locked>> `"$root\installer-lock.txt`")") +
            $Commands + "exit /b $Code"
        $oem = [Text.Encoding]::GetEncoding([int] (Get-ItemPropertyValue 'HKLM:\SYSTEM\CurrentControlSet\Control\Nls\CodePage' OEMCP))
        [IO.File]::WriteAllLines($Path, [string[]] $lines, $oem)
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
    # also sets CI=true, without which none counts. ENVIRONMENT sets more variables for the run. UNINSTALL runs it with
    # -Uninstall.
    function Invoke-Install([string[]] $Answers = @(), [switch] $NonInteractive, [string] $Shell = $pwsh,
        [string] $Folder = $release, [string[]] $Switches = @('FILES', 'UNSIGNED', 'ALLOW_ADMIN'), [hashtable] $Environment = @{},
        [switch] $Uninstall) {
        $script = Join-Path $Folder 'install.ps1'
        $common = "-NoSpeech -ReloadedConfig $(ConvertTo-Literal $config) -ReloadedFolder $(ConvertTo-Literal $reloadedFolder) " +
            "-SteamRoot $(ConvertTo-Literal $steam) -Desktop $(ConvertTo-Literal $desktop) -DotnetRoot $(ConvertTo-Literal $dotnet) " +
            "-VcRuntimeKey $(ConvertTo-Literal $vcKey) -Pins $(ConvertTo-Literal $pins)"
        if ($Uninstall) { $common += ' -Uninstall' }
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

    It 'ends every question it asks with a sentence that says what to type, never [y/N] (#473)' {
        $result = Invoke-Install $freshAnswers
        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Not -Match '\[[yY]/[nN]\]|skip it'
        $enterIsNo = [regex]::Escape('Type yes and press Enter to go on, or press Enter alone to stop.')
        $enterIsYes = [regex]::Escape('Press Enter alone to go on, or type no and press Enter to stop.')
        $result.Output | Should -Match "Install the mod\?.* $enterIsNo"
        $result.Output | Should -Match "Use it\? $enterIsYes"
        # One sentence per answer given: the install, the game (Enter means yes), then four downloads.
        ([regex]::Matches($result.Output, $enterIsNo)).Count | Should -Be 5
        ([regex]::Matches($result.Output, $enterIsYes)).Count | Should -Be 1
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
        $result.Output | Should -Match 'Open install.cmd in the mod''s folder from the release zip'
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

    It 'names Controlled folder access when Windows blocks the game''s settings folder from being made' {
        New-ReloadedSetup
        $apps = Join-Path $reloadedFolder 'Apps'
        Remove-Item -LiteralPath (Split-Path $appConfig) -Recurse -Force
        Block-Write $apps
        try { $result = Invoke-Install @('y', '') }
        finally { Unblock-Write $apps }
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Failed: Windows did not let the installer save this step''s files\. Windows Security''s Controlled folder access may have blocked it\.'
        $result.Output | Should -Not -Match 'This step stopped with an error'
        $result.Output | Should -Match 'The installer stopped at step 8'
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

# The launchers a player opens with Enter in File Explorer: install.cmd (#574), and uninstall.cmd (#575), which is the
# same but passes -Uninstall first. A stand-in install.ps1 records how it was started, so these tests run nothing of the
# real installer.
Describe '<Name>' -ForEach @(
    @{ Name = 'install.cmd'; Passed = @() }
    @{ Name = 'uninstall.cmd'; Passed = @('-Uninstall') }
) {
    BeforeAll {
        $script:launcher = Join-Path $repo "scripts/player/$Name"
        # Explorer opens a .cmd as "%1" %* (HKCR\cmdfile\shell\open\command) from the file's folder; CreateProcess on the
        # file is the same start. Standard input is closed, so the pause at the end returns at once.
        function Invoke-Launcher([string] $Folder, [string[]] $Arguments = @()) {
            $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $Folder $Name))
            foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
            $start.Environment['METAPHOR_INSTALL_TEST_ALLOW_ADMIN'] = '1' # honoured on CI only, whose runners are admin
            $start.WorkingDirectory = $Folder
            $start.UseShellExecute = $false
            $start.RedirectStandardInput = $true
            $start.RedirectStandardOutput = $true
            $process = [Diagnostics.Process]::Start($start)
            $process.StandardInput.Close()
            $output = $process.StandardOutput.ReadToEnd()
            if (-not $process.WaitForExit(120000)) { $process.Kill(); throw 'install.cmd did not finish' }
            [pscustomobject]@{ Output = $output; ExitCode = $process.ExitCode }
        }
        # A folder whose path has spaces, an ampersand and non-ASCII letters, holding the launcher and a stand-in
        # install.ps1 that writes what it saw to ran.json and exits 3. Both carry the downloaded-file mark, as after
        # unzipping a downloaded zip in File Explorer.
        function New-LauncherFolder {
            $name = "Mod folder & co $([char]0x00E9)$([char]0x00FC) $([char]0x65E5)$([char]0x672C)"
            $folder = New-Item -ItemType Directory -Force (Join-Path $TestDrive "$(New-Guid)/$name")
            Copy-Item -LiteralPath $launcher -Destination $folder
            Set-Content -LiteralPath (Join-Path $folder 'install.ps1') -Encoding ascii -Value @'
@{
    Root = $PSScriptRoot
    Edition = $PSVersionTable.PSEdition
    Policy = "$(Get-ExecutionPolicy -Scope Process)"
    Arguments = @($args)
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'ran.json') -Encoding UTF8
Write-Host 'stand-in installer ran'
exit 3
'@
            foreach ($file in $Name, 'install.ps1') {
                Set-Content -LiteralPath (Join-Path $folder $file) -Stream Zone.Identifier -Value "[ZoneTransfer]`r`nZoneId=3"
            }
            $folder.FullName
        }
    }

    # Started from PowerShell 7 here, as a player may: 5.1 then failed to load its own modules (#574).
    It 'runs install.ps1 from its own folder in Windows PowerShell 5.1, the policy bypassed for that process only' -Skip:(
        -not (Get-Command powershell.exe -ErrorAction SilentlyContinue)) {
        $folder = New-LauncherFolder
        $result = Invoke-Launcher $folder -Arguments '-Speak'
        $ran = Get-Content -Raw -Encoding utf8 (Join-Path $folder 'ran.json') | ConvertFrom-Json
        $ran.Root | Should -Be $folder
        $ran.Edition | Should -Be 'Desktop'
        $ran.Policy | Should -Be 'Bypass'
        @($ran.Arguments) | Should -Be @($Passed + '-Speak')
        $result.ExitCode | Should -Be 3
    }

    It 'keeps the window open after the installer, saying how to close it' -Skip:(
        -not (Get-Command powershell.exe -ErrorAction SilentlyContinue)) {
        $result = Invoke-Launcher (New-LauncherFolder)
        $result.Output | Should -Match '(?s)stand-in installer ran.*press any key to close this window'
    }

    It 'starts Windows PowerShell from System32, not a powershell file in its own folder' -Skip:(
        -not (Get-Command powershell.exe -ErrorAction SilentlyContinue)) {
        $folder = New-LauncherFolder
        foreach ($planted in 'powershell.bat', 'powershell.cmd') {
            Set-Content -LiteralPath (Join-Path $folder $planted) -Value "@echo planted`r`n@exit /b 9" -Encoding ascii
        }
        $result = Invoke-Launcher $folder
        $result.Output | Should -Not -Match 'planted'
        $result.ExitCode | Should -Be 3
    }

    # The real installer, with its own defaults, in a folder without the mod: it gets as far as saying so.
    It 'starts the real installer with its default pins file in Windows PowerShell 5.1' -Skip:(
        -not (Get-Command powershell.exe -ErrorAction SilentlyContinue)) {
        $folder = New-LauncherFolder
        foreach ($file in $shipped) { Copy-Item -LiteralPath (Join-Path $repo "scripts/player/$file") $folder -Force }
        $result = Invoke-Launcher $folder -Arguments '-NoSpeech'
        $result.Output | Should -Match 'not in the mod''s folder'
        $result.ExitCode | Should -Be 1
    }

    It 'ships in the package, ASCII only, with Windows line endings' {
        $project = Get-Content -Raw -LiteralPath (Join-Path $repo 'src/Metaphor.Hooks/Metaphor.Hooks.csproj')
        $project | Should -Match ([regex]::Escape("../../scripts/player/$Name"))
        $bytes = [IO.File]::ReadAllBytes($launcher)
        $bytes | Where-Object { $_ -gt 127 } | Should -BeNullOrEmpty
        $text = [Text.Encoding]::ASCII.GetString($bytes)
        ($text -replace "`r`n", '') | Should -Not -Match "[`r`n]"
    }
}

# The uninstall (#575): what it removes, what it keeps or asks about, and what it refuses to touch.
Describe 'install.ps1 -Uninstall' {
    BeforeAll {
        # The mod installed into a Reloaded-II the player set up before, with the mod's own settings saved by Reloaded-II.
        function Install-Mod {
            New-ReloadedSetup
            $result = Invoke-Install @('y')
            if ($result.ExitCode -ne 0) { throw "the install failed: $($result.Output)" }
            $script:installed = Join-Path $mods $modId
            $script:userConfig = Join-Path $reloadedFolder "User/Mods/$modId"
            Write-JsonFile (Join-Path $userConfig 'Config.json') @{ ReadVoicedLines = $true }
            $script:shortcut = Join-Path $desktop 'Metaphor ReFantazio with accessibility.lnk'
        }
        function New-Link([string] $Path, [string] $Target) {
            New-Item -ItemType Directory -Force $Target | Out-Null
            New-Item -ItemType Junction -Path $Path -Target $Target | Out-Null
        }
        $script:uninstallQuestion = 'Uninstall the mod\?'
        $script:reloadedQuestion = 'Remove Reloaded-II too\?'
    }
    BeforeEach {
        $script:root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        New-Machine
    }
    AfterEach { Remove-Item -LiteralPath "HKCU:\$vcKeyPath" -Recurse -Force -ErrorAction SilentlyContinue }

    It 'removes the mod''s folder, its settings, the shortcut and its enabled entry, and keeps everything else' {
        Install-Mod
        $result = Invoke-Install @('y') -Uninstall
        $result.ExitCode | Should -Be 0 -Because $result.Output
        $installed | Should -Not -Exist
        $userConfig | Should -Not -Exist
        $shortcut | Should -Not -Exist
        $app = Get-Content -Raw $appConfig | ConvertFrom-Json
        @($app.EnabledMods) | Should -Be @('other.mod')
        $app.AppName | Should -Be 'My Metaphor'
        $launcher | Should -Exist
        Join-Path $mods 'other.mod/ModConfig.json' | Should -Exist
        Join-Path $mods 'reloaded.sharedlib.hooks/ModConfig.json' | Should -Exist
        $config | Should -Exist
        Join-Path $dotnet 'shared/Microsoft.WindowsDesktop.App/9.0.21' | Should -Exist
        $result.Output | Should -Match 'Done: The mod is turned off for the game'
        $result.Output | Should -Match 'Done: Removed the mod''s folder from Reloaded-II\.'
        $result.Output | Should -Match 'Done: Removed the mod''s settings from Reloaded-II\.'
        $result.Output | Should -Match 'Done: Removed the desktop shortcut'
        $result.Output | Should -Match 'Nothing to remove: Kept Reloaded-II, which this installer did not install'
        $result.Output | Should -Match 'Kept the \.NET 9 Desktop Runtime and the Visual C\+\+ Redistributable'
        $result.Output | Should -Match 'The uninstall is done\.'
        $result.Output | Should -Not -Match $reloadedQuestion
    }

    It 'says each step, and asks before it starts in the plain words the install uses (#473)' {
        Install-Mod
        $result = Invoke-Install @('y') -Uninstall
        foreach ($n in 1..5) { $result.Output | Should -Match "Step $n of 5: " }
        $enterIsNo = [regex]::Escape('Type yes and press Enter to go on, or press Enter alone to stop.')
        $result.Output | Should -Match "$uninstallQuestion.* $enterIsNo"
        $result.Output | Should -Not -Match '\[[yY]/[nN]\]'
    }

    It 'says there is nothing left to remove on a second run, and changes nothing' {
        Install-Mod
        (Invoke-Install @('y') -Uninstall).ExitCode | Should -Be 0
        $before = Get-Snapshot
        $result = Invoke-Install @('y') -Uninstall
        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'There was nothing left to remove\.'
        $result.Output | Should -Not -Match '(?m)^Done: '
        Get-Snapshot | Should -Be $before
    }

    It 'changes nothing when the answer to going ahead is no, or there is no answer' {
        Install-Mod
        $before = Get-Snapshot
        $result = Invoke-Install @('') -Uninstall
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Nothing was changed, because the answer was no\.'
        (Invoke-Install -NonInteractive -Uninstall).ExitCode | Should -Be 1
        Get-Snapshot | Should -Be $before
    }

    It 'says there is nothing to remove on a computer where the mod was never installed' {
        $result = Invoke-Install @('y') -Uninstall
        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Match 'There was nothing left to remove\.'
        $reloadedFolder | Should -Not -Exist
    }

    It 'keeps the Reloaded-II it installed when the answer is Enter alone, and removes it on yes' {
        (Invoke-Install $freshAnswers).ExitCode | Should -Be 0
        $result = Invoke-Install @('y', '') -Uninstall
        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Match "$reloadedQuestion.*every other mod"
        $result.Output | Should -Match 'Kept Reloaded-II, because the answer was no\.'
        Join-Path $reloadedFolder 'Reloaded-II.exe' | Should -Exist
        Join-Path $reloadedFolder "Mods/$modId" | Should -Not -Exist

        $result = Invoke-Install @('y', 'y') -Uninstall
        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Match 'Done: Removed Reloaded-II\.'
        $reloadedFolder | Should -Not -Exist
        Join-Path $dotnet 'shared/Microsoft.WindowsDesktop.App/9.0.23' | Should -Exist

        # Reloaded-II's settings file stays, naming the launcher that is gone with its folder.
        $result = Invoke-Install @('y') -Uninstall
        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Match 'There was nothing left to remove\.'
    }

    It 'refuses settings whose launcher is missing, so a folder it names is never taken as Reloaded-II''s' {
        Install-Mod
        # As a hand edit could: a Reloaded-II.exe that isn't there, in a folder holding much more than Reloaded-II.
        $loader = Get-Content -Raw $config | ConvertFrom-Json
        $loader.LauncherPath = Join-Path $root 'Reloaded-II.exe'
        Write-JsonFile $config $loader
        $result = Invoke-Install @('y') -Uninstall
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Failed: Reloaded-II''s settings do not name its program and folders as the uninstall expects'
        Join-Path $installed 'ModConfig.json' | Should -Exist
        $userConfig | Should -Exist
    }

    It 'removes its own installed copy when run from it' {
        Install-Mod
        $result = Invoke-Install @('y') -Uninstall -Folder $installed
        $result.ExitCode | Should -Be 0 -Because $result.Output
        $installed | Should -Not -Exist
    }

    It 'refuses a mod folder that holds a link, and leaves what it points to alone' {
        Install-Mod
        $outside = Join-Path $root 'Precious'
        Set-Content -LiteralPath (Join-Path (New-Item -ItemType Directory -Force $outside) 'keep.txt') -Value 'keep'
        New-Link (Join-Path $installed 'linked') $outside
        $result = Invoke-Install @('y') -Uninstall
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Failed: The mod''s folder was left alone'
        $result.Output | Should -Match 'The uninstall did not remove everything'
        Join-Path $outside 'keep.txt' | Should -Exist
        Join-Path $installed 'ModConfig.json' | Should -Exist
        # The other steps still ran.
        $shortcut | Should -Not -Exist
        $userConfig | Should -Not -Exist
    }

    It 'refuses a mods folder outside Reloaded-II''s folder' {
        Install-Mod
        $elsewhere = Join-Path $root 'ElsewhereMods'
        Move-Item -LiteralPath $mods $elsewhere
        $loader = Get-Content -Raw $config | ConvertFrom-Json
        $loader.ModConfigDirectory = $elsewhere
        Write-JsonFile $config $loader
        $result = Invoke-Install @('y') -Uninstall
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Failed: The mod''s folder was left alone'
        Join-Path $elsewhere "$modId/ModConfig.json" | Should -Exist
    }

    It 'refuses a settings folder that is a link' {
        Install-Mod
        $outside = Join-Path $root 'OtherSettings'
        Remove-Item -LiteralPath $userConfig -Recurse -Force
        New-Link $userConfig $outside
        Set-Content -LiteralPath (Join-Path $outside 'keep.txt') -Value 'keep'
        $result = Invoke-Install @('y') -Uninstall
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Failed: The mod''s settings were left alone'
        Join-Path $outside 'keep.txt' | Should -Exist
    }

    It 'leaves a desktop shortcut of that name that starts another program' {
        Install-Mod
        $shell = New-Object -ComObject WScript.Shell
        try { $link = $shell.CreateShortcut($shortcut); $link.TargetPath = Join-Path $root 'notepad.exe'; $link.Save() }
        finally { [void] [Runtime.InteropServices.Marshal]::ReleaseComObject($shell) }
        $result = Invoke-Install @('y') -Uninstall
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Failed: A desktop shortcut with the mod''s name starts another program, so it was left alone\.'
        $shortcut | Should -Exist
    }

    It 'refuses Reloaded-II''s folder when it holds a link, and keeps the question for the next run' {
        (Invoke-Install $freshAnswers).ExitCode | Should -Be 0
        $outside = Join-Path $root 'Precious'
        New-Link (Join-Path $reloadedFolder 'Mods/linked') $outside
        Set-Content -LiteralPath (Join-Path $outside 'keep.txt') -Value 'keep'
        $result = Invoke-Install @('y', 'y') -Uninstall
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Failed: Reloaded-II was left alone'
        Join-Path $outside 'keep.txt' | Should -Exist
        Join-Path $reloadedFolder 'Reloaded-II.exe' | Should -Exist
    }

    It 'runs in Windows PowerShell 5.1' -Skip:(-not (Get-Command powershell.exe -ErrorAction SilentlyContinue)) {
        Install-Mod
        $result = Invoke-Install @('y') -Uninstall -Shell 'powershell.exe'
        $result.ExitCode | Should -Be 0 -Because $result.Output
        $installed | Should -Not -Exist
        @((Get-Content -Raw $appConfig | ConvertFrom-Json).EnabledMods) | Should -Be @('other.mod')
    }
}

Describe 'Get-RemovalProblem' {
    BeforeEach { $script:base = (New-Item -ItemType Directory (Join-Path $TestDrive "base $(New-Guid) $([char] 0xE9)")).FullName }

    It 'allows a folder inside the root, with hidden files and a different case' {
        $folder = New-Item -ItemType Directory (Join-Path $base 'Mods/mod')
        $hidden = New-Item -ItemType File (Join-Path $folder 'hidden.txt')
        $hidden.Attributes = 'Hidden'
        InModuleScope MetaphorInstall -Parameters @{ P = $folder.FullName.ToUpperInvariant(); R = $base } { param($P, $R) Get-RemovalProblem $P $R } |
            Should -BeNullOrEmpty
    }

    It 'refuses the root itself, unless allowed, a path outside it, and a drive''s top folder' {
        $outside = (New-Item -ItemType Directory (Join-Path $TestDrive "outside $(New-Guid)")).FullName
        InModuleScope MetaphorInstall -Parameters @{ R = $base; O = $outside } {
            param($R, $O)
            Get-RemovalProblem $R $R | Should -Match 'is not inside'
            Get-RemovalProblem $R $R -AllowRoot | Should -BeNullOrEmpty
            Get-RemovalProblem $O $R | Should -Match 'is not inside'
            Get-RemovalProblem (Join-Path $R '..') $R | Should -Match 'is not inside'
            $top = [IO.Path]::GetPathRoot($R)
            Get-RemovalProblem (Join-Path $top 'x') $top | Should -Match 'top folder'
        }
    }

    It 'refuses a link on the way down from the root, or inside the folder' {
        $target = New-Item -ItemType Directory (Join-Path $TestDrive "target $(New-Guid)")
        New-Item -ItemType Directory (Join-Path $target 'mod') | Out-Null
        New-Item -ItemType Junction -Path (Join-Path $base 'Mods') -Target $target.FullName | Out-Null
        $inner = New-Item -ItemType Directory (Join-Path $base 'Other/mod/deep')
        New-Item -ItemType Junction -Path (Join-Path $inner 'link') -Target $target.FullName | Out-Null
        InModuleScope MetaphorInstall -Parameters @{ R = $base } {
            param($R)
            Get-RemovalProblem (Join-Path $R 'Mods/mod') $R | Should -Match 'is a link'
            Get-RemovalProblem (Join-Path $R 'Other/mod') $R | Should -Match 'is a link'
        }
    }
}

Describe 'Test-FullyQualifiedPath' {
    It 'takes <Path> as <Expected>' -ForEach @(
        @{ Path = 'C:\Reloaded-II\Reloaded-II.exe'; Expected = $true }
        @{ Path = 'C:/Reloaded-II/Reloaded-II.exe'; Expected = $true }
        @{ Path = '\\host\share\Reloaded-II.exe'; Expected = $true }
        @{ Path = 'C:Reloaded-II.exe'; Expected = $false }
        @{ Path = '\Reloaded-II\Reloaded-II.exe'; Expected = $false }
        @{ Path = 'Reloaded-II\Reloaded-II.exe'; Expected = $false }
        @{ Path = ''; Expected = $false }
    ) {
        InModuleScope MetaphorInstall -Parameters @{ P = $Path } { param($P) Test-FullyQualifiedPath $P } | Should -Be $Expected
    }
}

Describe 'Read-UninstallLoader' {
    # A drive-relative launcher path resolves against the current folder; here that folder holds a Reloaded-II.exe and
    # mods, as the uninstall's own current folder (the temp folder) could.
    It 'refuses a launcher path that is <Name>' -ForEach @(
        @{ Name = 'relative to the drive''s current folder'; Form = { param($Folder) "$($Folder.Substring(0, 2))Reloaded-II.exe" } }
        @{ Name = 'relative to the drive''s top folder'; Form = { param($Folder) $Folder.Substring(2) + '\Reloaded-II.exe' } }
    ) {
        $folder = (New-Item -ItemType Directory (Join-Path $TestDrive "cwd $(New-Guid)")).FullName
        Set-Content -LiteralPath (Join-Path $folder 'Reloaded-II.exe') -Value 'stand-in launcher'
        $configPath = Join-Path $TestDrive "ReloadedII-$(New-Guid).json"
        @{ LauncherPath = (& $Form $folder); ModConfigDirectory = (Join-Path $folder 'Mods')
            ApplicationConfigDirectory = (Join-Path $folder 'Apps') } | ConvertTo-Json | Set-Content -LiteralPath $configPath
        $context = [pscustomobject]@{ ConfigPath = $configPath; Loader = $null; ReloadedRoot = $null; ModsFolder = $null
            AppsFolder = $null; UserConfigFolder = $null }
        $saved = [Environment]::CurrentDirectory
        Push-Location -LiteralPath $folder
        [Environment]::CurrentDirectory = $folder
        try { $step = InModuleScope MetaphorInstall -Parameters @{ C = $context } { param($C) Read-UninstallLoader $C } }
        finally { Pop-Location; [Environment]::CurrentDirectory = $saved }
        $step.State | Should -Be 'failed'
        $context.ReloadedRoot | Should -BeNullOrEmpty
    }
}

Describe 'Get-UninstallStep' {
    It 'gives the five steps in order, and keeps the step functions private' {
        $steps = @(Get-UninstallStep ([pscustomobject]@{}))
        $steps.Doing | Should -Be @('Turning the mod off for the game.', 'Removing the mod''s folder from Reloaded-II.',
            'Removing the mod''s settings from Reloaded-II.', 'Removing the desktop shortcut.',
            'Checking whether this installer installed Reloaded-II.')
        $exported = (Get-Module MetaphorInstall).ExportedFunctions.Keys
        foreach ($name in 'Disable-Mod', 'Remove-ModFiles', 'Remove-Shortcut', 'Remove-Reloaded', 'Get-RemovalProblem') { $exported | Should -Not -Contain $name }
    }
}

# A refused folder, given as New-Item gives it: a non-terminating access-denied error. Not a deny ACL, as the
# install.ps1 tests use for files: on CI's elevated runner a deny on adding a subfolder doesn't stop New-Item (#466).
Describe 'a refused folder' {
    BeforeEach {
        # A mock doesn't take the caller's -ErrorAction, so it acts it out: stop with it, a printed error without it.
        Mock -ModuleName MetaphorInstall New-Item {
            $denied = [UnauthorizedAccessException]::new("Access to the path '$Path' is denied.")
            if ($PesterBoundParameters['ErrorAction'] -eq 'Stop') { throw $denied }
            Write-Error -Exception $denied -ErrorAction Continue 2>$null
        } -ParameterFilter { $ItemType -eq 'Directory' }
    }

    It 'stops the game''s settings with the Controlled folder access message, not a missing folder' {
        $path = Join-Path $TestDrive 'Apps/metaphor.exe/AppConfig.json'
        $step = InModuleScope MetaphorInstall -Parameters @{ Path = $path } {
            param($Path)
            try { Write-JsonFile $Path @{ AppId = 'metaphor.exe' }; 'written' } catch { Get-ErrorStep $_ }
        }
        $step.Line | Should -Be 'Windows did not let the installer save this step''s files. Windows Security''s Controlled folder access may have blocked it.'
    }

    It 'stops Reloaded-II''s settings with the Controlled folder access message, not as done' {
        $configPath = Join-Path $TestDrive 'ReloadedII.json'
        Write-JsonFile $configPath @{ LauncherPath = 'Reloaded-II.exe'; ModConfigDirectory = (Join-Path $TestDrive 'Mods')
            ApplicationConfigDirectory = (Join-Path $TestDrive 'Apps') }
        $context = [pscustomobject]@{ ConfigPath = $configPath; Launcher = $null; Loader = $null; ModsFolder = $null; AppsFolder = $null }
        $step = InModuleScope MetaphorInstall -Parameters @{ Context = $context } {
            param($Context)
            try { Initialize-ReloadedConfig $Context } catch { Get-ErrorStep $_ }
        }
        $step.State | Should -Be 'failed'
        $step.Line | Should -Be 'Windows did not let the installer save this step''s files. Windows Security''s Controlled folder access may have blocked it.'
    }
}

Describe 'Get-InstallStep' {
    It 'gives the nine steps in order, only the shortcut optional, and keeps the step functions private' {
        $steps = @(Get-InstallStep ([pscustomobject]@{}))
        $steps.Doing | Should -Be @('Looking for the game.', 'Checking for Reloaded-II, which loads the mod into the game.',
            'Checking Reloaded-II''s settings.', 'Checking for the .NET 9 Desktop Runtime.',
            'Checking for the Visual C++ Redistributable.', 'Checking for Reloaded-II''s hooks library.',
            'Copying the mod into Reloaded-II.', 'Turning the mod on for the game.', 'Making the desktop shortcut.')
        $steps.Optional | Should -Be @($false, $false, $false, $false, $false, $false, $false, $false, $true)
        $exported = (Get-Module MetaphorInstall).ExportedFunctions.Keys
        foreach ($name in 'Select-InstallGame', 'Install-Shortcut', 'Get-ErrorStep', 'Test-Elevated') { $exported | Should -Not -Contain $name }
    }

    It 'turns an error a step stops with into a failed step' {
        $context = [pscustomobject]@{ Desktop = (Join-Path $TestDrive 'desktop'); Launcher = (Join-Path $TestDrive 'notepad.exe'); GameExe = 'METAPHOR.exe' }
        $step = & (@(Get-InstallStep $context)[8]).Run
        $step.State | Should -Be 'failed'
        $step.Line | Should -Be 'This step stopped with an error.'
        $step.Detail | Should -Match 'not Reloaded-II\.exe'
        Test-Path (Join-Path $TestDrive 'desktop') | Should -BeFalse
    }

    It 'gives only the failed step when a step returns a result and then stops with an error' {
        $steps = @(InModuleScope MetaphorInstall {
                $step = New-InstallStep 'Testing.' { Done 'This part worked.'; throw 'then it broke' } $null
                & $step.Run
            })
        $steps.Count | Should -Be 1
        $steps[0].State | Should -Be 'failed'
        $steps[0].Line | Should -Be 'This step stopped with an error.'
        $steps[0].Detail | Should -Match 'then it broke'
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
        InModuleScope MetaphorInstall { Test-Elevated } | Should -BeFalse
    }

    It 'still refuses administrator off CI, even with the test switch set' {
        $env:CI = $null
        InModuleScope MetaphorInstall { Test-Elevated } | Should -BeTrue
        $env:CI = '1'
        InModuleScope MetaphorInstall { Test-Elevated } | Should -BeTrue
    }
}

Describe 'Format-YesNoQuestion' {
    It 'ends the question with what to type, Enter alone meaning no' {
        Format-YesNoQuestion 'Go?' | Should -BeExactly 'Go? Type yes and press Enter to go on, or press Enter alone to stop.'
    }

    It 'says Enter alone means yes with -EnterMeansYes' {
        Format-YesNoQuestion 'Go?' -EnterMeansYes |
            Should -BeExactly 'Go? Press Enter alone to go on, or type no and press Enter to stop.'
    }

    It 'says skip it instead of stop with -Ending ''skip it''' {
        Format-YesNoQuestion 'Go?' -Ending 'skip it' |
            Should -BeExactly 'Go? Type yes and press Enter to go on, or press Enter alone to skip it.'
        Format-YesNoQuestion 'Go?' -EnterMeansYes -Ending 'skip it' |
            Should -BeExactly 'Go? Press Enter alone to go on, or type no and press Enter to skip it.'
    }

    It 'takes y and yes as yes, and Enter alone, no, or no answer as no' {
        foreach ($yes in 'y', 'yes', ' YES ') { Test-Yes $yes | Should -BeTrue -Because "'$yes'" }
        foreach ($no in '', 'n', 'no', 'yep', $null) { Test-Yes $no | Should -BeFalse -Because "'$no'" }
    }
}

Describe 'Confirm-RunWindow' {
    BeforeAll {
        $script:said = [Collections.Generic.List[string]]::new()
        $script:asked = [Collections.Generic.List[string]]::new()
        $script:say = { param($Printed, $Spoken) $said.Add($Printed) }
        Mock -ModuleName MetaphorInstall Test-Elevated { $true }

        # Runs Confirm-RunWindow with ANSWER as the reply to its question; $null is no console to answer on.
        function Invoke-Confirm($Answer) {
            $script:answer = $Answer
            Confirm-RunWindow -Say $say -Ask { param($Question) $asked.Add($Question); $script:answer }
        }
    }
    BeforeEach { $said.Clear(); $asked.Clear() }

    It 'goes on without asking in a normal window' {
        Mock -ModuleName MetaphorInstall Test-Elevated { $false }
        Invoke-Confirm 'n' | Should -BeTrue
        $asked.Count | Should -Be 0
    }

    It 'refuses an administrator window without asking while UAC is on' {
        Mock -ModuleName MetaphorInstall Test-UacEnabled { $true }
        Invoke-Confirm 'y' | Should -BeFalse
        $asked.Count | Should -Be 0
        $said -join ' ' | Should -Match 'runs as administrator'
    }

    It 'goes on after a yes while UAC is off, having said who will own the files' {
        Mock -ModuleName MetaphorInstall Test-UacEnabled { $false }
        Invoke-Confirm 'y' | Should -BeTrue
        $asked.Count | Should -Be 1
        $said -join ' ' | Should -Match 'User Account Control is turned off.*owned by the administrator account'
        $said -join ' ' | Should -Match 'administrator rights to update themselves later'
    }

    It 'asks for the word yes in a sentence a screen reader can read, and takes it typed in full' {
        Mock -ModuleName MetaphorInstall Test-UacEnabled { $false }
        Invoke-Confirm 'yes' | Should -BeTrue
        $asked[0] | Should -Be (Format-YesNoQuestion 'Install as administrator anyway?')
        $asked[0] | Should -Not -Match '\[y/N\]'
    }

    It 'refuses after a no while UAC is off' {
        Mock -ModuleName MetaphorInstall Test-UacEnabled { $false }
        Invoke-Confirm 'n' | Should -BeFalse
        Invoke-Confirm '' | Should -BeFalse
    }

    It 'refuses without a console to answer on while UAC is off' {
        Mock -ModuleName MetaphorInstall Test-UacEnabled { $false }
        Invoke-Confirm $null | Should -BeFalse
        $asked.Count | Should -Be 1
    }
}

Describe 'Test-UacEnabled' {
    It 'counts UAC as on when EnableLUA is 1' {
        Mock -ModuleName MetaphorInstall Get-ItemProperty { [pscustomobject] @{ EnableLUA = 1 } }
        InModuleScope MetaphorInstall { Test-UacEnabled } | Should -BeTrue
    }

    It 'counts UAC as off when EnableLUA is 0' {
        Mock -ModuleName MetaphorInstall Get-ItemProperty { [pscustomobject] @{ EnableLUA = 0 } }
        InModuleScope MetaphorInstall { Test-UacEnabled } | Should -BeFalse
    }

    It 'counts UAC as on when EnableLUA is 0 but not a DWORD' {
        Mock -ModuleName MetaphorInstall Get-ItemProperty { [pscustomobject] @{ EnableLUA = '0' } }
        InModuleScope MetaphorInstall { Test-UacEnabled } | Should -BeTrue
        Mock -ModuleName MetaphorInstall Get-ItemProperty { [pscustomobject] @{ EnableLUA = [long] 0 } }
        InModuleScope MetaphorInstall { Test-UacEnabled } | Should -BeTrue
    }

    It 'counts UAC as on when EnableLUA is missing' {
        Mock -ModuleName MetaphorInstall Get-ItemProperty { $null }
        InModuleScope MetaphorInstall { Test-UacEnabled } | Should -BeTrue
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

    # #458: a proxy or captive portal that accepts the connection and then goes quiet. A loopback listener stands in for
    # it: Windows accepts connections into its backlog, and nothing ever answers them.
    It 'gives up on a download that never answers, says what to do, and keeps nothing' {
        $work = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
        $listener.Start()
        try {
            $pin = [pscustomobject]@{ name = 'Reloaded-II'; fileName = 'file.zip'; sha256 = '0' * 64
                url = "https://127.0.0.1:$($listener.LocalEndpoint.Port)/file.zip" }
            $clock = [Diagnostics.Stopwatch]::StartNew()
            $result = InModuleScope MetaphorInstall -Parameters @{ Pin = $pin; Work = $work } {
                $saved = $script:DownloadTimeout
                $script:DownloadTimeout = @{ WaitSeconds = 2; TotalSeconds = 60 }
                try { Get-PinnedFile ([pscustomobject]@{ WorkFolder = $Work }) $Pin } finally { $script:DownloadTimeout = $saved }
            }
            $clock.Stop()
        }
        finally { $listener.Stop() }
        $clock.Elapsed.TotalSeconds | Should -BeLessThan 30
        $result.Path | Should -BeNullOrEmpty
        $result.Step.State | Should -Be 'failed'
        $result.Step.Line | Should -Be 'The download of Reloaded-II did not finish.'
        $result.Step.Fix | Should -Be ('The internet connection, or a proxy or firewall, probably stopped it. Check that this computer is ' +
            'online, then run the installer again. If it fails again, report it with the alpha feedback form.')
        @(Get-ChildItem -LiteralPath $work -Force) | Should -BeNullOrEmpty
    }
}

Describe 'Save-Download' {
    BeforeAll {
        # A download source: a file URL, which the same request code reads, so no network is needed.
        $script:source = Join-Path $TestDrive 'source.bin'
        [IO.File]::WriteAllBytes($source, [byte[]]::new(300000))
    }

    It 'keeps no partial file when the download fails part way' {
        $work = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $target = Join-Path $work 'file.zip'
        # A total time already used up after the first block of data.
        { InModuleScope MetaphorInstall -Parameters @{ Uri = [Uri] $source; Target = $target } {
                $saved = $script:DownloadTimeout
                $script:DownloadTimeout = @{ WaitSeconds = 2; TotalSeconds = -1 }
                try { Save-Download $Uri $Target } finally { $script:DownloadTimeout = $saved }
            } } | Should -Throw '*took longer than -1 seconds*'
        @(Get-ChildItem -LiteralPath $work -Force) | Should -BeNullOrEmpty
    }

    It 'saves a whole download under its name' {
        $work = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $target = Join-Path $work 'file.zip'
        InModuleScope MetaphorInstall -Parameters @{ Uri = [Uri] $source; Target = $target } { Save-Download $Uri $Target }
        (Get-Item -LiteralPath $target).Length | Should -Be 300000
        @(Get-ChildItem -LiteralPath $work -Force).Count | Should -Be 1
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
