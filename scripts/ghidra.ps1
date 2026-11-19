<#
.SYNOPSIS
The local Ghidra project of the game exe (#290): build it once per game build, then ask it questions headlessly.

.DESCRIPTION
build   Creates the project for the current game build in the git-ignored ghidra/ folder, or reuses it when that
        build's project is already complete. Imports the exe from disk, runs auto-analysis on everything but the
        protection's sections (tools/ghidra/MetaphorBuild.java), names each hook-signatures.json match, then names
        virtual functions after the RTTI vtables they sit in. On a complete project it only names the signatures
        added since. -Rebuild starts over.
<query> One of decompile, disasm, callers, callees, offset, global, rtti, vtable (tools/ghidra/MetaphorQuery.java lists
        what each takes). Opens the project read-only, so a query never changes it. Prints the answer and keeps it in
        ghidra/out/.

Only the exe on disk is read: nothing is launched, attached to, decrypted or unpacked (AGENTS.md rule 5,
docs/raw-sources/drm-status-2026-11-13.md). The project holds the exe's bytes and the answers hold decompiled code, so
both stay in ghidra/, which .gitignore and the checks keep out of the repository (rule 4).

Needs Ghidra and a JDK it supports: GHIDRA_INSTALL_DIR and JAVA_HOME, or else the newest ghidra_*_PUBLIC and jdk-*
folder in %LOCALAPPDATA%\Programs (docs/contributors/how-to/query-the-ghidra-project.md). Ghidra locks the project, so
one command runs at a time.

Exit codes: 0 done; 1 Ghidra or a script failed; 2 couldn't start (no Ghidra, JDK, game exe or project).

.EXAMPLE
pwsh scripts/ghidra.ps1 build
pwsh scripts/ghidra.ps1 decompile 0x21e1c0
pwsh scripts/ghidra.ps1 callers CursorManagerInput
pwsh scripts/ghidra.ps1 offset 0x48 CursorManagerInput
pwsh scripts/ghidra.ps1 global 0x2f04960
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('build', 'decompile', 'disasm', 'callers', 'callees', 'offset', 'global', 'rtti', 'vtable')]
    [string] $Command,
    # The query's target: an RVA such as 0x21e1c0, a full address, or a symbol name. For offset, the displacement.
    [Parameter(Position = 1)] [string] $Target,
    # For offset: the function to search in (default: all analysed code).
    [Parameter(Position = 2)] [string] $In,
    # The game executable. Defaults to METAPHOR_GAME_EXE, then to the game as registered in Reloaded-II.
    [string] $GameExe = $env:METAPHOR_GAME_EXE,
    # build: delete this build's project and start over.
    [switch] $Rebuild,
    # Java heap for Ghidra. Analysis of the exe needs more than Ghidra's 2G default.
    [string] $MaxMemory = '16G',
    [string] $ReloadedConfig = (Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Reloaded-Mod-Loader-II/ReloadedII.json'),
    [string] $KnownBuilds = (Join-Path $PSScriptRoot '../src/Metaphor.Core/known-game-builds.json'),
    [string] $Signatures = (Join-Path $PSScriptRoot '../src/Metaphor.Core/hook-signatures.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReloadedDev.psm1') -Force

$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
# One project for every worktree, and the only folder these tools write to: the main checkout's ghidra/ folder (its
# git dir's parent), which .gitignore and the checks' game-folder rule cover. Ghidra also refuses a project path with a
# folder starting with '.', as worktrees under .claude/ have.
$common = git -C $repo rev-parse --path-format=absolute --git-common-dir
if ($LASTEXITCODE -ne 0) { $common = Join-Path $repo '.git' }
$root = [IO.Path]::GetFullPath((Join-Path (Split-Path $common) 'ghidra'))
$scripts = Join-Path $repo 'tools/ghidra'
$projectName = 'metaphor'
$readyFile = 'ready.txt'

$folders = [ordered]@{}
function Write-Out([string] $Text) { Write-Host (Hide-LocalPaths $folders $Text) }
function Stop-Start([string] $Message) { Write-Out "ghidra can't start: $Message"; exit 2 }

# The newest folder matching $Pattern under %LOCALAPPDATA%\Programs, when the variable isn't set.
function Find-Install([string] $Variable, [string] $Pattern, [string] $Probe) {
    $value = [Environment]::GetEnvironmentVariable($Variable)
    if (-not $value) {
        $value = Get-ChildItem -LiteralPath (Join-Path $env:LOCALAPPDATA 'Programs') -Directory -Filter $Pattern -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $value -or -not (Test-Path -LiteralPath (Join-Path $value $Probe))) {
        Stop-Start "no ${Probe}: set $Variable (docs/contributors/how-to/query-the-ghidra-project.md)"
    }
    $value
}

$ghidra = Find-Install 'GHIDRA_INSTALL_DIR' 'ghidra_*_PUBLIC' 'support/analyzeHeadless.bat'
$env:JAVA_HOME = Find-Install 'JAVA_HOME' 'jdk-*' 'bin/java.exe'
$env:GHIDRA_HEADLESS_MAXMEM = $MaxMemory
Add-PathName $folders '<ghidra-project>' $root
Add-PathName $folders '<repo>' $repo
Add-PathName $folders '<ghidra>' $ghidra
Add-PathName $folders '<jdk>' $env:JAVA_HOME

if (-not $GameExe -and (Test-Path -LiteralPath $ReloadedConfig)) {
    $GameExe = (Resolve-LoaderPaths (Read-Json $ReloadedConfig) $null $null).GameExe
}
if (-not $GameExe -or -not (Test-Path -LiteralPath $GameExe)) {
    Stop-Start 'no game executable: set -GameExe or METAPHOR_GAME_EXE, or register the game in Reloaded-II'
}
Add-PathName $folders '<game>' (Split-Path $GameExe)

$build = Get-GameBuild $GameExe $KnownBuilds
$project = Join-Path $root $build.Sha256.Substring(0, 16)
$programName = Split-Path $GameExe -Leaf
New-Item -ItemType Directory -Force (Join-Path $root 'out'), (Join-Path $root 'logs') | Out-Null
# The folder ignores itself, so it stays out of git on a branch whose .gitignore predates it.
Set-Content -LiteralPath (Join-Path $root '.gitignore') -Value '*' -Encoding ascii

# Runs analyzeHeadless; true when it exited 0 and its script log names no script error.
function Invoke-Headless([string] $Name, [string[]] $Arguments) {
    $log = Join-Path $root "logs/$Name.log"
    $scriptLog = Join-Path $root "logs/$Name-script.log"
    Remove-Item -LiteralPath $log, $scriptLog -ErrorAction SilentlyContinue
    $all = @($project, $projectName) + $Arguments + @('-scriptPath', $scripts, '-log', $log, '-scriptlog', $scriptLog)
    & (Join-Path $ghidra 'support/analyzeHeadless.bat') @all *> $null
    $code = $LASTEXITCODE
    $errors = @(Select-String -LiteralPath $log, $scriptLog -Pattern 'SCRIPT ERROR|Abort due to|OutOfMemoryError|Save failed' -ErrorAction SilentlyContinue)
    if ($code -ne 0 -or $errors) {
        Write-Out "ghidra failed (exit $code); log: $log"
        $errors | Select-Object -First 10 | ForEach-Object { Write-Out "  $($_.Line)" }
        return $false
    }
    $true
}

if ($Command -eq 'build') {
    $known = if ($build.Known) { 'a known build' } else { 'not in known-game-builds.json' }
    Write-Out "game build $($build.Sha256) ($known)"
    $ready = Join-Path $project $readyFile
    # One headless run per step, so each saves its work and gets a fresh heap.
    $steps = [ordered]@{
        analyse = @('-import', $GameExe, '-noanalysis', '-postScript', 'MetaphorBuild.java', 'analyse')
        names   = @('-process', $programName, '-noanalysis', '-postScript', 'MetaphorBuild.java', 'names',
            ([IO.Path]::GetFullPath($Signatures)), $build.Sha256)
    }
    $steps.vtables = @('-process', $programName, '-noanalysis', '-postScript', 'MetaphorBuild.java', 'vtables')
    # Runs one step and prints what its script reported; exits 1 when it fails.
    function Invoke-Step([string] $Step) {
        $stepStart = Get-Date
        if (-not (Invoke-Headless "build-$Step" $steps[$Step])) { exit 1 }
        Select-String -LiteralPath (Join-Path $root "logs/build-$Step-script.log") -Pattern '\.java> ' |
            Select-Object -Last 8 | ForEach-Object { Write-Out "  $($_.Line -replace '^.*\.java> ', '')" }
        Write-Out "  $Step done in $([math]::Round(((Get-Date) - $stepStart).TotalMinutes, 1)) min"
    }
    if ($Rebuild -and (Test-Path -LiteralPath $project)) { Remove-Item -LiteralPath $project -Recurse -Force }
    if (Test-Path -LiteralPath $ready) {
        Write-Out "reusing ghidra/$(Split-Path $project -Leaf) (built $((Get-Content -LiteralPath $ready -TotalCount 1)))"
        # Signatures added since it was built get their names; analysis and vtable names stay as built.
        Invoke-Step 'names'
        exit 0
    }
    # A project without its ready file is a build that stopped part-way: start it over.
    if (Test-Path -LiteralPath $project) { Remove-Item -LiteralPath $project -Recurse -Force }
    New-Item -ItemType Directory -Force $project | Out-Null
    $started = Get-Date
    foreach ($step in $steps.Keys) { Invoke-Step $step }
    $minutes = [math]::Round(((Get-Date) - $started).TotalMinutes, 1)
    Set-Content -LiteralPath $ready -Value "$((Get-Date).ToString('u')) in $minutes min"
    Write-Out "built ghidra/$(Split-Path $project -Leaf) in $minutes min"
    exit 0
}

if (-not $Target) { Stop-Start "$Command needs a target" }
if (-not (Test-Path -LiteralPath (Join-Path $project $readyFile))) {
    Stop-Start "no complete project for build $($build.Sha256.Substring(0, 16)): run scripts/ghidra.ps1 build"
}
$safe = ($Command, $Target, $In | Where-Object { $_ }) -join '-' -replace '[^A-Za-z0-9_.-]', '_'
$out = Join-Path $root "out/$safe.txt"
Remove-Item -LiteralPath $out -ErrorAction SilentlyContinue
$arguments = @('-process', $programName, '-readOnly', '-noanalysis', '-postScript', 'MetaphorQuery.java', $out, $Command, $Target)
if ($In) { $arguments += $In }
$ok = Invoke-Headless 'query' $arguments
if (Test-Path -LiteralPath $out) { Get-Content -LiteralPath $out | ForEach-Object { Write-Out $_ } }
# A bad target (no such symbol, not code) is one "# error:" line from MetaphorQuery.java.
if (-not $ok -or (Select-String -LiteralPath $out -Pattern '^# error: ' -Quiet -ErrorAction SilentlyContinue)) { exit 1 }
