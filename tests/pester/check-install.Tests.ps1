# Tests for scripts/player/check-install.ps1 and the check in scripts/player/MetaphorInstall.psm1 (#283), against
# stand-ins in the test folder: a release folder laid out as the package is, a Reloaded-II config with its apps and
# mods folders, a game exe, and a screen-reader probe that finds none. No build, no network, no speech.
# Run: Invoke-Pester tests/pester (Pester 5+).

BeforeAll {
    $script:repo = (Resolve-Path "$PSScriptRoot/../..").Path
    Import-Module (Join-Path $repo 'scripts/player/MetaphorInstall.psm1') -Force
    # The files the package ships for the check, beside the mod's own (src/Metaphor.Hooks/Metaphor.Hooks.csproj).
    $script:shipped = 'check-install.ps1', 'MetaphorInstall.psm1'
    $script:modId = 'metaphor.accessibility'

    function Get-Sha256([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

    function Write-JsonFile([string] $Path, $Value) {
        New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
        $Value | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Path
    }

    # A release folder at PATH: the check's scripts, ModConfig.json, the known builds (naming the stand-in game), the
    # Prism pin and a prism.dll that matches it.
    function New-Release([string] $Path) {
        New-Item -ItemType Directory -Force $Path | Out-Null
        foreach ($file in $shipped) { Copy-Item -LiteralPath (Join-Path $repo "scripts/player/$file") $Path }
        Write-JsonFile (Join-Path $Path 'ModConfig.json') @{ ModId = $modId; ModDll = 'Metaphor.Hooks.dll' }
        Set-Content -LiteralPath (Join-Path $Path 'prism.dll') -Value 'stand-in prism' -NoNewline
        Write-JsonFile (Join-Path $Path 'prism.json') @{ version = 'v0'; dllSha256 = (Get-Sha256 (Join-Path $Path 'prism.dll')) }
        Write-JsonFile (Join-Path $Path 'known-game-builds.json') @(
            @{ sha256 = (Get-Sha256 $game); steamBuildId = 18330018; evidence = 'stand-in' })
    }

    # A working install under ROOT: Reloaded-II's config, the game added with the mod enabled, the mod in the mods
    # folder, and a status file from a run where it loaded.
    function New-Install {
        $script:game = Join-Path $root 'Game/METAPHOR.exe'
        New-Item -ItemType Directory -Force (Split-Path $game) | Out-Null
        Set-Content -LiteralPath $game -Value 'stand-in game' -NoNewline
        $script:launcher = Join-Path $root 'Reloaded/Reloaded-II.exe'
        New-Item -ItemType Directory -Force (Split-Path $launcher) | Out-Null
        Set-Content -LiteralPath $launcher -Value 'stand-in launcher'
        $script:mods = (New-Item -ItemType Directory -Force (Join-Path $root 'Reloaded/Mods')).FullName
        $apps = (New-Item -ItemType Directory -Force (Join-Path $root 'Reloaded/Apps')).FullName
        $script:config = Join-Path $root 'AppData/ReloadedII.json'
        Write-JsonFile $config @{ LauncherPath = $launcher; ApplicationConfigDirectory = $apps; ModConfigDirectory = $mods }
        $script:appConfig = Join-Path $apps 'metaphor.exe/AppConfig.json'
        Write-JsonFile $appConfig @{ AppId = 'metaphor.exe'; AppLocation = $game; EnabledMods = @('other.mod', $modId) }
        $script:release = Join-Path $root "Downloads/$modId"
        New-Release $release
        $script:installed = Join-Path $mods $modId
        Copy-Item -LiteralPath $release $installed -Recurse
        Write-Status @{ modId = $modId; state = 'ready'; gameBuildSha256 = (Get-Sha256 $game); hooksLoaded = 9
            hooksExpected = 9; reason = $null }
    }

    function Write-Status($Status) { Write-JsonFile (Join-Path $installed 'mod-status.json') $Status }

    function Invoke-Check([string] $Folder = $release, [scriptblock] $Readers = { 'NVDA' }) {
        Get-InstallCheck -ReleaseFolder $Folder -ConfigPath $config -FindScreenReader $Readers
    }

    function Get-Check([string] $Name, $Checks = (Invoke-Check)) { $Checks | Where-Object Name -EQ $Name }

    # The script in a new process, as a player runs it; SHELL is pwsh or powershell (Windows PowerShell 5.1).
    function Invoke-Script([string] $Folder = $release, [string] $Shell = 'pwsh', [string[]] $Extra = @('-NoSpeech')) {
        $output = & $Shell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $Folder 'check-install.ps1') `
            -ReloadedConfig $config @Extra 2>&1
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Lines = @($output | ForEach-Object { "$_" }) }
    }
}

Describe 'Get-InstallCheck' {
    BeforeEach {
        $script:root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $script:savedLauncher = $env:METAPHOR_RELOADED_EXE
        $env:METAPHOR_RELOADED_EXE = $null
        New-Install
    }
    AfterEach { $env:METAPHOR_RELOADED_EXE = $script:savedLauncher }

    It 'passes every check on a working install, run from the downloaded release folder' {
        $checks = Invoke-Check
        @($checks | Where-Object State -NE 'pass' | ForEach-Object Line) | Should -BeNullOrEmpty
        $checks.Name | Should -Be 'Reloaded-II', 'Mod installed', 'Game added', 'Mod enabled', 'Game version',
            'Speech library', 'Last start', 'Screen reader'
        Get-InstallSummary $checks | Should -Match 'Everything this script checks is in order'
        (Get-Check 'Game version' $checks).Line | Should -Match 'Steam build 18330018'
        (Get-Check 'Last start' $checks).Line | Should -Match '9 of 9 hooks'
        (Get-Check 'Screen reader' $checks).Line | Should -Match 'NVDA is running'
    }

    It 'passes the same way run from the installed mod folder' {
        @(Invoke-Check $installed | Where-Object State -NE 'pass') | Should -BeNullOrEmpty
    }

    It 'names installing Reloaded-II first when its config is missing, and checks nothing that needs it' {
        Remove-Item -LiteralPath $config
        $checks = Invoke-Check
        (Get-Check 'Reloaded-II' $checks).State | Should -Be 'problem'
        foreach ($name in 'Mod installed', 'Game added', 'Mod enabled', 'Game version', 'Last start') {
            (Get-Check $name $checks).State | Should -Be 'not checked'
        }
        Get-InstallSummary $checks | Should -Match '^First thing to fix: Install Reloaded-II'
    }

    It 'says when the launcher Reloaded-II recorded is gone' {
        Remove-Item -LiteralPath $launcher
        (Get-Check 'Reloaded-II').Line | Should -Match 'program is not where Reloaded-II last ran from'
    }

    It 'says the mod is not in the mods folder, and where to copy it' {
        Remove-Item -LiteralPath $installed -Recurse
        $checks = Invoke-Check
        (Get-Check 'Mod installed' $checks).State | Should -Be 'problem'
        (Get-Check 'Mod installed' $checks).Detail | Should -Match ([regex]::Escape($mods))
        (Get-Check 'Last start' $checks).State | Should -Be 'not checked'
        Get-InstallSummary $checks | Should -Match "^First thing to fix: Copy the $([regex]::Escape($modId)) folder"
    }

    It 'finds the mod in a mods subfolder of another name by its ModConfig.json' {
        Rename-Item -LiteralPath $installed 'Metaphor Accessibility'
        (Get-Check 'Mod installed').State | Should -Be 'pass'
    }

    It 'says the game has not been added to Reloaded-II' {
        Remove-Item -LiteralPath $appConfig
        $checks = Invoke-Check
        (Get-Check 'Game added' $checks).State | Should -Be 'problem'
        (Get-Check 'Mod enabled' $checks).State | Should -Be 'not checked'
        (Get-Check 'Game version' $checks).State | Should -Be 'not checked'
    }

    It 'says the mod is not turned on for the game' {
        Write-JsonFile $appConfig @{ AppId = 'metaphor.exe'; AppLocation = $game; EnabledMods = @('other.mod') }
        $checks = Invoke-Check
        (Get-Check 'Mod enabled' $checks).State | Should -Be 'problem'
        Get-InstallSummary $checks | Should -Match '^First thing to fix: Turn the mod on'
    }

    It 'says the game version is unknown when the exe''s SHA-256 is not a known build' {
        Set-Content -LiteralPath $game -Value 'a patched game' -NoNewline
        $check = Get-Check 'Game version'
        $check.Line | Should -Match 'a version this mod does not know yet'
        $check.Detail | Should -Match (Get-Sha256 $game)
    }

    It 'says the game program is missing where Reloaded-II points' {
        Remove-Item -LiteralPath $game
        $check = Get-Check 'Game version'
        $check.State | Should -Be 'problem'
        $check.Line | Should -Match 'not found'
    }

    It 'says prism.dll is missing' {
        Remove-Item -LiteralPath (Join-Path $installed 'prism.dll')
        $check = Get-Check 'Speech library'
        $check.State | Should -Be 'problem'
        $check.Line | Should -Match 'prism.dll, is missing'
    }

    It 'says prism.dll is not the pinned one' {
        Set-Content -LiteralPath (Join-Path $installed 'prism.dll') -Value 'another dll' -NoNewline
        (Get-Check 'Speech library').Line | Should -Match 'not the one this release ships'
    }

    It 'reports a start the mod refused, with its reason' {
        Write-Status @{ modId = $modId; state = 'refused'; gameBuildSha256 = 'ab'; hooksLoaded = 0; hooksExpected = 9
            reason = 'unknown game build' }
        $check = Get-Check 'Last start'
        $check.State | Should -Be 'problem'
        $check.Line | Should -Match 'turned itself off because it does not know this game version'
        $check.Detail | Should -Match 'unknown game build'
    }

    It 'reports a status file that was never written' {
        Remove-Item -LiteralPath (Join-Path $installed 'mod-status.json')
        $check = Get-Check 'Last start'
        $check.State | Should -Be 'problem'
        $check.Fix | Should -Match 'Start the game through Reloaded-II'
    }

    It 'notes when no screen reader the mod supports is running, without counting it as a problem' {
        $checks = Invoke-Check -Readers { }
        (Get-Check 'Screen reader' $checks).State | Should -Be 'note'
        (Get-Check 'Screen reader' $checks).Line | Should -Match 'NVDA, JAWS or ZoomText'
        Format-Check (Get-Check 'Screen reader' $checks) | Should -Match '^Note: '
        Get-InstallSummary $checks | Should -Match 'Everything this script checks is in order'
    }

    It 'leaves the screen reader note out of the first thing to fix when there is a real problem' {
        Remove-Item -LiteralPath (Join-Path $installed 'mod-status.json')
        Get-InstallSummary (Invoke-Check -Readers { }) | Should -Match '^First thing to fix: Start the game through Reloaded-II'
    }

    It 'speaks no path or hash, in any check or the summary' {
        $spoken = [Collections.Generic.List[string]]::new()
        $record = { $checks = Invoke-Check -Readers { }; $checks | ForEach-Object { $spoken.Add((Format-SpokenCheck $_)) }
            $spoken.Add((Get-InstallSummary $checks)) }
        & $record
        Set-Content -LiteralPath $game -Value 'a patched game' -NoNewline
        Write-Status @{ modId = $modId; state = 'refused'; gameBuildSha256 = ('0' * 64); hooksLoaded = 0; hooksExpected = 9
            reason = "can't open $root\Game\METAPHOR.exe" }
        Set-Content -LiteralPath (Join-Path $installed 'prism.dll') -Value 'another dll' -NoNewline
        & $record
        Remove-Item -LiteralPath $game, (Join-Path $installed 'prism.dll'), (Join-Path $installed 'mod-status.json')
        & $record
        Remove-Item -LiteralPath $installed -Recurse
        & $record
        Remove-Item -LiteralPath $mods -Recurse
        & $record
        Write-JsonFile $config @{ LauncherPath = '\\host\share\Reloaded-II.exe'; ModConfigDirectory = 'missing' }
        & $record
        Remove-Item -LiteralPath $config
        & $record
        Remove-Item -LiteralPath (Join-Path $release 'ModConfig.json')
        & $record
        foreach ($line in $spoken) {
            $line | Should -Not -Match '[A-Za-z]:\\|\\\\|/[^ ]+/|[0-9a-f]{64}'
            $line | Should -Not -Match ([regex]::Escape($root))
        }
        @($spoken | Where-Object { $_ -match '^Problem: ' }).Count | Should -BeGreaterThan 8
    }

    It 'refuses a launcher on a network share without reading it' {
        Write-JsonFile $config @{ LauncherPath = '\\host\share\Reloaded-II.exe'; ApplicationConfigDirectory = (Split-Path (Split-Path $appConfig))
            ModConfigDirectory = $mods }
        $check = Get-Check 'Reloaded-II'
        $check.State | Should -Be 'problem'
        $check.Line | Should -Match 'network drive'
        $check.Fix | Should -Match 'does not read over the network'
    }

    It 'refuses a mods folder, applications folder or game on a network share' {
        Write-JsonFile $config @{ LauncherPath = $launcher; ApplicationConfigDirectory = '//host/share/Apps'; ModConfigDirectory = '\\?\UNC\host\share\Mods' }
        $checks = Invoke-Check
        (Get-Check 'Mod installed' $checks).Line | Should -Match 'network drive'
        (Get-Check 'Game added' $checks).Line | Should -Match 'network drive'
        Write-JsonFile $config @{ LauncherPath = $launcher; ApplicationConfigDirectory = (Split-Path (Split-Path $appConfig))
            ModConfigDirectory = $mods }
        Write-JsonFile $appConfig @{ AppId = 'metaphor.exe'; AppLocation = '\\host\share\Game\METAPHOR.exe'; EnabledMods = @($modId) }
        (Get-Check 'Game version').Line | Should -Match 'network drive'
    }

    It 'ignores the developer variable METAPHOR_RELOADED_EXE' {
        Remove-Item -LiteralPath $launcher
        $env:METAPHOR_RELOADED_EXE = $game
        (Get-Check 'Reloaded-II').State | Should -Be 'problem'
    }

    It 'tells the player to turn the mod on in Reloaded-II, not to edit a file' {
        Write-JsonFile $appConfig @{ AppId = 'metaphor.exe'; AppLocation = $game; EnabledMods = @() }
        $fix = (Get-Check 'Mod enabled').Fix
        $fix | Should -Match 'in Reloaded-II, open the game''s page and tick'
        $fix | Should -Not -Match 'EnabledMods|\.json'
    }

    It 'says it cannot work out which mod it belongs to without a ModConfig.json beside it' {
        Remove-Item -LiteralPath (Join-Path $release 'ModConfig.json')
        (Get-Check 'Mod installed').Detail | Should -Match 'No ModConfig.json'
    }
}

Describe 'the package' {
    It 'ships the check, every module it imports, and the lists it reads' {
        $project = Get-Content -Raw -LiteralPath (Join-Path $repo 'src/Metaphor.Hooks/Metaphor.Hooks.csproj')
        foreach ($file in $shipped) {
            $project | Should -Match ([regex]::Escape("../../scripts/player/$file"))
            foreach ($import in [regex]::Matches((Get-Content -Raw (Join-Path $repo "scripts/player/$file")), "Join-Path \`$PSScriptRoot '([^']+)'")) {
                $import.Groups[1].Value | Should -BeIn $shipped
            }
        }
        $project | Should -Match ([regex]::Escape('<None Include="../Metaphor.Core/known-game-builds.json" Link="known-game-builds.json"'))
        $project | Should -Match ([regex]::Escape('<None Include="prism.json" CopyToPublishDirectory'))
        $project | Should -Not -Match 'scripts/dev/ReloadedDev\.psm1'
    }

    It 'holds only ASCII in what Windows PowerShell 5.1 reads' {
        foreach ($file in $shipped) {
            [IO.File]::ReadAllBytes((Join-Path $repo "scripts/player/$file")) | Where-Object { $_ -gt 127 } | Should -BeNullOrEmpty
        }
    }
}

Describe 'Select-Speech' {
    It 'prints only while a screen reader is running, and says how to hear Windows'' voice too' {
        $choice = Select-Speech -Speak $false -NoSpeech $false -Interactive $true -Readers 'NVDA'
        $choice.Speak | Should -BeFalse
        $choice.Message | Should -Match '^NVDA is running and reads this window, so printing only\. Add -Speak'
    }

    It 'speaks with Windows'' voice when no screen reader is running' {
        (Select-Speech -Speak $false -NoSpeech $false -Interactive $true -Readers @()).Speak | Should -BeTrue
    }

    It 'speaks anyway with -Speak' {
        (Select-Speech -Speak $true -NoSpeech $false -Interactive $true -Readers 'JAWS').Speak | Should -BeTrue
    }

    It 'never speaks with -NoSpeech or without a console' {
        (Select-Speech -Speak $true -NoSpeech $true -Interactive $true -Readers @()).Speak | Should -BeFalse
        $choice = Select-Speech -Speak $false -NoSpeech $false -Interactive $false -Readers @()
        $choice.Speak | Should -BeFalse
        $choice.Message | Should -Be 'No console, so printing only.'
    }
}

# A stand-in SpeechSynthesizer (#457): DEFAULT is the voice it starts with, VOICES the installed ones (name, culture and
# whether enabled). It records what it speaks; no real voice is used.
Describe 'Start-Voice' {
    BeforeAll {
        function New-StandInVoice([string] $Default, [object[]] $Voices) {
            $all = @($Voices | ForEach-Object {
                    [pscustomobject]@{ Enabled = $_[2]; VoiceInfo = [pscustomobject]@{ Name = $_[0]; Culture = [Globalization.CultureInfo] $_[1] } } })
            $voice = [pscustomobject]@{ Voice = ($all | Where-Object { $_.VoiceInfo.Name -eq $Default }).VoiceInfo; All = $all
                Spoken = [Collections.Generic.List[string]]::new() }
            $voice | Add-Member ScriptMethod SetOutputToDefaultAudioDevice { }
            $voice | Add-Member ScriptMethod GetInstalledVoices { $this.All }
            $voice | Add-Member ScriptMethod SelectVoice { param($Name) $this.Voice = ($this.All | Where-Object { $_.VoiceInfo.Name -eq $Name }).VoiceInfo }
            $voice | Add-Member ScriptMethod Speak { param($Text) $this.Spoken.Add($Text) }
            $voice | Add-Member ScriptMethod Dispose { $this.Disposed = $true }
            $voice | Add-Member NoteProperty Disposed $false
            $voice
        }
        # A stand-in OneCore engine (#509; MetaphorInstall.psm1's New-OneCoreEngine): DEFAULT is the voice it starts
        # with, VOICES the installed ones (display name and language tag). It records what it says; no sound is made.
        function New-StandInOneCore([string] $Default, [object[]] $Voices) {
            $all = @($Voices | ForEach-Object { [pscustomobject]@{ DisplayName = $_[0]; Language = $_[1] } })
            $engine = [pscustomobject]@{ AllVoices = $all; Voice = ($all | Where-Object { $_.DisplayName -eq $Default })
                Spoken = [Collections.Generic.List[string]]::new(); Disposed = $false }
            $engine | Add-Member ScriptMethod SpeakText { param($Text) $this.Spoken.Add($Text) }
            $engine | Add-Member ScriptMethod Dispose { $this.Disposed = $true }
            $engine
        }
        # Strings printed to the information stream, so Write-Host's lines, from OUTPUT.
        function Get-Printed($Output) {
            @($Output | Where-Object { $_ -is [Management.Automation.InformationRecord] } | ForEach-Object { "$_" })
        }
        $script:speaking = [pscustomobject]@{ Speak = $true; Message = $null }
        $script:noOneCore = { throw 'no Windows Runtime' }
        $script:noSapi = { throw 'no System.Speech' }
        $script:noEnglish = 'Windows has no English voice to read these lines, so another language''s voice reads them, which may be hard to follow.'
        $script:addVoice = 'You can add an English voice in Windows Settings, under Time and language, then Speech.'
        $script:printOnly = 'Windows'' voice did not start, so the installer will only print from here on.'
    }

    It 'chooses an installed English voice over a French default, en-US first' {
        $standIn = New-StandInVoice 'Hortense' @(('Hortense', 'fr-FR', $true), ('Hazel', 'en-GB', $true), ('Zira', 'en-US', $true))
        $voice = Start-Voice $speaking { $standIn }.GetNewClosure() $noOneCore 6> $null
        $voice.Voice.Name | Should -Be 'Zira'
        $voice.Spoken | Should -BeNullOrEmpty
    }

    It 'skips a disabled English voice' {
        $standIn = New-StandInVoice 'Hedda' @(('Hedda', 'de-DE', $true), ('Zira', 'en-US', $false), ('Hazel', 'en-GB', $true))
        (Start-Voice $speaking { $standIn }.GetNewClosure() $noOneCore 6> $null).Voice.Name | Should -Be 'Hazel'
    }

    It 'keeps an English default voice' {
        $standIn = New-StandInVoice 'Hazel' @(('Zira', 'en-US', $true), ('Hazel', 'en-GB', $true))
        (Start-Voice $speaking { $standIn }.GetNewClosure() $noOneCore 6> $null).Voice.Name | Should -Be 'Hazel'
    }

    It 'keeps the default voice without an English one, and says so once' {
        $standIn = New-StandInVoice 'Haruka' @(('Haruka', 'ja-JP', $true), ('Hortense', 'fr-FR', $true))
        $printed = Get-Printed (Start-Voice $speaking { $standIn }.GetNewClosure() $noOneCore 6>&1)
        $standIn.Voice.Name | Should -Be 'Haruka'
        @($standIn.Spoken) | Should -Be @($noEnglish)
        $printed | Should -Be @($noEnglish)
    }

    It 'keeps the default voice when the English one will not select' {
        $standIn = New-StandInVoice 'Hortense' @(('Hortense', 'fr-FR', $true), ('Zira', 'en-US', $true))
        $standIn | Add-Member -Force ScriptMethod SelectVoice { throw 'voice unavailable' }
        $voice = Start-Voice $speaking { $standIn }.GetNewClosure() $noOneCore 6> $null
        $voice.Voice.Name | Should -Be 'Hortense'
        @($voice.Spoken) | Should -Be @($noEnglish)
    }

    It 'prints only when the voice fails to say there is no English voice' {
        $standIn = New-StandInVoice 'Haruka' @(, ('Haruka', 'ja-JP', $true))
        $standIn | Add-Member -Force ScriptMethod Speak { throw 'audio device gone' }
        $output = @(Start-Voice $speaking { $standIn }.GetNewClosure() $noOneCore 6>&1)
        @($output | Where-Object { $null -ne $_ } | ForEach-Object { "$_" }) | Should -Be @($noEnglish, $printOnly)
    }

    It 'makes no voice while a screen reader is running, so the reader alone reads the window' {
        $speech = Select-Speech -Speak $false -NoSpeech $false -Interactive $true -Readers 'NVDA'
        # A voice made here would fail to start and add 'Windows' voice did not start' to the output.
        $output = @(Start-Voice $speech { throw 'no voice should be made' } { throw 'no voice should be made' } 6>&1)
        @($output | Where-Object { $null -ne $_ } | ForEach-Object { "$_" }) |
            Should -Be @("NVDA is running and reads this window, so printing only. Add -Speak to hear Windows' voice as well.")
    }

    It 'prints only when no voice will start' {
        $output = @(Start-Voice $speaking $noSapi $noOneCore 6>&1)
        @($output | Where-Object { $null -ne $_ } | ForEach-Object { "$_" }) | Should -Be @($printOnly)
    }

    # #509: Windows PowerShell 5.1's System.Speech sees only the "Desktop" voices; the ones Windows Settings adds are
    # OneCore voices, reached through the Windows Runtime.
    It 'chooses a OneCore English voice over a German default, en-US first, before System.Speech' {
        $oneCore = New-StandInOneCore 'Microsoft Katja' @(('Microsoft Katja', 'de-DE'), ('Microsoft Hazel', 'en-GB'),
            ('Microsoft Mark', 'en-US'))
        $voice = Start-Voice $speaking { throw 'System.Speech should not start' } { $oneCore }.GetNewClosure() 6> $null
        $voice.Voice.Name | Should -Be 'Microsoft Mark'
        $voice.Voice.Culture.Name | Should -Be 'en-US'
        $voice.Speak('Step one.')
        @($oneCore.Spoken) | Should -Be @('Step one.')
        $voice.Dispose()
        $oneCore.Disposed | Should -BeTrue
    }

    It 'keeps an English OneCore default voice' {
        $oneCore = New-StandInOneCore 'Microsoft Hazel' @(('Microsoft Mark', 'en-US'), ('Microsoft Hazel', 'en-GB'))
        (Start-Voice $speaking $noSapi { $oneCore }.GetNewClosure() 6> $null).Voice.Name | Should -Be 'Microsoft Hazel'
    }

    It 'uses System.Speech when the Windows Runtime is not there' {
        $standIn = New-StandInVoice 'Zira' @(, ('Zira', 'en-US', $true))
        $voice = Start-Voice $speaking { $standIn }.GetNewClosure() $noOneCore 6> $null
        $voice.Voice.Name | Should -Be 'Zira'
        $voice.Speak('Step one.')
        @($standIn.Spoken) | Should -Be @('Step one.')
    }

    It 'uses an English System.Speech voice when OneCore has none' {
        $oneCore = New-StandInOneCore 'Microsoft Katja' @(, ('Microsoft Katja', 'de-DE'))
        $standIn = New-StandInVoice 'Zira' @(, ('Zira', 'en-US', $true))
        $voice = Start-Voice $speaking { $standIn }.GetNewClosure() { $oneCore }.GetNewClosure() 6> $null
        $voice.Voice.Name | Should -Be 'Zira'
        $oneCore.Disposed | Should -BeTrue
    }

    It 'says how to add an English voice when only OneCore voices in another language are there' {
        $oneCore = New-StandInOneCore 'Microsoft Katja' @(, ('Microsoft Katja', 'de-DE'))
        $standIn = New-StandInVoice 'Hedda' @(, ('Hedda', 'de-DE', $true))
        $printed = Get-Printed (Start-Voice $speaking { $standIn }.GetNewClosure() { $oneCore }.GetNewClosure() 6>&1)
        $printed | Should -Be @("$noEnglish $addVoice")
        @($oneCore.Spoken) | Should -Be @("$noEnglish $addVoice")
        $standIn.Disposed | Should -BeTrue
    }

    It 'moves to System.Speech when the OneCore voice fails mid-install' {
        $oneCore = New-StandInOneCore 'Microsoft Mark' @(, ('Microsoft Mark', 'en-US'))
        $oneCore | Add-Member -Force ScriptMethod SpeakText {
            param($Text)
            if ($this.Spoken.Count -gt 0) { throw 'audio device gone' }
            $this.Spoken.Add($Text)
        }
        $standIn = New-StandInVoice 'Zira' @(, ('Zira', 'en-US', $true))
        $voice = Start-Voice $speaking { $standIn }.GetNewClosure() { $oneCore }.GetNewClosure() 6> $null
        $printed = Get-Printed (& { $voice.Speak('Step one.'); $voice.Speak('Step two.'); $voice.Speak('Step three.') } 6>&1)
        @($oneCore.Spoken) | Should -Be @('Step one.')
        @($standIn.Spoken) | Should -Be @('Step two.', 'Step three.')
        $oneCore.Disposed | Should -BeTrue
        $printed | Should -BeNullOrEmpty
    }

    It 'prints only, and says so once, when the OneCore voice fails and System.Speech will not start' {
        $oneCore = New-StandInOneCore 'Microsoft Mark' @(, ('Microsoft Mark', 'en-US'))
        $oneCore | Add-Member -Force ScriptMethod SpeakText { throw 'audio device gone' }
        $voice = Start-Voice $speaking $noSapi { $oneCore }.GetNewClosure() 6> $null
        $output = @(& { $voice.Speak('Step one.'); $voice.Speak('Step two.') } 6>&1)
        @($output | Where-Object { $null -ne $_ } | ForEach-Object { "$_" }) | Should -Be @($printOnly)
    }

    # Makes the sound for one line through Windows PowerShell 5.1's Windows Runtime and checks it is a WAV file, without
    # playing it. Skipped where Windows has no OneCore voice.
    It 'turns a line into a WAV file through a OneCore voice in Windows PowerShell 5.1' -Skip:(
        -not (Get-Command powershell.exe -ErrorAction SilentlyContinue)) {
        $module = Join-Path $repo 'scripts/player/MetaphorInstall.psm1'
        $command = "`$m = Import-Module '$($module -replace "'", "''")' -PassThru; & `$m {
            try { `$engine = New-OneCoreEngine } catch { 'no OneCore voice'; return }
            try { `$wave = Get-OneCoreWave `$engine.Synth 'Step one.'; `$bytes = `$wave.ToArray() }
            finally { `$engine.Dispose() }
            [Text.Encoding]::ASCII.GetString(`$bytes, 0, 4) + ' ' + [Text.Encoding]::ASCII.GetString(`$bytes, 8, 4)
        }"
        $output = @(& powershell.exe -NoProfile -NonInteractive -EncodedCommand (
                [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))) 2>&1 | ForEach-Object { "$_" })
        if ($output -contains 'no OneCore voice') { Set-ItResult -Skipped -Because 'Windows has no OneCore voice here' }
        $output | Should -Be @('RIFF WAVE')
    }
}

Describe 'Find-ScreenReader' {
    It 'returns only the names of readers the mod supports' {
        @(Find-ScreenReader | Where-Object { $_ -notin 'NVDA', 'JAWS', 'ZoomText' }) | Should -BeNullOrEmpty
    }

    # #459: Windows PowerShell 5.1's Add-Type compiled through the temp folder, and failed with an error on screen when
    # its path held a character outside the system's ANSI code page. Devanagari is in none of them.
    It 'looks for windows in Windows PowerShell 5.1 when the temp folder''s path is in another script' -Skip:(
        -not (Get-Command powershell.exe -ErrorAction SilentlyContinue)) {
        $temp = (New-Item -ItemType Directory (Join-Path $TestDrive 'Temp क 日本')).FullName
        $module = Join-Path $repo 'scripts/player/MetaphorInstall.psm1'
        $command = "Import-Module '$($module -replace "'", "''")'; Find-ScreenReader -Verbose; 'searched'"
        $saved = $env:TEMP, $env:TMP
        try {
            $env:TEMP = $env:TMP = $temp
            $output = & powershell.exe -NoProfile -NonInteractive -EncodedCommand (
                [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))) 2>&1
        }
        finally { $env:TEMP, $env:TMP = $saved }
        $output = @($output | ForEach-Object { "$_" })
        $output | Should -Contain 'searched'
        @($output | Where-Object { $_ -ne 'searched' -and $_ -notin 'NVDA', 'JAWS', 'ZoomText' }) | Should -BeNullOrEmpty
    }
}

Describe 'check-install.ps1' {
    BeforeEach {
        $script:root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $script:savedLauncher = $env:METAPHOR_RELOADED_EXE
        $env:METAPHOR_RELOADED_EXE = $null
        New-Install
    }
    AfterEach { $env:METAPHOR_RELOADED_EXE = $script:savedLauncher }

    It 'prints a line per check in plain words, and the first thing to fix last' {
        Remove-Item -LiteralPath $installed -Recurse
        $result = Invoke-Script
        $result.ExitCode | Should -Be 1
        $result.Lines | Should -Contain 'Pass: Reloaded-II is set up.'
        $result.Lines | Should -Contain "Problem: The mod is not in Reloaded-II's mods folder. Mods folder: $mods"
        $result.Lines[-1] | Should -Match "^First thing to fix: Copy the $([regex]::Escape($modId)) folder"
    }

    It 'exits 0 on a working install, whether or not a screen reader is running' {
        $result = Invoke-Script
        $result.ExitCode | Should -Be 0
        $result.Lines[-1] | Should -Match '^Everything this script checks is in order'
    }

    It 'changes nothing on the machine' {
        $before = Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object { "$($_.FullName) $(Get-Sha256 $_.FullName)" }
        Invoke-Script | Out-Null
        Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object { "$($_.FullName) $(Get-Sha256 $_.FullName)" } |
            Should -Be $before
    }

    It 'prints only, without speaking, when there is no console to speak to' {
        $result = Invoke-Script -Extra @()
        $result.Lines | Should -Contain 'No console, so printing only.'
    }

    It 'runs in Windows PowerShell 5.1' -Skip:(-not (Get-Command powershell.exe -ErrorAction SilentlyContinue)) {
        $result = Invoke-Script -Shell powershell.exe
        $result.Lines | Should -Contain 'Pass: Reloaded-II is set up.'
        @($result.Lines | Where-Object { $_ -match '^Pass: The game''s version is one the mod knows' }).Count | Should -Be 1
        @($result.Lines | Where-Object { $_ -match '^Pass: The last time the game started' }).Count | Should -Be 1
    }
}
