<#
.SYNOPSIS
Writes the local known-functions reference (#716) from the findings' functions blocks and hook-signatures.json.

.DESCRIPTION
Reads the "- **Functions:**" block in each finding's header (format: docs/findings/README.md#the-functions-block) and
every signature in src/Metaphor.Core/hook-signatures.json. Writes one Markdown page, grouped by area, with each row's
name, address, game build, section, use, summary, finding, and whether a signature locates it. A row whose game build
isn't the newest one in src/Metaphor.Core/known-game-builds.json says "old build: re-verify". Signatures that no
finding lists get a table of their own, and so do findings without a block (the one-time pass's to-do list).

The page is private for now: the collected names of the game's own identifiers are the reason. It goes to a git-ignored
file, by default out/known-functions.md, and the script refuses to write anywhere git would track. Never hand-edit it.

Ends with one line, "ok: ..." or "failed: ...". Exit codes: 0 ok, 1 a finding's block is malformed (each problem is
listed with its file and line), 2 the output can't be written.

.EXAMPLE
pwsh scripts/dev/known-functions.ps1
#>
#Requires -Version 7
[CmdletBinding()]
param(
    # The repository root; the default is this script's.
    [string] $Root = (Join-Path $PSScriptRoot '../..'),
    # Where the page goes; it must be git-ignored.
    [string] $OutFile = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Areas = 'Dialogue', 'Menus', 'Settings', 'Field', 'Battle', 'Speech', 'Install and Release', 'Messages and Text'
$Uses = 'hooked', 'read', 'known only'

function Write-Failed([string] $Message, [int] $Code) {
    Write-Output "failed: $Message"
    exit $Code
}

# A Markdown table cell: no line breaks, and a pipe that can't end the cell.
function Format-Cell([string] $Text) { ($Text -replace '\r?\n', ' ') -replace '\|', '\|' }

function Format-Build([string] $Build, [string] $Newest) {
    if (-not $Build) { return 'none' }
    $short = $Build.Substring(0, [Math]::Min(8, $Build.Length))
    if ($Build -eq $Newest) { $short } else { "$short (old build: re-verify)" }
}

$Root = [IO.Path]::GetFullPath($Root)
if (-not $OutFile) { $OutFile = Join-Path $Root 'out/known-functions.md' }
$OutFile = [IO.Path]::GetFullPath($OutFile, (Get-Location).Path)
$findingsDir = Join-Path $Root 'docs/findings'
$signaturesFile = Join-Path $Root 'src/Metaphor.Core/hook-signatures.json'
$buildsFile = Join-Path $Root 'src/Metaphor.Core/known-game-builds.json'
foreach ($path in $findingsDir, $signaturesFile, $buildsFile) {
    if (-not (Test-Path -LiteralPath $path)) { Write-Failed "$path is missing; is -Root the repository?" 2 }
}

# Refuse a path git would track: the page never enters the repository.
$outDir = Split-Path -Parent $OutFile
if (-not (Get-Command git -CommandType Application -ErrorAction SilentlyContinue)) {
    Write-Failed 'git is not on PATH, so whether the output is git-ignored cannot be checked' 2
}
# Without --no-index, a file already tracked (force-added) counts as not ignored, so it is refused too.
& git -C $Root check-ignore -q -- $OutFile 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Failed "$OutFile is not git-ignored here, or git already tracks it; write it under out/, untracked" 2
}

$builds = @(Get-Content -LiteralPath $buildsFile -Raw -Encoding utf8 | ConvertFrom-Json)
$newest = if ($builds.Count) { [string] $builds[-1].sha256 } else { '' }
$signatures = @(Get-Content -LiteralPath $signaturesFile -Raw -Encoding utf8 | ConvertFrom-Json)

$rows = [Collections.Generic.List[object]]::new()
$problems = [Collections.Generic.List[string]]::new()
$withoutBlock = [Collections.Generic.List[string]]::new()
$findings = @(Get-ChildItem -LiteralPath $findingsDir -Filter '*.md' -File -Force | Where-Object Name -NE 'README.md' |
        Sort-Object Name)
foreach ($finding in $findings) {
    $lines = [IO.File]::ReadAllLines($finding.FullName, [Text.UTF8Encoding]::new($false))
    $relative = "docs/findings/$($finding.Name)"
    $build = ''
    $block = $null # the line index of "- **Functions:**"
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line.StartsWith('## ')) { break } # the header ends at the first second-level heading
        if ($line -match '^- \*\*Game build:\*\*.*?([0-9a-fA-F]{64})') { $build = $Matches[1].ToLowerInvariant() }
        if ($line -match '^- \*\*Functions:\*\*\s*(.*)$') {
            if ($null -ne $block) { $problems.Add("${relative}:$($i + 1): a second functions block"); continue }
            $block = $i
            $rest = $Matches[1].Trim()
            if ($rest -and $rest -ne 'none') {
                $problems.Add("${relative}:$($i + 1): after '- **Functions:**' put nothing, or 'none'; got '$rest'")
            }
        }
    }
    if ($null -eq $block) { $withoutBlock.Add($relative); continue }
    for ($i = $block + 1; $i -lt $lines.Count -and $lines[$i] -match '^  - (.*)$'; $i++) {
        $at = "${relative}:$($i + 1)"
        $fields = $Matches[1] -split ' \| '
        if ($fields.Count -ne 6) { $problems.Add("${at}: needs six fields separated by ' | ', has $($fields.Count)"); continue }
        $name, $address, $section, $area, $use, $summary = $fields | ForEach-Object { $_.Trim() }
        if (-not $name) { $problems.Add("${at}: the name is empty") }
        if ($address -notmatch '^0x[0-9A-Fa-f]+$') { $problems.Add("${at}: the address '$address' isn't hex like 0x140123456") }
        if ($section -notmatch '^\.[A-Za-z0-9_]+$') { $problems.Add("${at}: the section '$section' isn't like .text") }
        if ($area -cnotin $Areas) { $problems.Add("${at}: the area '$area' isn't one of: $($Areas -join ', ')") }
        if ($use -cnotin $Uses) { $problems.Add("${at}: the use '$use' isn't one of: $($Uses -join ', ')") }
        if (-not $summary) { $problems.Add("${at}: the summary is empty") }
        if (-not $build) { $problems.Add("${at}: the finding's header names no game build") }
        $rows.Add([pscustomobject]@{
                Name = $name; Address = $address; Section = $section; Area = $area; Use = $use; Summary = $summary
                Build = $build; Finding = $finding.FullName
            })
    }
}
if ($problems.Count) {
    $problems | ForEach-Object { Write-Output $_ }
    Write-Failed "$($problems.Count) problem(s) in the findings' functions blocks, listed above" 1
}

$signed = @{}
foreach ($signature in $signatures) { $signed[[string] $signature.name] = $signature }
$listed = @{}
foreach ($row in $rows) { $listed[$row.Name] = $true }

function Get-Link([string] $Path) {
    $link = [IO.Path]::GetRelativePath($outDir, $Path) -replace '\\', '/'
    "[$(Split-Path -Leaf $Path)]($($link -replace ' ', '%20' -replace '\(', '%28' -replace '\)', '%29'))"
}

$page = [Text.StringBuilder]::new()
[void] $page.AppendLine('# Known functions')
[void] $page.AppendLine()
[void] $page.AppendLine('Generated by `scripts/dev/known-functions.ps1` (#716) from the findings'' functions blocks and')
[void] $page.AppendLine('`hook-signatures.json`. Private: never commit or publish it. Never edit it; rerun the script.')
[void] $page.AppendLine("Builds are the first 8 characters of the SHA-256; the newest known build is $(Format-Build $newest $newest).")
foreach ($area in $Areas) {
    $inArea = @($rows | Where-Object Area -CEQ $area | Sort-Object Name, Address)
    if (-not $inArea.Count) { continue }
    [void] $page.AppendLine()
    [void] $page.AppendLine("## $area")
    [void] $page.AppendLine()
    [void] $page.AppendLine('| Name | Address | Game build | Section | Use | Summary | Finding | Signature |')
    [void] $page.AppendLine('| --- | --- | --- | --- | --- | --- | --- | --- |')
    foreach ($row in $inArea) {
        $signature = if ($signed.ContainsKey($row.Name)) { 'yes' } else { 'no' }
        $cells = $row.Name, $row.Address, (Format-Build $row.Build $newest), $row.Section, $row.Use, $row.Summary
        [void] $page.AppendLine("| $(($cells | ForEach-Object { Format-Cell $_ }) -join ' | ') | $(Get-Link $row.Finding) | $signature |")
    }
}
$unlisted = @($signatures | Where-Object { -not $listed.ContainsKey([string] $_.name) } | Sort-Object name)
if ($unlisted.Count) {
    [void] $page.AppendLine()
    [void] $page.AppendLine('## Signatures no finding lists')
    [void] $page.AppendLine()
    [void] $page.AppendLine('| Name | Section | Game builds | Evidence |')
    [void] $page.AppendLine('| --- | --- | --- | --- |')
    foreach ($signature in $unlisted) {
        $onBuilds = (@($signature.gameBuilds) | ForEach-Object { Format-Build ([string] $_) $newest }) -join ', '
        $cells = [string] $signature.name, [string] $signature.section, $onBuilds, [string] $signature.evidence
        [void] $page.AppendLine("| $(($cells | ForEach-Object { Format-Cell $_ }) -join ' | ') |")
    }
}
if ($withoutBlock.Count) {
    [void] $page.AppendLine()
    [void] $page.AppendLine('## Findings without a functions block')
    [void] $page.AppendLine()
    foreach ($relative in $withoutBlock) { [void] $page.AppendLine("- $(Get-Link (Join-Path $Root $relative))") }
}

# A temporary file beside the output, then a rename, so a failed write never leaves half a page.
try {
    [void] [IO.Directory]::CreateDirectory($outDir)
    $temp = "$OutFile.tmp"
    [IO.File]::WriteAllText($temp, $page.ToString(), [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temp -Destination $OutFile -Force
} catch {
    Write-Failed "could not write ${OutFile}: $($_.Exception.Message)" 2
}
Write-Output ("ok: wrote $($rows.Count) functions from $($findings.Count - $withoutBlock.Count) findings with a block " +
    "and $($unlisted.Count) unlisted signatures to $OutFile; $($withoutBlock.Count) findings have no block")
