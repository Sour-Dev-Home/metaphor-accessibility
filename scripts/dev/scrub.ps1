<#
.SYNOPSIS
The chapter scrub (#516): a gameplay video to a few tiled contact sheets and a manifest, for a chapter's interaction
inventory.

.DESCRIPTION
For each segment of the video (a chapter from -Chapters, a speech or quiet stretch from -Captions, or the whole video),
cut to -Range: picks frames at scene changes, at least -MinSpacing seconds apart and at most -MaxGap apart; drops
near-duplicates by dHash; when Ollama with qwen2.5vl:7b answers on this machine, labels each frame and drops loading
screens (without it, the run says so and goes on); and tiles the rest into sheets with each frame's time burned in.
Each segment is cached by the video and the settings, so a rerun skips it; -Force redoes it.

Everything is written under METAPHOR_SCRUB_DIR, which must be set, and must be outside any git repository: frames,
sheets and captions are game content and never enter the repository. The manifest, <METAPHOR_SCRUB_DIR>/<name>/
manifest.json, lists each segment's captions file, sheets, frame times and labels. How to use it:
docs/contributors/how-to/scrub-a-chapter.md.

Needs ffmpeg and ffprobe on PATH. Ends with one line, "ok: ..." or "failed: ... - what to do". Exit codes: 0 ok, 2
failed. The work is in scripts/dev/ChapterScrub.psm1.

.EXAMPLE
$env:METAPHOR_SCRUB_DIR = 'D:\scrub'; pwsh scripts/dev/scrub.ps1 -Video D:\capture\chapter1.mkv

.EXAMPLE
pwsh scripts/dev/scrub.ps1 -Video part1.mp4 -Chapters chapters.txt -Chapter 'The king*' -Captions part1.en.vtt -Range 0:00-40:00
#>
[CmdletBinding()]
param(
    # The video file.
    [Parameter(Mandatory)] [string] $Video,
    # The run's folder under METAPHOR_SCRUB_DIR (default the video's file name).
    [string] $Name,
    # Chapter markers, one per line as "0:00 Title"; -Chapter keeps those whose title matches a pattern (* any text).
    [string] $Chapters,
    [string[]] $Chapter,
    # A .vtt or .srt caption file: its text is written beside each segment's sheets, and without -Chapters its gaps of
    # -CaptionGap seconds or more (default 30) cut the video into speech and quiet segments.
    [string] $Captions,
    [double] $CaptionGap,
    # Only these parts of the video, each start-end (h:mm:ss, m:ss or seconds).
    [string[]] $Range,
    # Scene-change score that picks a frame, 0 to 1 (default 0.3); seconds between picks, at least (default 2) and at
    # most (default 60).
    [double] $SceneThreshold,
    [double] $MinSpacing,
    [double] $MaxGap,
    # Frame height in pixels, 480 to 720 (default 540).
    [int] $Height,
    # Frames whose dHashes differ in at most this many of 64 bits count as one (default 5; -1 keeps every frame).
    [int] $HashDistance,
    # Sheet layout, columns x rows (default 3x3).
    [string] $Tile,
    # No labels, even when Ollama answers; Ollama's address on this machine (default http://127.0.0.1:11434).
    [switch] $NoLabels,
    [string] $OllamaUrl,
    # Redo every segment, cached or not.
    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ChapterScrub.psm1') -Force

$options = @{}
foreach ($key in $PSBoundParameters.Keys) {
    if ($key -notin [Management.Automation.PSCmdlet]::CommonParameters) { $options[$key] = $PSBoundParameters[$key] }
}
exit (Invoke-ChapterScrub $options)
