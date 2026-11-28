# Tests for scripts/setup/agy.ps1 and its deny rules, scripts/setup/agy-settings.json (#1031).
# The rules: command(...) matches word by word as a prefix (Antigravity's docs,
# https://antigravity.google/docs/permissions.md, read 2026-11-27), and command(regex:...) against the whole command
# line as ^(?:pattern)$. The docs read as if a regex were matched one word at a time, but a live check on agy 1.5.18
# (scripts/dev/agy-check-rules.sh, 2026-11-27) showed whole-line matching; these tests match that way, and the live
# check is the one to rerun after an agy update. The script: run in a child pwsh with a stand-in agy first on PATH.
# Run: Invoke-Pester tests/pester (Pester 5 or later).

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path
    $script = Join-Path $repo 'scripts/setup/agy.ps1'
    $settingsPath = Join-Path $repo 'scripts/setup/agy-settings.json'
    $deny = (Get-Content -Raw $settingsPath | ConvertFrom-Json).permissions.deny

    # Whether a command(...) deny rule stops COMMAND.
    function Test-CommandDenied([string] $Command) {
        foreach ($rule in $deny | Where-Object { $_ -like 'command(*)' }) {
            $target = $rule.Substring(8, $rule.Length - 9)
            if ($target.StartsWith('regex:')) {
                if ([regex]::IsMatch($Command, "^(?:$($target.Substring(6)))$")) { return $true }
            }
            else {
                $want = $target -split '\s+'; $have = $Command -split '\s+'
                if ($have.Count -ge $want.Count -and -not (Compare-Object $want $have[0..($want.Count - 1)] -SyncWindow 0)) {
                    return $true
                }
            }
        }
        $false
    }

    $checked = [regex]::Match((Get-Content -Raw $script), "(?m)^\`$checkedVersion = '([\d.]+)'").Groups[1].Value

    function Invoke-Agy([string[]] $ArgList = @(), [switch] $NoAgy, [string] $LocalAppData, [string] $Version = $checked) {
        $root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $bin = (New-Item -ItemType Directory (Join-Path $root 'bin')).FullName
        $log = Join-Path $root 'calls.log'
        if (-not $NoAgy) {
            Set-Content (Join-Path $bin 'agy.cmd') -Encoding ascii -Value @"
@echo off
if "%1"=="--version" (echo $Version& exit /b 0)
>>"$log" echo agy %* in %CD%
"@
        }
        $old = @{ PATH = $env:PATH; LOCALAPPDATA = $env:LOCALAPPDATA }
        $env:PATH = "$bin;$env:SystemRoot\System32"
        $env:LOCALAPPDATA = if ($LocalAppData) { $LocalAppData } else { (New-Item -ItemType Directory (Join-Path $root 'local')).FullName }
        try { $out = & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File $script @ArgList 2>&1; $code = $LASTEXITCODE }
        finally { $env:PATH = $old.PATH; $env:LOCALAPPDATA = $old.LOCALAPPDATA }
        [pscustomobject]@{
            Out = ($out -join "`n"); Code = $code
            Calls = if (Test-Path $log) { @(Get-Content $log) } else { @() }
        }
    }
}

Describe 'agy-settings.json' {
    It 'denies <command>' -ForEach @(
        @{ command = 'git push --force origin main' }
        @{ command = 'git push origin main --force' }
        @{ command = 'git push --force-with-lease' }
        @{ command = 'git push -f' }
        @{ command = 'git push origin -f' }
        @{ command = 'git push -fu origin x' }
        @{ command = 'git push -uf origin x' }
        @{ command = 'git push origin +main' }
        @{ command = 'git -C repo push -f origin x' }
        @{ command = 'git status && git push -f' }
        @{ command = 'git commit --no-verify -m x' }
        @{ command = 'git push --no-verify' }
        @{ command = 'git commit -n -m x' }
        @{ command = 'git commit -nm x' }
        @{ command = 'git commit -m x -n' }
        @{ command = 'SECURITY_REVIEW_SKIP=1 git commit -m x' }
        @{ command = '$env:SECURITY_REVIEW_SKIP = "1"' }
        @{ command = 'cat .claude/pii-patterns.txt' }
        @{ command = 'Get-Content .claude/pii-patterns.txt' }
        @{ command = 'head -5 .claude/pii-patterns.txt' }
    ) {
        Test-CommandDenied $command | Should -BeTrue
    }

    It 'allows <command>' -ForEach @(
        @{ command = 'git push' }
        @{ command = 'git push -u origin feat/1031-agy' }
        @{ command = 'git push origin main' }
        @{ command = 'git push --follow-tags' }
        @{ command = 'git fetch --force' }
        @{ command = 'git commit -m "fix: one"' }
        @{ command = 'git commit --amend --no-edit' }
        @{ command = 'git commit -m x && git log -n 5' }
        @{ command = 'git log -n 5' }
        @{ command = 'bash scripts/check.sh full' }
        @{ command = 'gh pr merge 12 --auto' }
        @{ command = 'grep -F -f .claude/pii-patterns.txt notes.md' }
    ) {
        Test-CommandDenied $command | Should -BeFalse
    }

    It 'denies reading the PII patterns file with the file tools' {
        $deny | Should -Contain 'read_file(.claude/pii-patterns.txt)'
    }
}

Describe 'agy.ps1' {
    It 'runs agy from the repository root with the deny rules and the arguments' {
        $run = Invoke-Agy @('-p', 'hello')
        $run.Calls | Should -Contain "agy --settings $settingsPath -p hello in $repo"
    }

    It 'finds agy in its install folder when it isn''t on PATH' {
        $local = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        New-Item -ItemType Directory (Join-Path $local 'agy/bin') | Out-Null
        # A real console program in agy.exe's place; it rejects agy's arguments, which shows it ran.
        # curl.exe, as it needs no language resources beside it.
        Copy-Item (Join-Path $env:SystemRoot 'System32/curl.exe') (Join-Path $local 'agy/bin/agy.exe')
        $run = Invoke-Agy -NoAgy -LocalAppData $local
        $run.Out | Should -Not -Match "isn't installed"
        $run.Out | Should -Match 'option --settings: is unknown'
    }

    It 'warns when agy is not the version the rules were checked on' {
        (Invoke-Agy -Version '9.9.9').Out | Should -Match "rules were checked on $checked"
        (Invoke-Agy).Out | Should -Not -Match 'rules were checked on'
    }

    It 'says how to install agy when it is missing' {
        $run = Invoke-Agy -NoAgy
        $run.Code | Should -Not -Be 0
        $run.Out | Should -Match 'irm https://antigravity.google/cli/install.ps1'
    }
}
