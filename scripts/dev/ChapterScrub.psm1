# The chapter scrub (#516): turns a gameplay video into a few tiled contact sheets and a manifest, so an agent can write
# a chapter's interaction inventory from about one image per nine frames (docs/contributors/how-to/scrub-a-chapter.md).
# Behind scripts/dev/scrub.ps1. Doesn't ship. Every file it writes goes under METAPHOR_SCRUB_DIR, which must be outside
# any git repository: frames, sheets and captions are game content (AGENTS.md rule 4).
#
# Per segment: one ffmpeg pass picks frames at scene changes (select's scene score, with a minimum spacing and a
# maximum gap), scales them, burns the timestamp in and writes a 9x8 grey copy of each for its dHash; near-duplicates
# are dropped; a local Ollama, when reachable, labels each frame; a second pass tiles the rest into sheets. ffmpeg is
# always called with an argument list, never a command string. Ollama is only ever asked on a loopback address.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$WorkDirVariable = 'METAPHOR_SCRUB_DIR'
$Invariant = [Globalization.CultureInfo]::InvariantCulture
$Labels = 'dialogue', 'choice', 'menu', 'shop', 'field', 'map-travel', 'battle-command', 'battle-action', 'tutorial',
'minigame', 'cutscene', 'loading', 'other'
$DroppedLabels = @('loading')
$DefaultModel = 'qwen2.5vl:7b'
$DefaultOllamaUrl = 'http://127.0.0.1:11434'
# Bumped when a change makes cached segments wrong, so they are redone.
$CacheVersion = 1

$Defaults = @{
    SceneThreshold = 0.3; MinSpacing = 2.0; MaxGap = 60.0; Height = 540; HashDistance = 5; Tile = '3x3'; CaptionGap = 30.0
}
$KnownOptions = 'Video', 'Name', 'Chapters', 'Chapter', 'Captions', 'Range', 'CaptionGap', 'SceneThreshold', 'MinSpacing',
'MaxGap', 'Height', 'HashDistance', 'Tile', 'NoLabels', 'OllamaUrl', 'Force'

class ScrubStop : Exception {
    [string] $Fix
    ScrubStop([string] $Message, [string] $Fix) : base($Message) { $this.Fix = $Fix }
}

function Stop-Scrub([string] $Message, [string] $Fix) { throw [ScrubStop]::new($Message, $Fix) }

function Get-Option([hashtable] $Options, [string] $Name) {
    $value = $Options[$Name]
    if ($null -eq $value -or ($value -is [string] -and $value -eq '')) { $Defaults[$Name] } else { $value }
}

# "1:02:03.5", "2:03" or "123" to seconds.
function ConvertTo-Seconds([string] $Text) {
    $parts = $Text.Trim() -split ':'
    if ($parts.Count -gt 3 -or ($parts | Where-Object { $_ -notmatch '^\d+(\.\d+)?$' })) {
        Stop-Scrub "'$Text' isn't a time" 'write times as h:mm:ss, m:ss or seconds'
    }
    $seconds = 0.0
    foreach ($part in $parts) { $seconds = $seconds * 60 + [double]::Parse($part, $Invariant) }
    $seconds
}

function Format-Time([double] $Seconds) {
    $whole = [int][Math]::Floor($Seconds)
    $h = [int][Math]::Floor($whole / 3600); $m = [int][Math]::Floor(($whole % 3600) / 60); $s = $whole % 60
    if ($h) { '{0}:{1:00}:{2:00}' -f $h, $m, $s } else { '{0}:{1:00}' -f $m, $s }
}

function Read-Utf8Lines([string] $Path) { [IO.File]::ReadAllLines($Path, [Text.UTF8Encoding]::new($false)) }

# Chapter markers as a video description lists them, one per line: "0:00 Title" or "1:02:03 - Title".
function Read-Chapters([string] $Path) {
    $chapters = foreach ($line in Read-Utf8Lines $Path) {
        if ($line -match '^\s*((?:\d+:)?\d+:\d{2})\s*[-–:]?\s*(.+?)\s*$') {
            [pscustomobject]@{ Start = ConvertTo-Seconds $Matches[1]; Title = $Matches[2] }
        }
    }
    $chapters = @($chapters | Sort-Object Start)
    if (-not $chapters) { Stop-Scrub 'the chapters file has no "m:ss Title" lines' 'write one chapter per line, as "0:00 Title"' }
    $chapters
}

# WebVTT or SubRip cues: start, end and text, tags removed, a line repeated by rolling captions kept once.
function Read-Captions([string] $Path) {
    $cues = [Collections.Generic.List[object]]::new()
    $current = $null
    $time = '(?:(\d+):)?(\d{1,2}):(\d{2})[.,](\d{3})'
    foreach ($line in Read-Utf8Lines $Path) {
        if ($line -match "^\s*$time\s*-->\s*$time") {
            $start = [int]$(if ($Matches[1]) { $Matches[1] } else { 0 }) * 3600 + [int]$Matches[2] * 60 + [int]$Matches[3] + [int]$Matches[4] / 1000
            $end = [int]$(if ($Matches[5]) { $Matches[5] } else { 0 }) * 3600 + [int]$Matches[6] * 60 + [int]$Matches[7] + [int]$Matches[8] / 1000
            $current = [pscustomobject]@{ Start = [double]$start; End = [double]$end; Lines = [Collections.Generic.List[string]]::new() }
            $cues.Add($current)
        }
        elseif ($line.Trim() -eq '') { $current = $null }
        elseif ($current) {
            $text = ($line -replace '<[^>]*>', '').Trim()
            if ($text) { $current.Lines.Add($text) }
        }
    }
    $seen = $null
    foreach ($cue in $cues) {
        $lines = @($cue.Lines | Where-Object { $_ -ne $seen })
        if ($cue.Lines.Count) { $seen = $cue.Lines[$cue.Lines.Count - 1] }
        if ($lines) { [pscustomobject]@{ Start = $cue.Start; End = $cue.End; Text = $lines -join ' ' } }
    }
}

# Speech and quiet stretches from caption cues: a gap of at least $Gap seconds between cues is its own "quiet" segment,
# where play without text (menus, battles, movement) most likely is.
function Get-CaptionSegments([object[]] $Cues, [double] $Duration, [double] $Gap) {
    $segments = [Collections.Generic.List[object]]::new()
    $start = 0.0; $lastEnd = 0.0
    foreach ($cue in $Cues) {
        if ($cue.Start - $lastEnd -ge $Gap) {
            if ($lastEnd -gt $start) { $segments.Add([pscustomobject]@{ Title = 'speech'; Start = $start; End = $lastEnd }) }
            $segments.Add([pscustomobject]@{ Title = 'quiet'; Start = $lastEnd; End = $cue.Start })
            $start = $cue.Start
        }
        $lastEnd = [Math]::Max($lastEnd, $cue.End)
    }
    # A short tail after the last cue stays with its speech, so no part of the video is left out.
    if ($Duration - $lastEnd -ge $Gap) {
        if ($lastEnd -gt $start) { $segments.Add([pscustomobject]@{ Title = 'speech'; Start = $start; End = $lastEnd }) }
        $segments.Add([pscustomobject]@{ Title = 'quiet'; Start = $lastEnd; End = $Duration })
    }
    else { $segments.Add([pscustomobject]@{ Title = 'speech'; Start = $start; End = $Duration }) }
    $segments
}

# The segments to scrub: chapters (or caption stretches, or the whole video), kept by -Chapter, cut to -Range.
function Get-Segments([double] $Duration, [object[]] $Chapters, [string[]] $ChapterFilter, [object[]] $Cues, [double] $CaptionGap, [string[]] $Ranges) {
    if ($Chapters) {
        $base = for ($i = 0; $i -lt $Chapters.Count; $i++) {
            $end = if ($i + 1 -lt $Chapters.Count) { $Chapters[$i + 1].Start } else { $Duration }
            [pscustomobject]@{ Title = $Chapters[$i].Title; Start = $Chapters[$i].Start; End = [Math]::Min($end, $Duration) }
        }
        if ($ChapterFilter) {
            $base = @($base | Where-Object { $title = $_.Title; $ChapterFilter | Where-Object { $title -like $_ } })
            if (-not $base) { Stop-Scrub 'no chapter title matches -Chapter' 'check the patterns against the chapters file; * matches any text' }
        }
    }
    elseif ($ChapterFilter) { Stop-Scrub '-Chapter needs -Chapters' 'pass the chapters file too' }
    elseif ($Cues) { $base = Get-CaptionSegments $Cues $Duration $CaptionGap }
    else { $base = @([pscustomobject]@{ Title = 'video'; Start = 0.0; End = $Duration }) }

    if ($Ranges) {
        $cuts = foreach ($range in $Ranges) {
            $ends = $range -split '-'
            if ($ends.Count -ne 2) { Stop-Scrub "'$range' isn't a range" 'write a range as start-end, as 5:00-20:00' }
            $from = ConvertTo-Seconds $ends[0]; $to = ConvertTo-Seconds $ends[1]
            if ($to -le $from) { Stop-Scrub "the range '$range' ends before it starts" 'put the earlier time first' }
            [pscustomobject]@{ Start = $from; End = $to }
        }
        $base = foreach ($segment in $base) {
            foreach ($cut in $cuts) {
                $start = [Math]::Max($segment.Start, $cut.Start); $end = [Math]::Min($segment.End, $cut.End)
                if ($end -gt $start) { [pscustomobject]@{ Title = $segment.Title; Start = $start; End = $end } }
            }
        }
    }
    $base = @($base | Where-Object { $_.End - $_.Start -gt 0.05 })
    if (-not $base) { Stop-Scrub 'no part of the video is left to scrub' 'check -Range against the video''s length' }
    $base
}

function Get-Sha256Hex([byte[]] $Bytes) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant() }

# A fast identity for a large video: its length and its first and last 4 MiB.
function Get-InputKey([string] $Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        $chunk = 4MB
        $buffer = [IO.MemoryStream]::new()
        $length = [BitConverter]::GetBytes([long]$stream.Length)
        $buffer.Write($length, 0, $length.Length)
        foreach ($offset in 0, [Math]::Max(0, $stream.Length - $chunk)) {
            $stream.Position = $offset
            $bytes = [byte[]]::new([Math]::Min($chunk, $stream.Length))
            $read = $stream.Read($bytes, 0, $bytes.Length)
            $buffer.Write($bytes, 0, $read)
        }
        Get-Sha256Hex $buffer.ToArray()
    }
    finally { $stream.Dispose() }
}

# dHash: each of the 8 rows of a 9x8 grey image gives 8 bits, one per pixel brighter than its right-hand neighbour.
function Get-DHash([byte[]] $Pixels, [int] $Offset = 0) {
    [uint64] $hash = 0
    for ($row = 0; $row -lt 8; $row++) {
        for ($col = 0; $col -lt 8; $col++) {
            $i = $Offset + $row * 9 + $col
            $hash = ($hash -shl 1) -bor [uint64]($Pixels[$i] -gt $Pixels[$i + 1])
        }
    }
    $hash
}

function Get-HammingDistance([uint64] $A, [uint64] $B) { [Numerics.BitOperations]::PopCount($A -bxor $B) }

# The one place ffmpeg and ffprobe run: an argument list, never a string. Returns stdout; throws on a non-zero exit.
function Invoke-Ffmpeg([string] $Tool, [string[]] $Arguments) {
    $exe = (Get-Command $Tool -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1)
    if (-not $exe) { Stop-Scrub "$Tool isn't on PATH" 'install FFmpeg (winget install Gyan.FFmpeg) and open a new terminal' }
    $errors = [IO.Path]::GetTempFileName()
    try {
        $out = & $exe.Source @Arguments 2> $errors
        if ($LASTEXITCODE -ne 0) {
            $tail = (Get-Content -LiteralPath $errors -Tail 3) -join ' '
            Stop-Scrub "$Tool exited with $LASTEXITCODE ($tail)" 'check the video plays, then run again'
        }
        $out -join "`n"
    }
    finally { Remove-Item -LiteralPath $errors -Force -ErrorAction SilentlyContinue }
}

function Get-VideoDuration([string] $Video) {
    $json = Invoke-Ffmpeg 'ffprobe' @('-v', 'error', '-show_entries', 'format=duration', '-of', 'json', $Video) | ConvertFrom-Json
    [double]::Parse($json.format.duration, $Invariant)
}

# Windows' Arial for the burned-in timestamps (FFmpeg's Windows builds have no font configuration). Escaped for a
# filter graph: forward slashes, and the drive's colon escaped.
function Get-FontFilterPath {
    $windows = [Environment]::GetFolderPath('Windows')
    if (-not $windows) { return $null }
    $font = Join-Path $windows 'Fonts/arial.ttf'
    if (-not (Test-Path -LiteralPath $font)) { return $null }
    "'" + ($font -replace '\\', '/' -replace ':', '\:') + "'"
}

function Test-LoopbackUrl([string] $Url) {
    $uri = $null
    [Uri]::TryCreate($Url, [UriKind]::Absolute, [ref] $uri) -and $uri.Scheme -eq 'http' -and $uri.IsLoopback
}

# $null when Ollama answers with the model pulled, or why not.
function Test-Ollama([string] $Url, [string] $Model) {
    # No redirects, here or below: one could send a frame off this machine after the loopback check passed.
    try { $tags = Invoke-RestMethod -Uri "$Url/api/tags" -Method Get -TimeoutSec 3 -MaximumRedirection 0 }
    catch { return "Ollama isn't reachable at $Url" }
    $names = @($tags.models | ForEach-Object { $_.name })
    if ($Model -notin $names) { return "Ollama has no $Model (ollama pull $Model)" }
    $null
}

function Get-FrameLabel([string] $Url, [string] $Model, [string] $Frame) {
    $prompt = "This is a frame from a role-playing video game. Answer with exactly one word from this list, nothing else: $($Labels -join ', ')."
    $body = @{
        model = $Model; prompt = $prompt; stream = $false; options = @{ temperature = 0 }
        images = @([Convert]::ToBase64String([IO.File]::ReadAllBytes($Frame)))
    } | ConvertTo-Json -Depth 4
    $answer = Invoke-RestMethod -Uri "$Url/api/generate" -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 120 -MaximumRedirection 0
    $word = ([string]$answer.response).Trim().ToLowerInvariant() -replace '[^a-z-]', ''
    if ($word -in $Labels) { $word } else { 'other' }
}

function ConvertTo-InvariantText($Number) { ([double]$Number).ToString($Invariant) }

function Write-JsonFile([string] $Path, $Value) {
    $temp = "$Path.tmp"
    [IO.File]::WriteAllText($temp, ($Value | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temp -Destination $Path -Force
}

# One segment: frames, dedupe, labels, sheets. Writes segment.json last, so a folder without it is redone.
function Invoke-Segment($Segment, [string] $Video, [string] $Folder, [hashtable] $Settings, $Ollama) {
    if (Test-Path -LiteralPath $Folder) { Remove-Item -LiteralPath $Folder -Recurse -Force }
    $frames = Join-Path $Folder 'frames'
    New-Item -ItemType Directory -Force $frames | Out-Null
    $grey = Join-Path $Folder 'dhash.gray'

    $t = { param($x) ConvertTo-InvariantText $x }
    $select = "select='isnan(prev_selected_t)+gt(scene\,$(& $t $Settings.SceneThreshold))*gte(t-prev_selected_t\,$(& $t $Settings.MinSpacing))+gte(t-prev_selected_t\,$(& $t $Settings.MaxGap))'"
    $big = "scale=-2:$($Settings.Height)"
    $font = Get-FontFilterPath
    if ($font) {
        $big += ",drawtext=fontfile=${font}:text='%{pts\:hms\:$(& $t $Segment.Start)}':x=8:y=8:fontsize=h/16:fontcolor=white:box=1:boxcolor=black@0.7:boxborderw=4"
    }
    $graph = "[0:v]$select,split=2[a][b];[a]$big[big];[b]scale=9:8:flags=area,format=gray[small]"
    Invoke-Ffmpeg 'ffmpeg' @(
        '-hide_banner', '-nostdin', '-loglevel', 'error', '-y',
        '-ss', (& $t $Segment.Start), '-to', (& $t $Segment.End), '-i', $Video,
        '-filter_complex', $graph,
        '-map', '[big]', '-fps_mode', 'vfr', '-enc_time_base:v', '1/1000', '-frame_pts', '1', '-q:v', '4', (Join-Path $frames '%d.jpg'),
        '-map', '[small]', '-fps_mode', 'vfr', '-f', 'rawvideo', $grey
    ) | Out-Null

    # Each frame is named by its time in ms from the segment's start, in the order the hashes were written.
    $files = @(Get-ChildItem -LiteralPath $frames -File -Force -Filter '*.jpg' | Sort-Object { [long]$_.BaseName })
    $pixels = [IO.File]::ReadAllBytes($grey)
    if ($pixels.Length -ne 72 * $files.Count) { Stop-Scrub 'ffmpeg wrote a different number of frames and hashes' 'run again with -Force' }
    Remove-Item -LiteralPath $grey -Force

    $kept = [Collections.Generic.List[object]]::new()
    $duplicates = 0
    for ($i = 0; $i -lt $files.Count; $i++) {
        $hash = Get-DHash $pixels ($i * 72)
        $near = $Settings.HashDistance -ge 0 -and ($kept | Where-Object { (Get-HammingDistance $_.Hash $hash) -le $Settings.HashDistance })
        if ($near) { Remove-Item -LiteralPath $files[$i].FullName -Force; $duplicates++; continue }
        $time = $Segment.Start + [long]$files[$i].BaseName / 1000
        $kept.Add([pscustomobject]@{ Time = $time; Hash = $hash; File = $files[$i].FullName; Label = $null })
    }

    $dropped = 0
    if ($Ollama) {
        foreach ($frame in $kept) {
            $frame.Label = try { Get-FrameLabel $Ollama.Url $Ollama.Model $frame.File } catch { 'unlabelled' }
        }
        $drop = @($kept | Where-Object { $_.Label -in $DroppedLabels })
        foreach ($frame in $drop) { Remove-Item -LiteralPath $frame.File -Force; [void]$kept.Remove($frame); $dropped++ }
    }

    # Tile: the kept frames copied into an unbroken sequence, which ffmpeg's tile filter lays out row by row.
    $columns, $rows = [int[]]($Settings.Tile -split 'x')
    $perSheet = $columns * $rows
    $sequence = Join-Path $Folder 'sequence'
    New-Item -ItemType Directory -Force $sequence | Out-Null
    for ($i = 0; $i -lt $kept.Count; $i++) { Copy-Item -LiteralPath $kept[$i].File -Destination (Join-Path $sequence ('{0:0000}.jpg' -f ($i + 1))) }
    if ($kept.Count) {
        Invoke-Ffmpeg 'ffmpeg' @(
            '-hide_banner', '-nostdin', '-loglevel', 'error', '-y', '-framerate', '1', '-i', (Join-Path $sequence '%04d.jpg'),
            '-vf', "tile=$($Settings.Tile):padding=6:margin=6", '-fps_mode', 'vfr', '-q:v', '4', (Join-Path $Folder 'sheet-%03d.jpg')
        ) | Out-Null
    }
    Remove-Item -LiteralPath $sequence -Recurse -Force

    $sheets = for ($s = 0; $s * $perSheet -lt $kept.Count; $s++) {
        $onSheet = @($kept | Select-Object -Skip ($s * $perSheet) -First $perSheet)
        [ordered]@{ file = 'sheet-{0:000}.jpg' -f ($s + 1); times = @($onSheet | ForEach-Object { Format-Time $_.Time }) }
    }
    $record = [ordered]@{
        cacheVersion = $CacheVersion
        title = $Segment.Title; start = $Segment.Start; end = $Segment.End
        frames = @($kept | ForEach-Object {
                [ordered]@{ time = $_.Time; timeText = Format-Time $_.Time; label = $_.Label; file = 'frames/' + (Split-Path $_.File -Leaf) }
            })
        sheets = @($sheets)
        dropped = [ordered]@{ duplicate = $duplicates; label = $dropped }
        labelled = [bool]$Ollama
    }
    Write-JsonFile (Join-Path $Folder 'segment.json') $record
    [pscustomobject]$record
}

function Test-InsideGitRepository([string] $Path) {
    $dir = [IO.DirectoryInfo]::new($Path)
    while ($dir) {
        if (Test-Path -LiteralPath (Join-Path $dir.FullName '.git')) { return $true }
        $dir = $dir.Parent
    }
    $false
}

function Invoke-Scrub([hashtable] $Options) {
    $extra = @($Options.Keys | Where-Object { $_ -notin $KnownOptions } | Sort-Object)
    if ($extra) { Stop-Scrub "scrub doesn't take -$($extra -join ', -')" 'see: Get-Help scripts/dev/scrub.ps1 -Full' }

    $work = [Environment]::GetEnvironmentVariable($WorkDirVariable)
    if (-not $work) {
        Stop-Scrub "$WorkDirVariable isn't set" "set it to a folder on a drive with room for video frames, outside any git repository, as `$env:$WorkDirVariable = 'D:\scrub'"
    }
    if (-not [IO.Path]::IsPathFullyQualified($work)) { Stop-Scrub "$WorkDirVariable isn't a full path" 'set it to a full path, with its drive' }
    # Checked before the folder is made, so a refused one is never created inside a repository.
    $work = [IO.Path]::GetFullPath($work)
    if (Test-InsideGitRepository $work) {
        Stop-Scrub "$WorkDirVariable is inside a git repository" 'frames and captions are game content; pick a folder outside any repository'
    }
    New-Item -ItemType Directory -Force $work | Out-Null

    $video = $Options['Video']
    if (-not $video -or -not (Test-Path -LiteralPath $video -PathType Leaf)) { Stop-Scrub 'no video at -Video' 'pass the path of the video file' }
    $video = (Resolve-Path -LiteralPath $video).ProviderPath

    $settings = @{}
    foreach ($name in 'SceneThreshold', 'MinSpacing', 'MaxGap', 'HashDistance', 'Height', 'Tile', 'CaptionGap') { $settings[$name] = Get-Option $Options $name }
    $settings.SceneThreshold = [double]$settings.SceneThreshold
    $settings.MinSpacing = [double]$settings.MinSpacing
    $settings.MaxGap = [double]$settings.MaxGap
    $settings.HashDistance = [int]$settings.HashDistance
    $settings.Height = [int]$settings.Height
    if ($settings.Height -lt 480 -or $settings.Height -gt 720) { Stop-Scrub '-Height is outside 480 to 720' 'small frames are the point: pick 480 to 720' }
    if ($settings.Tile -notmatch '^[1-9]x[1-9]$') { Stop-Scrub "-Tile '$($settings.Tile)' isn't columns x rows" 'write it as 3x3' }
    if ($settings.SceneThreshold -le 0 -or $settings.SceneThreshold -ge 1) { Stop-Scrub '-SceneThreshold is outside 0 to 1' 'try 0.3' }

    $name = Get-Option $Options 'Name'
    if (-not $name) { $name = [IO.Path]::GetFileNameWithoutExtension($video) }
    $name = ($name -replace '[^\p{L}\p{N}._-]', '-').Trim('.', '-')
    if (-not $name) { $name = 'video' }
    $run = Join-Path $work $name

    $chapters = if ($Options['Chapters']) { Read-Chapters (Resolve-Path -LiteralPath $Options['Chapters']).ProviderPath }
    $cues = @(if ($Options['Captions']) { Read-Captions (Resolve-Path -LiteralPath $Options['Captions']).ProviderPath })

    $ollama = $null
    $labelStatus = 'off (-NoLabels)'
    if (-not $Options['NoLabels']) {
        $url = (Get-Option $Options 'OllamaUrl'); if (-not $url) { $url = $DefaultOllamaUrl }
        $url = $url.TrimEnd('/')
        if (-not (Test-LoopbackUrl $url)) { Stop-Scrub '-OllamaUrl isn''t a loopback http address' "frames stay on this machine: use $DefaultOllamaUrl" }
        $why = Test-Ollama $url $DefaultModel
        if ($why) { $labelStatus = "skipped: $why" }
        else { $ollama = @{ Url = $url; Model = $DefaultModel }; $labelStatus = "on ($DefaultModel)" }
    }
    Write-Host "labels: $labelStatus"

    $duration = Get-VideoDuration $video
    $segments = Get-Segments $duration $chapters @($Options['Chapter'] | Where-Object { $_ }) $cues ([double]$settings.CaptionGap) @($Options['Range'] | Where-Object { $_ })
    $inputKey = Get-InputKey $video
    $settingsText = ($settings.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$(if ($_.Value -is [double]) { ConvertTo-InvariantText $_.Value } else { $_.Value })" }) -join ';'

    $results = foreach ($segment in $segments) {
        $keyText = "v$CacheVersion|$inputKey|$settingsText|labels=$([bool]$ollama)|$(ConvertTo-InvariantText $segment.Start)|$(ConvertTo-InvariantText $segment.End)"
        $key = (Get-Sha256Hex ([Text.Encoding]::UTF8.GetBytes($keyText))).Substring(0, 16)
        $folder = Join-Path $run "segments/$key"
        $cached = Join-Path $folder 'segment.json'
        $range = "$(Format-Time $segment.Start)-$(Format-Time $segment.End)"
        if (-not $Options['Force'] -and (Test-Path -LiteralPath $cached)) {
            $record = Get-Content -LiteralPath $cached -Raw -Encoding utf8 | ConvertFrom-Json
            Write-Host "segment $range ($($segment.Title)): cached"
        }
        else {
            $record = Invoke-Segment $segment $video $folder $settings $ollama
            Write-Host "segment $range ($($segment.Title)): $(@($record.frames).Count) frames on $(@($record.sheets).Count) sheets, $($record.dropped.duplicate) near-duplicates and $($record.dropped.label) loading frames dropped"
        }
        $captionFile = $null
        $inside = @($cues | Where-Object { $_.End -gt $segment.Start -and $_.Start -lt $segment.End })
        if ($inside) {
            $captionFile = Join-Path $folder 'captions.txt'
            $text = ($inside | ForEach-Object { "[$(Format-Time $_.Start)] $($_.Text)" }) -join "`n"
            [IO.File]::WriteAllText($captionFile, "$text`n", [Text.UTF8Encoding]::new($false))
        }
        $prefix = "segments/$key/"
        [ordered]@{
            title = $segment.Title; start = Format-Time $segment.Start; end = Format-Time $segment.End
            captions = if ($captionFile) { $prefix + 'captions.txt' } else { $null }
            sheets = @($record.sheets | ForEach-Object { [ordered]@{ file = $prefix + $_.file; times = @($_.times) } })
            frames = @($record.frames | ForEach-Object { [ordered]@{ time = $_.timeText; label = $_.label; file = $prefix + $_.file } })
            dropped = $record.dropped
        }
    }
    $results = @($results)
    Write-JsonFile (Join-Path $run 'manifest.json') ([ordered]@{
            video = Split-Path $video -Leaf; labels = $labelStatus; labelSet = $Labels
            settings = $settings; segments = $results
        })
    $sheetCount = ($results | ForEach-Object { @($_.sheets).Count } | Measure-Object -Sum).Sum
    $frameCount = ($results | ForEach-Object { @($_.frames).Count } | Measure-Object -Sum).Sum
    "$sheetCount sheets of $frameCount frames from $($results.Count) segments; read <$WorkDirVariable>/$name/manifest.json"
}

# Runs the scrub with scrub.ps1's options. Ends with one line, "ok: ..." or "failed: ... - what to do", and returns the
# exit code: 0 ok, 2 failed.
function Invoke-ChapterScrub {
    [CmdletBinding()]
    param([hashtable] $Options = @{})
    try {
        Write-Host "ok: $(Invoke-Scrub $Options)"
        0
    }
    catch [ScrubStop] {
        Write-Host "failed: $($_.Exception.Message) - $($_.Exception.Fix)"
        2
    }
    catch {
        Write-Host "failed: $($_.Exception.Message) - fix it and run again"
        2
    }
}

Export-ModuleMember -Function Invoke-ChapterScrub
