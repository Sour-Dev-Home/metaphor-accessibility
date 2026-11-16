# Tests for scripts/signature-spike.ps1, run against the stand-in launcher and game (tests/Metaphor.StandInGame). The
# stand-in "scans" its own exe file, so the memory side sees file offsets, not addresses.

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path
    $script = Join-Path $repo 'scripts/signature-spike.ps1'
    $standIn = Join-Path $repo 'tests/Metaphor.StandInGame/bin/Release/net9.0'
    dotnet build (Join-Path $repo 'tests/Metaphor.StandInGame') -c Release -v q --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'the stand-in game failed to build' }

    function Write-Json([string] $Path, $Value) {
        New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
        $Value | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
    }

    function Get-Game { Get-Process -ErrorAction SilentlyContinue | Where-Object Path -EQ $script:gameExe }
}

Describe 'signature-spike.ps1' {
    BeforeAll {
        $root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $game = Join-Path $root 'game'
        Copy-Item $standIn $game -Recurse
        $script:gameExe = Join-Path $game 'METAPHOR.exe'
        Copy-Item (Join-Path $game 'Metaphor.StandInGame.exe') $script:gameExe
        $launcher = Join-Path $game 'Metaphor.StandInGame.exe'
        $script:build = (Get-FileHash $script:gameExe -Algorithm SHA256).Hash.ToLowerInvariant()

        $reloaded = Join-Path $root 'reloaded'
        $config = Join-Path $reloaded 'ReloadedII.json'
        Write-Json $config ([ordered]@{
                ApplicationConfigDirectory = Join-Path $reloaded 'Apps'
                ModConfigDirectory         = Join-Path $reloaded 'Mods'
                ModUserConfigDirectory     = Join-Path $reloaded 'User/Mods'
                MiscConfigDirectory        = Join-Path $reloaded 'User/Misc'
                PluginConfigDirectory      = Join-Path $reloaded 'Plugins'
                LauncherPath               = 'C:/Reloaded/Reloaded-II.exe'
            })
        Write-Json (Join-Path $reloaded 'Apps/metaphor.exe/AppConfig.json') ([ordered]@{
                AppId = 'metaphor.exe'; AppLocation = $script:gameExe; EnabledMods = @('user.mod')
            })
        Write-Json (Join-Path $reloaded 'Mods/user.mod/ModConfig.json') @{ ModId = 'user.mod' }
        $configBefore = Get-FileHash $config
        $known = Join-Path $root 'known-game-builds.json'
        Write-Json $known @(@{ sha256 = $script:build; steamBuildId = 1; evidence = 'test' })
        $env:METAPHOR_STANDIN_RELOADED_CONFIG = $config
        Remove-Item Env:METAPHOR_STANDIN_BEHAVIOUR -ErrorAction SilentlyContinue

        # The committed probes, plus one that matches the stand-in exe: its DOS header, at the image's start both on
        # disk and in "memory".
        $probes = Join-Path $root 'probes.json'
        $committed = Get-Content -Raw (Join-Path $repo 'tools/Metaphor.SignatureCheck/signature-probes.json') | ConvertFrom-Json
        Write-Json $probes (@($committed) + @{ name = 'dos-header'; pattern = '4D 5A 90 00'; source = 'test' })

        function Invoke-Spike([string] $Draft) {
            $output = & pwsh -NoProfile -File $script -GameExe $script:gameExe -LauncherExe $launcher -Probes $probes `
                -ReloadedConfig $config -KnownBuilds $known -OutFile $Draft -TimeoutSeconds 10 2>&1
            [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
        }

        # A run where the mod refuses, so the smoke test fails.
        $env:METAPHOR_STANDIN_BEHAVIOUR = 'refuse'
        $script:failedDraft = Join-Path $root 'failed-draft.md'
        $script:failedRun = Invoke-Spike $script:failedDraft
        Get-Game | Stop-Process -Force
        Remove-Item Env:METAPHOR_STANDIN_BEHAVIOUR

        $script:draft = Join-Path $root 'draft.md'
        $run = Invoke-Spike $script:draft
        $script:exitCode = $run.ExitCode
        $script:output = $run.Output
        $script:configRestored = (Get-FileHash $config).Hash -eq $configBefore.Hash
        $script:root = $root
    }

    AfterAll {
        Get-Game | Stop-Process -Force
        Remove-Item Env:METAPHOR_STANDIN_RELOADED_CONFIG -ErrorAction SilentlyContinue
    }

    It 'writes a draft and exits 0' {
        $script:exitCode | Should -Be 0 -Because $script:output
        $script:draft | Should -Exist
    }

    It 'names the game build in the raw-source header' {
        Get-Content -Raw $script:draft | Should -Match ([regex]::Escape("- **Game build:** ``$script:build``"))
    }

    It 'compares each probe on disk and in memory' {
        $draft = Get-Content -Raw $script:draft
        $draft | Should -Match '\| `CriFs_InitializeLibrary` \| 0 \|  \| 0 \|  \| absent from both \|'
        $draft | Should -Match '\| `dos-header` \| 1 \| 0x0 \| 1 \| 0x0 \| same place \|'
    }

    It 'closes the game and restores the Reloaded-II config' {
        Get-Game | Should -BeNullOrEmpty
        $script:configRestored | Should -BeTrue
    }

    It 'fails and writes no draft when the smoke test fails' {
        $script:failedRun.ExitCode | Should -Be 1 -Because $script:failedRun.Output
        $script:failedDraft | Should -Not -Exist
    }

    It 'keeps local paths out of the draft' {
        Get-Content -Raw $script:draft | Should -Not -Match ([regex]::Escape($script:root))
    }
}
