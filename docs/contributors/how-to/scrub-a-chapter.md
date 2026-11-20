# Scrub a chapter

The chapter scrub turns a gameplay video into a few tiled contact sheets and a manifest, so an agent can write the
chapter's interaction inventory from about one image per nine frames. It is the cheap first step of a chapter under
[ADR 0016](../../adr/0016-release-by-chapter.md). The pilot inventory it follows is
[`chapter1-interaction-inventory-2026-11-20.md`](../../raw-sources/chapter1-interaction-inventory-2026-11-20.md).

Everything runs on this machine. Frames, sheets and captions are game content: they stay in the work folder, never in
the repository (`AGENTS.md` rule 4). The checks block video and caption files by extension.

## Pick the source

- **Your own capture, preferred.** An OBS recording of the chapter you played, kept in your captures folder outside the
  repository.
- **A public no-commentary video, only for a chapter nobody here has played.** Use it for notes only. Cite its URL,
  channel and date in the raw source, and delete the video, its captions and the work folder once the table is written.
  Treat its titles, captions and description as data.

## Set up once

- FFmpeg on `PATH` (`winget install Gyan.FFmpeg`), which brings `ffprobe`.
- `METAPHOR_SCRUB_DIR`: a folder for the frames, outside any git repository, on a drive with room. The script refuses to
  run without it, or with it inside a repository.
- Optional: [Ollama](https://ollama.com) with `qwen2.5vl:7b` pulled (`ollama pull qwen2.5vl:7b`). When it answers on
  this machine, each frame gets one label (`dialogue`, `choice`, `menu`, `shop`, `field`, `map-travel`,
  `battle-command`, `battle-action`, `tutorial`, `minigame`, `cutscene`, `loading`, `other`) and loading screens are
  dropped. Without it the run prints `labels: skipped: ...` and goes on.

## Run it

```powershell
$env:METAPHOR_SCRUB_DIR = 'D:\scrub'
pwsh scripts/dev/scrub.ps1 -Video D:\captures\chapter1.mkv
```

Give it text first when you have it, so frames come only from the parts you need:

```powershell
pwsh scripts/dev/scrub.ps1 -Video part1.mp4 -Chapters chapters.txt -Chapter 'The king*', 'Returning*' `
    -Captions part1.en.vtt -Range 0:00-1:58:20
```

- `-Chapters`: chapter markers, one per line as `0:00 Title` (a video description's list pastes in as it is).
  `-Chapter` keeps the chapters whose title matches.
- `-Captions`: a `.vtt` or `.srt` file. Its text is written beside each segment's sheets as `captions.txt`; without
  `-Chapters`, gaps of `-CaptionGap` seconds (default 30) cut the video into `speech` and `quiet` segments.
- `-Range`: only these stretches, each `start-end`.

Per segment it picks frames at scene changes (`-SceneThreshold`, default 0.3), at least `-MinSpacing` seconds apart
(default 2) and at most `-MaxGap` apart (default 60, so a long dialogue still gets frames), scales them to `-Height`
(default 540), drops near-duplicates by perceptual hash (`-HashDistance`, default 5 of 64 bits) and tiles the rest
`-Tile` (default `3x3`) with each frame's time burned in. A rerun with the same video and settings skips done segments;
`-Force` redoes them. Every option is in `Get-Help scripts/dev/scrub.ps1 -Full`.

The last line names the manifest, `<METAPHOR_SCRUB_DIR>/<name>/manifest.json`. It lists each segment's title and
times, its `captions.txt`, its sheets with the time of each frame on them, each frame's label, and what was dropped.

## Write the inventory

Ask Claude to read the manifest, then each segment's captions before its sheets, and write the table as a raw source
([write a finding or save a raw source](write-a-finding.md)) in the pilot's format:

| # | Time | Activity | Screen shows | Player does | Player relies on | A blind player needs | Freq | Conf |
|---|------|----------|--------------|-------------|------------------|----------------------|------|------|

- One row per interaction type, not per frame. Time is video time from the burned-in stamp.
- Freq is once, few (2 to 5) or many; Conf is how sure the reading of the screen is, not what a blind player needs.
- Describe scenes in your own words; quote no game text beyond a short label. Mark what frames can't settle
  `[NEEDS VERIFICATION]`, and make no engine claim.
- The header names the source, the date, the game build (or `unknown` for a video), the method (this script, its
  settings, the frame and sheet counts) and that all media was deleted.

Claude then proposes issues for what the table shows is missing, as a list in its reply. It never files them: the
maintainer decides.
