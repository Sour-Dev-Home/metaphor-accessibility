# Tests for scripts/setup/gemini.ps1 and its deny rules, scripts/setup/gemini-policy.toml (#1031).
# The rules: each commandRegex is tested as Gemini CLI 0.71.1 tests it (its bundle's buildArgsPatterns): against the
# arguments as JSON, matching from right after `"command":"`. .NET and JavaScript agree on the syntax these rules use
# (\b, \s, alternation). The script: run in a child pwsh with stand-ins for gemini and npm first on PATH and
# GEMINI_CLI_HOME in TestDrive.
# Run: Invoke-Pester tests/pester (Pester 5 or later).

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path
    $script = Join-Path $repo 'scripts/setup/gemini.ps1'
    $policy = Join-Path $repo 'scripts/setup/gemini-policy.toml'
    $pinned = [regex]::Match((Get-Content -Raw $script), "(?m)^\`$geminiVersion = '([\d.]+)'").Groups[1].Value

    # Each [[rule]] of the policy as a hashtable of its keys.
    function Get-Rules {
        $rules = @(); $current = $null
        foreach ($line in Get-Content $policy) {
            if ($line -match '^\[\[rule\]\]') { if ($current) { $rules += $current }; $current = @{}; continue }
            if ($current -and $line -match "^(\w+) = (?:'(.*)'|`"(.*)`"|(\d+))$") {
                $current[$Matches[1]] = @($Matches[2], $Matches[3], $Matches[4]) | Where-Object { $_ } | Select-Object -First 1
            }
        }
        if ($current) { $rules += $current }
        $rules
    }

    # Whether a deny rule stops TOOL with ARGS (a hashtable), as Gemini CLI matches them.
    function Test-Denied([string] $Tool, [hashtable] $ToolArgs) {
        $json = $ToolArgs | ConvertTo-Json -Compress
        foreach ($rule in Get-Rules | Where-Object { $_.toolName -eq $Tool -and $_.decision -eq 'deny' }) {
            if ($rule.commandRegex) {
                $command = ($ToolArgs.command | ConvertTo-Json -Compress).Trim('"')
                if ([regex]::IsMatch("`0`"command`":`"$command", "^\x00`"command`":`"(?:$($rule.commandRegex))")) { return $true }
            }
            elseif ($rule.argsPattern -and [regex]::IsMatch($json, $rule.argsPattern)) { return $true }
        }
        $false
    }

    function Invoke-Setup([string] $InstalledVersion, [string[]] $ArgList = @(), [string] $GeminiHome) {
        $root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $bin = (New-Item -ItemType Directory (Join-Path $root 'bin')).FullName
        $log = Join-Path $root 'calls.log'
        Set-Content (Join-Path $bin 'gemini.cmd') -Encoding ascii -Value @"
@echo off
if "%1"=="--version" (echo $InstalledVersion& exit /b 0)
>>"$log" echo gemini %* in %CD%
"@
        Set-Content (Join-Path $bin 'npm.cmd') -Encoding ascii -Value ">>`"$log`" echo npm %*"
        $home_ = if ($GeminiHome) { $GeminiHome } else { (New-Item -ItemType Directory (Join-Path $root 'home')).FullName }
        $env:GEMINI_CLI_HOME = $home_
        $oldPath = $env:PATH; $env:PATH = "$bin;$env:PATH"
        try { $out = & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File $script @ArgList 2>&1 }
        finally { $env:PATH = $oldPath; Remove-Item Env:GEMINI_CLI_HOME }
        [pscustomobject]@{
            Out = ($out -join "`n"); Home = $home_
            Calls = if (Test-Path $log) { @(Get-Content $log) } else { @() }
            Policy = Join-Path $home_ '.gemini/policies/metaphor-accessibility.toml'
        }
    }
}

Describe 'gemini-policy.toml' {
    It 'denies <command>' -ForEach @(
        @{ command = 'git push --force origin main' }
        @{ command = 'git push origin main --force' }
        @{ command = 'git push --force-with-lease' }
        @{ command = 'git push -f' }
        @{ command = 'git push origin -f' }
        @{ command = 'git push origin +main' }
        @{ command = 'git push -fu origin x' }
        @{ command = 'git push -uf origin x' }
        @{ command = 'git commit -nm x' }
        @{ command = 'git -C repo push -f origin x' }
        @{ command = 'git status && git push -f' }
        @{ command = 'git commit --no-verify -m x' }
        @{ command = 'git push --no-verify' }
        @{ command = 'git commit -n -m x' }
        @{ command = 'git commit -m x -n' }
        @{ command = 'SECURITY_REVIEW_SKIP=1 git commit -m x' }
        @{ command = '$env:SECURITY_REVIEW_SKIP = "1"' }
        @{ command = 'cat .claude/pii-patterns.txt' }
        @{ command = 'Get-Content .claude/pii-patterns.txt' }
        @{ command = 'head -5 .claude/pii-patterns.txt' }
    ) {
        Test-Denied 'run_shell_command' @{ command = $command } | Should -BeTrue
    }

    It 'allows <command>' -ForEach @(
        @{ command = 'git push' }
        @{ command = 'git push -u origin feat/1031-gemini-setup' }
        @{ command = 'git push origin main' }
        @{ command = 'git fetch --force' }
        @{ command = 'git commit -m "fix: one"' }
        @{ command = 'git log -n 5' }
        @{ command = 'bash scripts/check.sh full' }
        @{ command = 'gh pr merge 12 --auto' }
        @{ command = 'grep -F -f .claude/pii-patterns.txt notes.md' }
        @{ command = 'git commit -m x && git log -n 5' }
        @{ command = 'git push -u origin feat/x' }
    ) {
        Test-Denied 'run_shell_command' @{ command = $command } | Should -BeFalse
    }

    It 'denies <tool> on the PII patterns file' -ForEach @(
        @{ tool = 'read_file'; toolArgs = @{ file_path = 'C:/repo/.git/pii-patterns.txt' } }
        @{ tool = 'read_many_files'; toolArgs = @{ include = @('**/pii-patterns.txt') } }
        @{ tool = 'grep_search'; toolArgs = @{ pattern = 'x'; dir_path = '.git/pii-patterns.txt' } }
        @{ tool = 'read_many_files'; toolArgs = @{ include = @('.claude/*.txt') } }
        @{ tool = 'read_many_files'; toolArgs = @{ include = @('**/*.txt'); respect_git_ignore = $false } }
        @{ tool = 'grep_search'; toolArgs = @{ pattern = 'x'; include_pattern = '.claude\**' } }
        @{ tool = 'grep_search'; toolArgs = @{ pattern = 'x'; no_ignore = $true } }
        @{ tool = 'glob'; toolArgs = @{ pattern = '**/*.txt'; respect_git_ignore = $false } }
    ) {
        Test-Denied $tool $toolArgs | Should -BeTrue
    }

    It 'allows <tool> on other files' -ForEach @(
        @{ tool = 'read_file'; toolArgs = @{ file_path = 'AGENTS.md' } }
        @{ tool = 'read_many_files'; toolArgs = @{ include = @('docs/**/*.md') } }
        @{ tool = 'grep_search'; toolArgs = @{ pattern = 'x'; dir_path = '.claude/hooks' } }
        @{ tool = 'glob'; toolArgs = @{ pattern = '**/*.cs' } }
    ) {
        Test-Denied $tool $toolArgs | Should -BeFalse
    }
}

Describe 'gemini.ps1' {
    It 'installs the pinned version when another one is there, and the deny rules' {
        $run = Invoke-Setup '0.1.0'
        $run.Calls | Should -Contain "npm install -g @google/gemini-cli@$pinned"
        Get-Content -Raw $run.Policy | Should -Be (Get-Content -Raw $policy)
    }

    It 'skips the install when the pinned version is there' {
        $run = Invoke-Setup $pinned
        $run.Calls | Where-Object { $_ -like 'npm *' } | Should -BeNullOrEmpty
    }

    It 'replaces deny rules that differ, and leaves current ones alone' {
        $run = Invoke-Setup $pinned
        $run.Out | Should -Match 'Installed this repository''s deny rules'
        (Invoke-Setup $pinned -GeminiHome $run.Home).Out | Should -Not -Match 'deny rules'
        Set-Content $run.Policy 'stale'
        (Invoke-Setup $pinned -GeminiHome $run.Home).Out | Should -Match 'Installed this repository''s deny rules'
        Get-Content -Raw $run.Policy | Should -Be (Get-Content -Raw $policy)
    }

    It 'runs gemini from the repository root with the arguments' {
        $run = Invoke-Setup $pinned @('-p', 'hello')
        $run.Calls | Should -Contain "gemini -p hello in $repo"
    }

    It 'loads AGENTS.md as the project context file' {
        $settings = Get-Content -Raw (Join-Path $repo '.gemini/settings.json') | ConvertFrom-Json
        $settings.context.fileName | Should -Be @('AGENTS.md')
    }
}
