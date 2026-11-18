# Player position, facing and camera in capture session 6 (#177)

- **Source:** capture session 6 (capture evening 2, #250): Debug mod from branch `capture-kit-2` at `1ff25fc` through
  Reloaded-II with `opt-in-field.txt`; the `field` record (`FieldWatch.cs`), whose fields are defined in
  `docs/raw-sources/capture-2-field-leads-2026-11-17.md` on `capture-kit-2`. Read from the session's `message-log.txt`
  (1050 `field` records) with grep and awk, against the maintainer's narration of the walking test (video 07:02–08:35)
  and the OBS recording. Log, narration and recording stay local (game text). Video time is log time − 161 s.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Findings

**Position: `player+0x80` (x, y, z floats), player `[[fldWorld+0x20]]`, class `fldPlayer@fld@app`.** x and z change
while walking and stay fixed while standing; y steps (0, 20, 217.8) on stairs and ledges. Same player class on all five
maps visited. Units look like centimetres (a walk of a few seconds covers 2000–5000) `[NEEDS VERIFICATION]`.

**Facing: the quaternion at `player+0x90`**, logged as `atan2(x, z)` of `(0, 0, 1)` rotated by it. It turns with the
stick and holds while standing.

**Camera: `cam70` is the look-at point and `cam60` the eye; `camHeading` (heading of `cam70 − cam60`) is the view
direction.** `cam70` sits within about 60 of the player in x/z and about 150 above, and follows them. While the player
stood still (log 663–679 s) and the maintainer turned the camera left, right, up and down, only `cam60` moved: its x/z
orbit swept `camHeading` through the full circle, and up and down moved its y (19 to 641). `cam` (`[world+0x10]+0x08`)
read 2 in free roam on every map, 1 or 4 briefly; never the 5 the static lead expected, so what the values mean is
`[NEEDS VERIFICATION]`.

**Camera-relative direction works.** `facing − camHeading`, wrapped to ±180°, on every record where the player moved
during the stick test:

| Stick | Video time | `facing − camHeading` |
|---|---|---|
| Up | 447–461 s | −7° to +7° (34 records) |
| Left | 468–470 s | +80° to +86° (8) |
| Right | 478–480 s | −94° to −99° (8) |
| Down | 482–487 s | ±166° to 180° |

So headings grow counter-clockwise seen from above (left is +90°). For a target at world heading `h`, its clock
direction from the camera is `round(−(h − camHeading) / 30) mod 12`, with 12 ahead and 3 to the right. The headings
`field-near` logs (#178) use the same convention.

How sure: high for position and camera heading (stationary camera sweeps and four stick directions agree within 10°);
high for facing. Turning outside the stick test, the unit of distance and the `cam` values are `[NEEDS VERIFICATION]`.

## Steps

1. Print `field` records for video 440–520 s: `pos`, `facing`, `cam`, `cam60`, `cam70`, `camHeading`.
2. For records where `pos` changed, compute `facing − camHeading` wrapped to ±180°; group by the narrated stick
   direction.
3. For 663–679 s (player still), compare `cam60` and `cam70` against the narrated camera turns.
