# Tests for scripts/dev/scrub.ps1 (ChapterScrub.psm1), on a 12-second video made here from ffmpeg's own test patterns:
# never game footage. Ollama is mocked, so no test needs it or reaches the network.

Describe 'scrub' {
    BeforeAll {
        Import-Module (Join-Path $PSScriptRoot '../../scripts/dev/ChapterScrub.psm1') -Force
        $script:hasFfmpeg = [bool](Get-Command ffmpeg -CommandType Application -ErrorAction SilentlyContinue) -and
        [bool](Get-Command ffprobe -CommandType Application -ErrorAction SilentlyContinue)
        $script:savedWork = $env:METAPHOR_SCRUB_DIR

        # Four still scenes of 3 s each, the last a repeat of the first: cuts at 3, 6 and 9 s.
        $script:video = Join-Path $TestDrive 'scène vidéo.mp4'
        if ($script:hasFfmpeg) {
            $sources = 'smptebars', 'testsrc', 'rgbtestsrc', 'smptebars'
            $arguments = @('-hide_banner', '-loglevel', 'error', '-y')
            foreach ($source in $sources) { $arguments += '-f', 'lavfi', '-i', "$source=s=320x180:r=5:d=3" }
            $arguments += '-filter_complex', '[0][1][2][3]concat=n=4:v=1:a=0,format=yuv420p', '-c:v', 'libx264', $script:video
            & ffmpeg @arguments
            if ($LASTEXITCODE) { throw "ffmpeg could not make the test video ($LASTEXITCODE)" }
        }

        function Invoke-Scrub([hashtable] $Options) {
            $output = Invoke-ChapterScrub $Options 6>&1
            [pscustomobject]@{ ExitCode = $output[-1]; Output = ($output | Select-Object -SkipLast 1) -join "`n" }
        }

        function Read-Manifest([string] $Name = 'scène-vidéo') {
            Get-Content -LiteralPath (Join-Path $env:METAPHOR_SCRUB_DIR "$Name/manifest.json") -Raw -Encoding utf8 | ConvertFrom-Json
        }

        function Skip-WithoutFfmpeg { if (-not $script:hasFfmpeg) { Set-ItResult -Skipped -Because 'ffmpeg is not on PATH' } }

        # Ollama answering with the model pulled, labelling the frame at 6 s (rgbtestsrc's 6000.jpg) as loading.
        function Mock-OllamaPresent {
            Mock -ModuleName ChapterScrub Invoke-RestMethod -ParameterFilter { $Uri -like '*/api/tags' } {
                [pscustomobject]@{ models = @([pscustomobject]@{ name = 'qwen2.5vl:7b' }) }
            }
            Mock -ModuleName ChapterScrub Invoke-RestMethod -ParameterFilter { $Uri -like '*/api/generate' } {
                $image = ($Body | ConvertFrom-Json).images[0]
                $script:labelCalls++
                Start-Sleep -Milliseconds 30
                [pscustomobject]@{ response = if ($script:loadingImage -eq $image) { 'Loading.' } else { 'menu' } }
            }
        }
    }

    AfterAll { $env:METAPHOR_SCRUB_DIR = $script:savedWork }

    BeforeEach {
        $env:METAPHOR_SCRUB_DIR = Join-Path $TestDrive "work é $(New-Guid)"
        # No Ollama unless a test says otherwise.
        Mock -ModuleName ChapterScrub Invoke-RestMethod { throw 'No connection could be made' }
    }

    It 'refuses to run without METAPHOR_SCRUB_DIR' {
        $env:METAPHOR_SCRUB_DIR = $null
        $r = Invoke-Scrub @{ Video = $script:video; NoLabels = $true }
        $r.ExitCode | Should -Be 2
        $r.Output | Should -Match 'failed: METAPHOR_SCRUB_DIR isn''t set'
    }

    It 'refuses a work folder inside a git repository' {
        $repo = Join-Path $TestDrive "repo $(New-Guid)"
        New-Item -ItemType Directory -Force (Join-Path $repo '.git') | Out-Null
        $env:METAPHOR_SCRUB_DIR = Join-Path $repo 'scrub'
        $r = Invoke-Scrub @{ Video = $script:video; NoLabels = $true }
        $r.ExitCode | Should -Be 2
        $r.Output | Should -Match 'inside a git repository'
        $env:METAPHOR_SCRUB_DIR | Should -Not -Exist
    }

    It 'refuses an Ollama address off this machine' {
        Skip-WithoutFfmpeg
        $r = Invoke-Scrub @{ Video = $script:video; OllamaUrl = 'http://192.0.2.1:11434' }
        $r.ExitCode | Should -Be 2
        $r.Output | Should -Match 'loopback'
        Should -Invoke -ModuleName ChapterScrub Invoke-RestMethod -Times 0 -Exactly
    }

    It 'picks a frame at each scene change' {
        Skip-WithoutFfmpeg
        $r = Invoke-Scrub @{ Video = $script:video; NoLabels = $true; MinSpacing = 1; HashDistance = -1 }
        $r.ExitCode | Should -Be 0 -Because $r.Output
        (Read-Manifest).segments[0].frames.time | Should -Be @('0:00', '0:03', '0:06', '0:09')
    }

    It 'drops a repeated scene as a near-duplicate' {
        Skip-WithoutFfmpeg
        $r = Invoke-Scrub @{ Video = $script:video; NoLabels = $true; MinSpacing = 1 }
        $r.ExitCode | Should -Be 0 -Because $r.Output
        $segment = (Read-Manifest).segments[0]
        $segment.frames.time | Should -Be @('0:00', '0:03', '0:06')
        $segment.dropped.duplicate | Should -Be 1
    }

    It 'keeps a frame every max-gap in a long static stretch, though all hash alike (#525)' {
        Skip-WithoutFfmpeg
        $static = Join-Path $TestDrive 'static.mp4'
        & ffmpeg -hide_banner -loglevel error -y -f lavfi -i 'smptebars=s=320x180:r=5:d=10' -pix_fmt yuv420p -c:v libx264 $static
        $LASTEXITCODE | Should -Be 0
        $r = Invoke-Scrub @{ Video = $static; NoLabels = $true; MinSpacing = 1; MaxGap = 3; Name = 'static' }
        $r.ExitCode | Should -Be 0 -Because $r.Output
        (Read-Manifest 'static').segments[0].frames.time | Should -Be @('0:00', '0:03', '0:06', '0:09')
    }

    It 'keeps picks at least the minimum spacing apart' {
        Skip-WithoutFfmpeg
        $r = Invoke-Scrub @{ Video = $script:video; NoLabels = $true; MinSpacing = 4; HashDistance = -1 }
        $r.ExitCode | Should -Be 0 -Because $r.Output
        # The cut at 3 s is too close to 0 s, and the one at 9 s too close to 6 s.
        (Read-Manifest).segments[0].frames.time | Should -Be @('0:00', '0:06')
    }

    It 'tiles the frames onto sheets of the given size, in order' {
        Skip-WithoutFfmpeg
        $r = Invoke-Scrub @{ Video = $script:video; NoLabels = $true; MinSpacing = 1; Tile = '2x1' }
        $r.ExitCode | Should -Be 0 -Because $r.Output
        $sheets = (Read-Manifest).segments[0].sheets
        $sheets.Count | Should -Be 2
        $sheets[0].times | Should -Be @('0:00', '0:03')
        $sheets[1].times | Should -Be @('0:06')
        foreach ($sheet in $sheets) { Join-Path $env:METAPHOR_SCRUB_DIR "scène-vidéo/$($sheet.file)" | Should -Exist }
    }

    It 'takes frames only inside the requested range' {
        Skip-WithoutFfmpeg
        $r = Invoke-Scrub @{ Video = $script:video; NoLabels = $true; MinSpacing = 1; Range = @('0:05-0:08') }
        $r.ExitCode | Should -Be 0 -Because $r.Output
        $segment = (Read-Manifest).segments[0]
        $segment.start | Should -Be '0:05'
        $segment.frames.time | Should -Be @('0:05', '0:06')
    }

    It 'cuts segments at chapter markers and keeps those asked for' {
        Skip-WithoutFfmpeg
        $chapters = Join-Path $TestDrive 'chapters.txt'
        Set-Content -LiteralPath $chapters -Encoding utf8 -Value '0:00 Bars', '0:03 - Pattern', '0:06 Colours'
        $r = Invoke-Scrub @{ Video = $script:video; NoLabels = $true; MinSpacing = 1; Chapters = $chapters; Chapter = @('Pat*', 'Col*') }
        $r.ExitCode | Should -Be 0 -Because $r.Output
        $segments = (Read-Manifest).segments
        $segments.title | Should -Be @('Pattern', 'Colours')
        $segments[0].frames.time | Should -Be @('0:03')
    }

    It 'cuts speech and quiet segments from captions and writes their text beside the sheets' {
        Skip-WithoutFfmpeg
        $captions = Join-Path $TestDrive 'captions.vtt'
        Set-Content -LiteralPath $captions -Encoding utf8 -Value 'WEBVTT', '', '00:00:00.500 --> 00:00:02.000', '<c>Hello</c> there', '', '00:00:08.000 --> 00:00:09.000', 'Again'
        $r = Invoke-Scrub @{ Video = $script:video; NoLabels = $true; MinSpacing = 1; Captions = $captions; CaptionGap = 4 }
        $r.ExitCode | Should -Be 0 -Because $r.Output
        $segments = (Read-Manifest).segments
        $segments.title | Should -Be @('speech', 'quiet', 'speech')
        $text = Get-Content -LiteralPath (Join-Path $env:METAPHOR_SCRUB_DIR "scène-vidéo/$($segments[0].captions)") -Raw
        $text | Should -Be "[0:00] Hello there`n"
        $segments[1].captions | Should -BeNullOrEmpty
    }

    It 'skips a segment it has already done' {
        Skip-WithoutFfmpeg
        $first = Invoke-Scrub @{ Video = $script:video; NoLabels = $true; MinSpacing = 1 }
        $first.ExitCode | Should -Be 0 -Because $first.Output
        # ffprobe still answers the length; ffmpeg must not run.
        Mock -ModuleName ChapterScrub Invoke-Ffmpeg {
            if ($Tool -eq 'ffmpeg') { throw 'ffmpeg ran again' } else { '{"format":{"duration":"12.000000"}}' }
        }
        $second = Invoke-Scrub @{ Video = $script:video; NoLabels = $true; MinSpacing = 1 }
        $second.ExitCode | Should -Be 0 -Because $second.Output
        $second.Output | Should -Match ': cached'
        (Read-Manifest).segments[0].frames.time | Should -Be @('0:00', '0:03', '0:06')
    }

    It 'redoes a segment when a setting changes' {
        Skip-WithoutFfmpeg
        (Invoke-Scrub @{ Video = $script:video; NoLabels = $true; MinSpacing = 1 }).ExitCode | Should -Be 0
        $second = Invoke-Scrub @{ Video = $script:video; NoLabels = $true; MinSpacing = 4 }
        $second.Output | Should -Not -Match ': cached'
    }

    It 'reports Ollama absent and goes on without labels' {
        Skip-WithoutFfmpeg
        $r = Invoke-Scrub @{ Video = $script:video; MinSpacing = 1 }
        $r.ExitCode | Should -Be 0 -Because $r.Output
        $r.Output | Should -Match 'labels: skipped: Ollama isn''t reachable'
        $manifest = Read-Manifest
        $manifest.labels | Should -Match '^skipped'
        $manifest.segments[0].frames.Count | Should -Be 3
    }

    It 'reports a missing model and goes on without labels' {
        Skip-WithoutFfmpeg
        Mock -ModuleName ChapterScrub Invoke-RestMethod { [pscustomobject]@{ models = @() } }
        $r = Invoke-Scrub @{ Video = $script:video; MinSpacing = 1 }
        $r.ExitCode | Should -Be 0 -Because $r.Output
        $r.Output | Should -Match 'ollama pull qwen2.5vl:7b'
    }

    It 'labels each frame with Ollama and drops loading screens' {
        Skip-WithoutFfmpeg
        # The frame at 6 s, as the run will extract it, answers "Loading."
        $probe = Invoke-Scrub @{ Video = $script:video; NoLabels = $true; MinSpacing = 1; Name = 'probe' }
        $probe.ExitCode | Should -Be 0
        $frame = Join-Path $env:METAPHOR_SCRUB_DIR "probe/$((Read-Manifest 'probe').segments[0].frames[2].file)"
        $script:loadingImage = [Convert]::ToBase64String([IO.File]::ReadAllBytes($frame))
        $script:labelCalls = 0
        Mock-OllamaPresent
        $r = Invoke-Scrub @{ Video = $script:video; MinSpacing = 1 }
        $r.ExitCode | Should -Be 0 -Because $r.Output
        $r.Output | Should -Match 'labels: on \(qwen2.5vl:7b\)'
        $segment = (Read-Manifest).segments[0]
        $segment.frames.time | Should -Be @('0:00', '0:03')
        $segment.frames.label | Should -Be @('menu', 'menu')
        $segment.dropped.label | Should -Be 1
        $script:labelCalls | Should -Be 3
        # The label time is in the segment line and the manifest (#525).
        $r.Output | Should -Match 'labels took \d+(\.\d+)? s'
        $segment.labelSeconds | Should -BeGreaterThan 0
        (Read-Manifest).labelSeconds | Should -BeGreaterThan 0
        # No redirect is followed, so no frame can leave the machine after the loopback check.
        Should -Invoke -ModuleName ChapterScrub Invoke-RestMethod -Times 4 -Exactly -ParameterFilter { $MaximumRedirection -eq 0 }
    }
}
