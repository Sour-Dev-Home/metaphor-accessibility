<#
.SYNOPSIS
Takes a new contributor from a fresh clone to a working setup (#267), then prints the prompt to paste into Claude.

.DESCRIPTION
Run it from the repository root in PowerShell: .\scripts\onboard.ps1. Every step is safe to re-run, and a second run
says that nothing changed.

0. Run from Windows PowerShell with PowerShell 7 installed (on PATH, or at its default path when PATH hasn't caught up
   yet), it says so and runs itself again under pwsh, with the same arguments, in the same window.
1. Tools: the .NET SDK in global.json (roll-forward is off, so exactly that version), Git for Windows with Git Bash,
   PowerShell 7, the GitHub CLI and Node.js LTS (for the docs tools). For each one missing it offers a pinned winget
   install (an exact version; winget checks the installer against the hash in its manifest) and installs only after a
   yes. A no stops with what to do, except for Node.js, which only the docs tools need. When winget says a tool is
   already installed that the script still doesn't find, it says what it looked for. Then the Visual C++
   Redistributable (x64), which lychee needs (#385): the same offer, with the installer's own admin prompt, and the run
   carries on after it.
2. Git hooks: sets core.hooksPath to scripts/githooks for this clone.
3. Private patterns: creates the git-ignored .claude/pii-patterns.txt from the example, opens it in Notepad, and stops
   while it still holds the example lines. It never prints the file.
4. Docs tools: runs scripts/install-docs-tools.sh unless the pinned versions already run, and names any that don't.
5. WSL: runs scripts/setup-wsl.ps1 (it asks before creating the metaphor-checks distro). Without that distro the
   check-script suites run in Git Bash, and a docs-only check.sh full takes about 4 minutes longer.
6. Skills: asks, then adds Matt Pocock's skills through scripts/claude.ps1, unless they're already there.
7. GitHub: checks gh's login and whether the account can push to the repository.
8. Game: finds the game and Reloaded-II (scripts/GameLocator.psm1), shows what it found and asks before saving the
   game's path as METAPHOR_GAME_EXE for your Windows user. With both found, it offers a 30-second test deploy
   (scripts/listen.ps1 -Seconds 30 -Close) and reports the hooks.
9. Extras: offers to build the Ghidra project (scripts/ghidra.ps1 build, about 10 minutes).
10. Runs bash scripts/check.sh fast, prints a ready summary and the prompt to paste into Claude, then starts
    scripts/claude.ps1 unless -NoClaude.

Without a console (pwsh -NonInteractive, redirected input, no user session) it answers every question no, opens neither
Notepad nor Claude Code, and says so in the summary.

It writes only inside the clone, except through the tools it calls (winget, git, Claude Code's plugin commands, setx
for METAPHOR_GAME_EXE, WSL through setup-wsl.ps1, and Reloaded-II's mod folder through listen.ps1).

Exit codes: 0 it reached the summary (which lists anything left to do); 1 it stopped at a missing tool, or the private
patterns aren't filled in.

.EXAMPLE
.\scripts\onboard.ps1
.\scripts\onboard.ps1 -NoClaude
#>
[CmdletBinding()]
param(
    # Don't start Claude Code at the end.
    [switch] $NoClaude,
    # Answers to the questions, in order, for a scripted run: the run then counts as having a console. A question past
    # the last answer is answered no.
    [string[]] $Answers,
    # Steam's folder. Defaults to the one in the registry.
    [string] $SteamRoot = '',
    # Reloaded-II's root config. Defaults to its fixed AppData path (ReloadedDev.psm1's Get-ReloadedConfigPath).
    [string] $ReloadedConfig = '',
    # The Program Files folder to look in for PowerShell 7 when pwsh isn't on PATH. Defaults to Windows' own.
    [string] $ProgramFiles = $env:ProgramFiles,
    # The folder to look in for the Visual C++ runtime's vcruntime140.dll. Defaults to Windows' System32.
    [string] $SystemDirectory = [Environment]::SystemDirectory
)

# This file must parse in Windows PowerShell 5.1 too, so it can say that PowerShell 7 is missing: no PowerShell 7-only
# syntax, and ASCII only (5.1 reads a BOM-less file as ANSI).
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoSlug = 'Sour-Dev-Home/metaphor-accessibility'
# Pinned winget packages, each checked with winget show
# (docs/raw-sources/onboard-winget-pins-and-return-codes-2026-11-19.md). To update one, pick a version from `winget show --id <id> --versions` and record the check there or in a new source.
$gitPackage = @{ Id = 'Git.Git'; Version = '2.56.0.4' }
$powerShellPackage = @{ Id = 'Microsoft.PowerShell'; Version = '7.6.10.0' }
$ghPackage = @{ Id = 'GitHub.cli'; Version = '2.105.0' }
$nodePackage = @{ Id = 'OpenJS.NodeJS.LTS'; Version = '26.13.1' }
# docs/raw-sources/vc-redist-winget-pin-and-lychee-runtime-2026-11-19.md (#385).
$vcRedistPackage = @{ Id = 'Microsoft.VCRedist.2015+.x64'; Version = '14.52.37029.0' }
# winget's exit codes for a package that is already installed: PACKAGE_ALREADY_INSTALLED, UPDATE_NOT_APPLICABLE,
# UPGRADE_VERSION_NOT_NEWER, INSTALL_ALREADY_INSTALLED and INSTALL_DOWNGRADE, as winget-cli's returnCodes.md lists them
# (docs/raw-sources/onboard-winget-pins-and-return-codes-2026-11-19.md).
$wingetAlreadyInstalled = @(0x8A150061, 0x8A15002B, 0x8A15004F, 0x8A15010D, 0x8A15010E)

$root = Split-Path -Parent $PSScriptRoot
$ok = [string][char]0x2713
$notOk = [string][char]0x2717
$summary = [Collections.Generic.List[object]]::new()
$changes = [Collections.Generic.List[string]]::new()

function Write-Step([string] $Title) { Write-Host ''; Write-Host "== $Title" }
# READY is $true, $false, or 'optional' for an item nothing else needs.
function Add-Item([string] $Name, [object] $Ready, [string] $Detail) {
    $summary.Add([pscustomobject]@{ Name = $Name; Ready = $Ready; Detail = $Detail })
}
Import-Module (Join-Path $PSScriptRoot 'Interactive.psm1') -Force
$scripted = $PSBoundParameters.ContainsKey('Answers')
$answerQueue = [Collections.Generic.Queue[string]]::new()
if ($scripted) { foreach ($answer in $Answers) { $answerQueue.Enqueue($answer) } }
$interactive = $scripted -or (Test-Interactive)
# An answer from -Answers, or else from the console (Interactive.psm1): '' for Enter, $null for none (counts as no).
function Read-OnboardAnswer([string] $Question) {
    if (-not $scripted) { return Read-Answer $Question }
    Write-Host "$Question " -NoNewline
    if ($answerQueue.Count -eq 0) { Write-Host '(no answer given, so no)'; return $null }
    $answer = $answerQueue.Dequeue()
    Write-Host $answer
    return $answer
}
function Read-YesNo([string] $Question) {
    $answer = Read-OnboardAnswer "$Question [y/N]"
    return [bool]($null -ne $answer -and $answer -match '^\s*(y|yes)\s*$')
}

# PowerShell 7's pwsh.exe: at the installer's default path first (a window opened before the install doesn't have it on
# PATH yet, and a pwsh.exe earlier on PATH could be anything), else on PATH (#358).
function Find-PowerShell7 {
    if ($ProgramFiles) {
        $default = Join-Path $ProgramFiles 'PowerShell\7\pwsh.exe'
        if (Test-Path -LiteralPath $default) { return $default }
    }
    $onPath = Get-Command pwsh -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($onPath) { return $onPath.Source }
    return $null
}
# --- 0. Windows PowerShell with PowerShell 7 installed: carry on under pwsh, in this window --------------------------
if ($PSVersionTable.PSVersion.Major -lt 7) {
    $powerShell7 = Find-PowerShell7
    if ($powerShell7) {
        Write-Host "PowerShell 7 is installed ($powerShell7), and this window is Windows PowerShell, so onboarding"
        Write-Host 'carries on under PowerShell 7 in this window. Next time, start it from pwsh.'
        # The same arguments as a command, every value a single-quoted literal escaped by PowerShell's own
        # CodeGeneration (it doubles the typographic single quotes too, which PowerShell also ends a string on).
        # -File can't pass -Answers' array, and Windows PowerShell drops empty arguments, so the command goes
        # base64-encoded: no command-line quoting is involved. -OutputFormat Text keeps the output as -Command gives it
        # (without it, redirected host output also reaches the console).
        $quote = {
            param($Text)
            "'" + [Management.Automation.Language.CodeGeneration]::EscapeSingleQuotedStringContent("$Text") + "'"
        }
        $command = '& ' + (& $quote $PSCommandPath)
        foreach ($name in $PSBoundParameters.Keys) {
            if ($name -notmatch '^\w+$') { continue }
            $value = $PSBoundParameters[$name]
            if ($value -is [switch]) { if ($value.IsPresent) { $command += " -$name" } }
            elseif ($value -is [array]) {
                $command += " -$name @(" + (@($value | ForEach-Object { & $quote $_ }) -join ',') + ')'
            }
            else { $command += " -$name " + (& $quote $value) }
        }
        $command += '; exit $LASTEXITCODE'
        $flags = @('-NoProfile', '-OutputFormat', 'Text')
        if (-not $interactive) { $flags += '-NonInteractive' }
        & $powerShell7 @flags -EncodedCommand ([Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command)))
        exit $LASTEXITCODE
    }
}

if (-not $interactive) {
    Write-Host 'No console to answer on: every question is answered no, and Notepad and Claude Code are not started.'
}
function Test-Command([string] $Name) { [bool](Get-Command $Name -ErrorAction SilentlyContinue) }
function Test-VcRuntime { Test-Path -LiteralPath (Join-Path $SystemDirectory 'vcruntime140.dll') }

# Git Bash next to git: Git for Windows keeps git.exe in cmd\ (on PATH) and bash.exe in bin\.
function Find-GitBash {
    $git = Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $git) { return $null }
    $dir = Split-Path $git.Source
    foreach ($candidate in @((Join-Path $dir 'bash.exe'), (Join-Path $dir '..\bin\bash.exe'),
            (Join-Path $dir '..\..\bin\bash.exe'))) {
        if (Test-Path -LiteralPath $candidate) { return [IO.Path]::GetFullPath($candidate) }
    }
    return $null
}

# --- 1. Tools ---------------------------------------------------------------------------------------------------------
Write-Step 'Tools'
$sdkVersion = (Get-Content -Raw -LiteralPath (Join-Path $root 'global.json') | ConvertFrom-Json).sdk.version
$sdkMajor = $sdkVersion.Split('.')[0]
# Each tool: its pinned winget package, TEST to find it, PROBLEM when it's missing, LOOKS for what TEST looks for, and
# whether the rest of onboarding needs it. Node.js only serves the docs tools: missing, it gets a SUMMARY line instead.
$tools = @(
    [pscustomobject]@{
        Name = '.NET SDK'; Version = $sdkVersion; Id = "Microsoft.DotNet.SDK.$sdkMajor"; Required = $true
        Url = "https://dotnet.microsoft.com/download/dotnet/$sdkMajor.0"
        Test = {
            (Test-Command dotnet) -and (@(& dotnet --list-sdks) -match "^$([regex]::Escape($sdkVersion)) ").Count -gt 0
        }
        Problem = "version $sdkVersion (global.json pins it exactly) isn't installed"
        Looks = "exactly $sdkVersion in dotnet --list-sdks, as global.json pins it; another version doesn't count"
    }
    [pscustomobject]@{
        Name = 'Git'; Version = $gitPackage.Version; Id = $gitPackage.Id; Url = 'https://git-scm.com/download/win'
        Required = $true; Test = { [bool](Find-GitBash) }; Problem = 'Git for Windows with Git Bash isn''t installed'
        Looks = 'bash.exe beside the git on PATH (Git for Windows with Git Bash)'
    }
    [pscustomobject]@{
        Name = 'PowerShell 7'; Version = $powerShellPackage.Version; Id = $powerShellPackage.Id; Required = $true
        Url = 'https://github.com/PowerShell/PowerShell/releases'
        Test = { $PSVersionTable.PSVersion.Major -ge 7 -or [bool](Find-PowerShell7) }
        Problem = 'this is Windows PowerShell, and the scripts need 7'
        Looks = 'pwsh on PATH, or pwsh.exe in Program Files\PowerShell\7'
    }
    [pscustomobject]@{
        Name = 'GitHub CLI'; Version = $ghPackage.Version; Id = $ghPackage.Id; Url = 'https://cli.github.com/'
        Required = $true; Test = { Test-Command gh }; Problem = 'gh isn''t installed'; Looks = 'gh on PATH'
    }
    [pscustomobject]@{
        Name = 'Node.js'; Version = $nodePackage.Version; Id = $nodePackage.Id; Url = 'https://nodejs.org'
        Required = $false; Test = { Test-Command npm }; Looks = 'npm on PATH'
        Problem = 'npm isn''t on PATH, and the docs tools need Node.js for markdownlint'
        Summary = 'not installed, and the docs tools need it: install Node.js LTS (https://nodejs.org), then run ' +
            '.\scripts\onboard.ps1 again'
    }
    # lychee's Windows build loads vcruntime140.dll (#385). Nothing goes on PATH, so this run carries on after the
    # install (SameWindow), and its installer raises its own admin prompt (Admin).
    [pscustomobject]@{
        Name = 'Visual C++ runtime'; Version = $vcRedistPackage.Version; Id = $vcRedistPackage.Id; Required = $false
        Url = 'https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist'; SameWindow = $true; Admin = $true
        Test = { Test-VcRuntime }
        Problem = 'vcruntime140.dll isn''t installed, and lychee, a docs tool, needs it'
        Looks = "vcruntime140.dll in $SystemDirectory"
        Summary = 'not installed, and lychee (a docs tool) needs it: run .\scripts\onboard.ps1 again and say yes to ' +
            'it, or install the Visual C++ Redistributable (x64) from ' +
            'https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist'
    }
)
$installed = @()
$missing = @()
foreach ($tool in $tools) {
    if (& $tool.Test) { Write-Host "$($tool.Name): found"; continue }
    Write-Host "$($tool.Name): $($tool.Problem)."
    if (-not (Test-Command winget)) { $missing += $tool; continue }
    Write-Host "winget can install $($tool.Name) $($tool.Version). A yes also accepts the package's licence agreement and"
    Write-Host "the winget source's terms on your behalf; read them first with:"
    Write-Host "  winget show --id $($tool.Id) --exact --version $($tool.Version)"
    $sameWindow = [bool]$tool.PSObject.Properties['SameWindow']
    if ($tool.PSObject.Properties['Admin']) {
        Write-Host 'The installer needs a Windows admin prompt: answer yes to it too, or the install fails.'
    }
    else { Write-Host 'The installer may show a Windows admin prompt.' }
    if (-not (Read-YesNo "Install $($tool.Name) $($tool.Version) and accept its licence agreement?")) {
        $missing += $tool; continue
    }
    # Only behind that yes: the flags accept the agreements the question named.
    & winget install --id $tool.Id --exact --version $tool.Version --source winget --accept-package-agreements `
        --accept-source-agreements
    $code = $LASTEXITCODE
    if ($code -eq 0 -and $sameWindow) {
        # Nothing to put on PATH, so this run can use it straight away.
        if (& $tool.Test) {
            Write-Host "Installed $($tool.Name)."
            $changes.Add("installed the $($tool.Name)")
            continue
        }
        Write-Host "winget installed $($tool.Name), but onboarding still doesn't find $($tool.Looks)."
        $missing += $tool; continue
    }
    if ($code -eq 0) { $installed += $tool; continue }
    if ($wingetAlreadyInstalled -contains $code) {
        if (& $tool.Test) { Write-Host "$($tool.Name): found"; continue }
        # Installing it again would only repeat this, so say what's missing instead.
        Write-Host "winget says $($tool.Name) is already installed, but onboarding still doesn't find it: it looks for"
        Write-Host "$($tool.Looks). If it was installed only just now, open a new PowerShell window, so it's on PATH,"
        Write-Host "and run .\scripts\onboard.ps1 again there; otherwise install it by hand: $($tool.Url)"
    }
    $missing += $tool
}
$todo = @($missing | Where-Object { $_.Required })
if ($todo.Count -gt 0) {
    Write-Host ''
    Write-Host 'Install these, then run .\scripts\onboard.ps1 again:'
    foreach ($tool in $todo) { Write-Host "  $($tool.Name) $($tool.Version): $($tool.Url)" }
    exit 1
}
if ($installed.Count -gt 0) {
    Write-Host ''
    Write-Host "Installed: $(($installed | ForEach-Object { $_.Name }) -join ', ')."
    Write-Host 'Open a new PowerShell window, so they are on PATH, and run .\scripts\onboard.ps1 again there (for'
    Write-Host 'PowerShell 7, start it with pwsh).'
    exit 1
}
foreach ($tool in $tools) {
    if ($missing -contains $tool) { Add-Item $tool.Name $false $tool.Summary }
    else { Add-Item $tool.Name $true 'installed' }
}
$bash = Find-GitBash

Push-Location $root
try {
    # --- 2. Git hooks -------------------------------------------------------------------------------------------------
    Write-Step 'Git hooks'
    $hooksPath = & git config --local --get core.hooksPath
    if ($hooksPath -eq 'scripts/githooks') {
        Write-Host 'core.hooksPath is already scripts/githooks.'
        Add-Item 'git hooks' $true 'on'
    }
    else {
        & git config --local core.hooksPath scripts/githooks
        if ($LASTEXITCODE -eq 0) {
            Write-Host 'Set core.hooksPath to scripts/githooks: the hooks now check each commit and push.'
            $changes.Add('turned on the git hooks')
            Add-Item 'git hooks' $true 'on'
        }
        else { Add-Item 'git hooks' $false 'run: git config core.hooksPath scripts/githooks (in a git clone)' }
    }

    # --- 3. Private patterns ------------------------------------------------------------------------------------------
    Write-Step 'Private patterns'
    $patterns = Join-Path $root '.claude/pii-patterns.txt'
    $example = Join-Path $root '.claude/pii-patterns.example.txt'
    # The lines check.sh reads as patterns: trimmed, without comments or blanks. Never printed.
    function Get-PatternLine([string] $Path) {
        @(Get-Content -LiteralPath $Path | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') })
    }
    $exampleLines = @(Get-PatternLine $example)
    function Test-Pattern {
        if (-not (Test-Path -LiteralPath $patterns)) { return $false }
        $lines = @(Get-PatternLine $patterns)
        return $lines.Count -gt 0 -and -not ($lines | Where-Object { $exampleLines -contains $_ })
    }
    $patternsReady = Test-Pattern
    if ($patternsReady) {
        Write-Host '.claude/pii-patterns.txt is filled in.'
    }
    else {
        if (-not (Test-Path -LiteralPath $patterns)) {
            Copy-Item -LiteralPath $example -Destination $patterns
            Write-Host 'Created .claude/pii-patterns.txt from the example. It is git-ignored and stays on this machine.'
            $changes.Add('created .claude/pii-patterns.txt')
        }
        Write-Host 'Replace its example lines with your own private strings, one per line: your real name, email, user'
        Write-Host 'names. The checks then block any commit that contains one.'
        if ($interactive) {
            Write-Host 'Opening it in Notepad.'
            & notepad $patterns
            $null = Read-OnboardAnswer 'Press Enter once you have saved it.'
            if (-not (Test-Pattern)) {
                Write-Host ''
                Write-Host '.claude/pii-patterns.txt still holds an example line or no pattern at all. Replace the example'
                Write-Host 'lines with your own private strings, save, and run .\scripts\onboard.ps1 again.'
                exit 1
            }
            $patternsReady = $true
            $changes.Add('filled in the private patterns')
        }
    }
    if ($patternsReady) { Add-Item 'private patterns' $true 'filled in' }
    else { Add-Item 'private patterns' $false 'replace the example lines in .claude/pii-patterns.txt (no console for Notepad)' }

    # --- 4. Docs tools ------------------------------------------------------------------------------------------------
    Write-Step 'Docs tools'
    $installer = Get-Content -LiteralPath (Join-Path $root 'scripts/install-docs-tools.sh')
    $lychee = @($installer -match '^LYCHEE_VERSION=')[0] -replace '^LYCHEE_VERSION=', ''
    $vale = @($installer -match '^VALE_VERSION=')[0] -replace '^VALE_VERSION=', ''
    # The docs tools that don't run at their pins, from the clone's folders for them (install-docs-tools.sh --path,
    # #353): lychee, Vale, markdownlint-cli2. Each one is tried, so a failing lychee doesn't hide Vale (#385).
    function Get-FailingDocsTool {
        $versions = (& $bash -c 'IFS=: read -r l v m <<<"$(bash scripts/install-docs-tools.sh --path)" || exit
            "$l/lychee" --version; "$v/vale" --version
            [ ! -e "$m/markdownlint-cli2" ] || echo markdownlint' 2>&1) -join "`n"
        if (-not $versions.Contains("lychee $lychee")) { 'lychee' }
        if (-not $versions.Contains("version $vale")) { 'Vale' }
        if (-not $versions.Contains('markdownlint')) { 'markdownlint-cli2' }
    }
    function Test-DocsTool { @(Get-FailingDocsTool).Count -eq 0 }
    if (Test-DocsTool) {
        Write-Host "lychee $lychee, Vale $vale and markdownlint-cli2 are installed."
        Add-Item 'docs tools' $true 'installed'
    }
    elseif (-not (Test-Command npm)) {
        Write-Host 'The docs tools need Node.js for markdownlint, and npm is not on PATH.'
        Add-Item 'docs tools' $false 'install Node.js LTS (https://nodejs.org), then run .\scripts\onboard.ps1 again'
    }
    else {
        & $bash scripts/install-docs-tools.sh
        $installCode = $LASTEXITCODE
        $failing = @(Get-FailingDocsTool)
        if ($installCode -eq 0 -and $failing.Count -eq 0) {
            Write-Host "Installed lychee $lychee, Vale $vale and markdownlint-cli2, so check.sh docs runs what CI runs."
            $changes.Add('installed the docs tools')
            Add-Item 'docs tools' $true 'installed'
        }
        else {
            # Name what failed, even when the installer said nothing (#385).
            $what = if ($failing.Count -gt 0) { "$($failing -join ', ') did not run" }
            else { "scripts/install-docs-tools.sh exited $installCode" }
            $fix = 'see install-docs-tools.sh''s message above, then run bash scripts/install-docs-tools.sh in Git Bash'
            if ($failing -contains 'lychee' -and -not (Test-VcRuntime)) {
                $fix = 'it needs the Visual C++ Redistributable (x64): run .\scripts\onboard.ps1 again and say yes ' +
                    'to it'
            }
            Write-Host "Docs tools: $what; $fix."
            Add-Item 'docs tools' $false "$what; $fix"
        }
    }

    # --- 5. WSL -------------------------------------------------------------------------------------------------------
    Write-Step 'WSL'
    $wslLines = @(& (Join-Path $PSScriptRoot 'setup-wsl.ps1') *>&1 | ForEach-Object { Write-Host $_; "$_" })
    $wslCode = $LASTEXITCODE
    if (@($wslLines | Where-Object { $_ -match '^setup-wsl: created ' }).Count -gt 0) {
        $changes.Add('created the metaphor-checks WSL distro')
    }
    if ($wslCode -eq 0) {
        Add-Item 'WSL' $true 'the metaphor-checks distro is ready'
    }
    else {
        # setup-wsl.ps1's last line gives the reason in its first sentence and any fix it knows (a restart, say) after
        # it, before "The check suites keep running in Git Bash."; or it says nothing changed after a no.
        $last = @($wslLines | Where-Object { $_ -match '^setup-wsl: ' } | Select-Object -Last 1)
        $reason = 'not set up.'
        $wslFix = 'Fix that, then run .\scripts\setup-wsl.ps1;'
        $ownFix = $false
        if ($last -and $last[0] -match 'nothing changed\. Run it in a PowerShell window') {
            $reason = 'there was no console to ask about creating the distro.'
        }
        elseif ($last -and $last[0] -match 'nothing changed') { $reason = 'you chose not to create the distro.' }
        elseif ($last -and $last[0] -match '^setup-wsl: (.+?\.)\s+(.+?)\s+The check suites keep running') {
            $reason = $Matches[1]
            $wslFix = $Matches[2] -replace '\.$', ';'
            $ownFix = $true
        }
        elseif ($last -and $last[0] -match '^setup-wsl: (.+?\.)(\s|$)') { $reason = $Matches[1] }
        Write-Host 'Without the metaphor-checks distro the check-script suites run in Git Bash, which is slow: a'
        Write-Host 'docs-only check.sh full takes about 4 minutes longer.'
        # $WslUnavailableExitCode (Interactive.psm1): this machine can't run WSL 2 yet. Any other failure (a network
        # error, say) isn't fixed by installing WSL (#360), and neither is a restart or virtualization that
        # setup-wsl.ps1 names itself (#402).
        if ($wslCode -eq $WslUnavailableExitCode -and -not $ownFix) {
            Write-Host 'To get WSL 2, run wsl --install in an admin PowerShell and restart Windows'
            Write-Host '(https://learn.microsoft.com/windows/wsl/install); then run .\scripts\setup-wsl.ps1.'
        }
        Add-Item 'WSL' $false "$reason $wslFix until then check.sh full is about 4 minutes slower"
    }

    # --- 6. Skills ----------------------------------------------------------------------------------------------------
    Write-Step 'Skills'
    $claudeScript = Join-Path $PSScriptRoot 'claude.ps1'
    function Get-ClaudeList([string[]] $Arguments) {
        $text = (& $claudeScript @Arguments | Out-String).Trim()
        if (-not $text) { return }
        try { $list = $text | ConvertFrom-Json } catch { return }
        # One item per plugin or marketplace: ConvertFrom-Json hands a JSON array on whole.
        foreach ($item in $list) { $item }
    }
    function Test-Skill {
        @(Get-ClaudeList 'plugin', 'list', '--json' |
                Where-Object { $_.PSObject.Properties['id'] -and $_.id -eq 'mattpocock-skills@mattpocock' }).Count -gt 0
    }
    if (-not (Test-Command claude)) {
        Write-Host 'Claude Code is not installed.'
        Add-Item 'skills' 'optional' ('optional: install Claude Code (https://claude.com/claude-code), then run ' +
            '.\scripts\onboard.ps1 again; contributing works without it')
    }
    elseif (Test-Skill) {
        Write-Host 'Matt Pocock''s skills are installed.'
        Add-Item 'skills' $true 'installed'
    }
    elseif (-not (Read-YesNo ('Install Matt Pocock''s skills for this project''s Claude Code? It adds the mattpocock/skills ' +
                'marketplace from GitHub and installs its mattpocock-skills plugin.'))) {
        Write-Host 'Skipped. To install them by hand: .\scripts\claude.ps1 plugin marketplace add mattpocock/skills, then'
        Write-Host '.\scripts\claude.ps1 plugin install mattpocock-skills@mattpocock'
        Add-Item 'skills' $false ('not installed; run .\scripts\claude.ps1 plugin marketplace add mattpocock/skills, then ' +
            '.\scripts\claude.ps1 plugin install mattpocock-skills@mattpocock')
    }
    else {
        $markets = Get-ClaudeList 'plugin', 'marketplace', 'list', '--json'
        if (-not @($markets | Where-Object { $_.PSObject.Properties['name'] -and $_.name -eq 'mattpocock' }).Count) {
            & $claudeScript plugin marketplace add mattpocock/skills
        }
        & $claudeScript plugin install mattpocock-skills@mattpocock
        if (Test-Skill) {
            Write-Host 'Installed Matt Pocock''s skills (the mattpocock-skills plugin) for this project''s Claude Code.'
            $changes.Add('installed Matt Pocock''s skills')
            Add-Item 'skills' $true 'installed'
        }
        else { Add-Item 'skills' $false 'run: .\scripts\claude.ps1 plugin install mattpocock-skills@mattpocock' }
    }

    # --- 7. GitHub ----------------------------------------------------------------------------------------------------
    Write-Step 'GitHub'
    function Test-GhLogin { & gh auth status *> $null; return $LASTEXITCODE -eq 0 }
    $loggedIn = Test-GhLogin
    if (-not $loggedIn) {
        Write-Host 'The GitHub CLI is not logged in.'
        if (Read-YesNo 'Log in to GitHub now with gh auth login?') {
            & gh auth login
            $loggedIn = Test-GhLogin
        }
    }
    if ($loggedIn) {
        Write-Host 'The GitHub CLI is logged in.'
        Add-Item 'GitHub login' $true 'logged in'
        $push = (& gh api "repos/$repoSlug" --jq .permissions.push) -join ''
        if ($push.Trim() -eq 'true') {
            Write-Host "This account can push to $repoSlug."
            Add-Item 'push access' $true "can push to $repoSlug"
        }
        else {
            Write-Host "This account can't push to $repoSlug yet: ask the maintainer for access."
            Add-Item 'push access' $false 'ask the maintainer for access; everything else works meanwhile'
        }
    }
    else {
        Add-Item 'GitHub login' $false 'run: gh auth login'
        Add-Item 'push access' $false 'log in first; if you then can''t push, ask the maintainer for access'
    }

    # --- 8. Game ------------------------------------------------------------------------------------------------------
    Write-Step 'Game'
    Import-Module (Join-Path $PSScriptRoot 'GameLocator.psm1') -Force
    $locate = @{}
    if ($SteamRoot) { $locate.SteamRoot = $SteamRoot }
    if ($ReloadedConfig) { $locate.ConfigPath = $ReloadedConfig }
    $choice = Select-MetaphorGame -Ask { param($Question) Read-OnboardAnswer $Question } @locate
    $game = $null
    if ($choice) {
        $game = $choice.Path
        if (-not $choice.Saved) {
            & setx METAPHOR_GAME_EXE $game | Out-Null
            $env:METAPHOR_GAME_EXE = $game
            Write-Host 'Saved it as METAPHOR_GAME_EXE for your Windows user, so every script finds it.'
            $changes.Add('saved METAPHOR_GAME_EXE')
        }
    }
    if ($game) { Add-Item 'game path' $true 'METAPHOR_GAME_EXE is set' }
    else {
        Add-Item 'game path' 'optional' 'optional: set METAPHOR_GAME_EXE to the game''s exe, or run .\scripts\onboard.ps1 again'
    }

    $locate.Remove('SteamRoot')
    $launcher = Find-ReloadedLauncher @locate
    if ($launcher) { Write-Host "Found Reloaded-II: $launcher"; Add-Item 'Reloaded-II' $true 'found' }
    else {
        Write-Host 'Did not find Reloaded-II (no METAPHOR_RELOADED_EXE, and no launcher in its ReloadedII.json). You need'
        Write-Host 'it only to run the mod.'
        Add-Item 'Reloaded-II' 'optional' 'optional: install Reloaded-II and launch it once, to run the mod'
    }

    if ($game -and $launcher -and (Read-YesNo ('Run a 30-second test deploy? It builds the mod, starts the game through ' +
                'Reloaded-II and closes it after 30 seconds (unsaved progress is lost).'))) {
        $deployLines = @(& (Join-Path $PSScriptRoot 'listen.ps1') -GameExe $game -Seconds 30 -Close *>&1 |
                ForEach-Object { Write-Host $_; "$_" })
        $deployPassed = $LASTEXITCODE -eq 0
        $hooks = @($deployLines | Select-String 'hooks (\d+/\d+)' | ForEach-Object { $_.Matches[0].Groups[1].Value })
        $hookText = if ($hooks) { "hooks $($hooks -join ', ')" } else { 'no hook report' }
        Write-Host "Test deploy: $hookText."
        if ($deployPassed) { Add-Item 'test deploy' $true $hookText }
        else { Add-Item 'test deploy' $false "$hookText; see listen.ps1's output above" }
    }

    # --- 9. Extras ----------------------------------------------------------------------------------------------------
    if ($game) {
        Write-Step 'Extras'
        if (Read-YesNo ('Build the Ghidra project of the game now? It takes about 10 minutes and needs Ghidra and a JDK ' +
                '(docs/contributors/how-to/query-the-ghidra-project.md).')) {
            & (Join-Path $PSScriptRoot 'ghidra.ps1') build -GameExe $game
            if ($LASTEXITCODE -eq 0) { Add-Item 'Ghidra project' $true 'built' }
            else { Add-Item 'Ghidra project' $false 'see ghidra.ps1''s output above' }
        }
    }

    # --- 10. Finish line ----------------------------------------------------------------------------------------------
    Write-Step 'Checks'
    & $bash scripts/check.sh fast
    if ($LASTEXITCODE -eq 0) { Add-Item 'check.sh fast' $true 'passed' }
    else { Add-Item 'check.sh fast' $false 'failed; see above, and run bash scripts/check.sh fast in Git Bash' }

    Write-Step 'Ready summary'
    foreach ($item in $summary) {
        $mark = if ('optional' -eq $item.Ready) { '-' } elseif ($item.Ready) { $ok } else { $notOk }
        Write-Host "  $mark $($item.Name): $($item.Detail)"
    }
    Write-Host ''
    if ($changes.Count -gt 0) { Write-Host "Changed: $($changes -join '; ')." }
    else { Write-Host 'Nothing changed: everything was already set up.' }
    if (-not $interactive) {
        Write-Host 'This run had no console: every question was answered no, and Notepad and Claude Code were not started.'
        Write-Host 'Run .\scripts\onboard.ps1 in a PowerShell window to finish.'
    }

    Write-Host ''
    Write-Host 'Paste this into Claude:'
    Write-Host ''
    Write-Host '  Check my setup for this project, without installing anything: list your skills (the mattpocock-skills'
    Write-Host '  ones should be there), confirm that git config core.hooksPath is scripts/githooks, and run'
    Write-Host '  bash scripts/check.sh fast. Then read docs/agents/workflow.md and suggest an open "good first issue"'
    Write-Host "  in $repoSlug for me to start on."
    Write-Host ''
    if (-not $NoClaude -and (Test-Interactive) -and (Test-Command claude)) {
        Write-Host 'Starting Claude Code (scripts/claude.ps1).'
        & $claudeScript
    }
}
finally { Pop-Location }
if (-not $patternsReady) { exit 1 }
exit 0
