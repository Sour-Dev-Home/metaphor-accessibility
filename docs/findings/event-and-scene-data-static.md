# In-engine scenes are readable EVT/ECS timelines: cast, animations, look-at, moves, props, sounds and line order

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Reading the game's own data archives (CPK) offline with our read-only tool (`tools/Metaphor.CpkExtract`),
  output kept in git-ignored `extracted/`; opening the shipped executable in a disassembler as-is, accepting that
  protected parts may be unreadable (only its RTTI names and a data table were read)
- **Sources:** [`atlus-evt-ecs-format-community-2026-11-24.md`](../raw-sources/atlus-evt-ecs-format-community-2026-11-24.md),
  [`modding-tools-survey-2026-11-13.md`](../raw-sources/modding-tools-survey-2026-11-13.md) (Squirrel `.cnut`)
- **Functions:** none

For #960. Each in-engine scene is an unencrypted `COMMON/event/**/<id>.EVT` with an `.ECS` beside it, in Persona 5's
EVT/ECS layout with larger records. The EVT lists the scene's objects (characters, props, field, camera) and a timeline
of four-letter commands with start frames: message lines in order, body animations, look-at, moves, prop attachments,
emotion icons, camera and fades. The ECS holds its sound commands. So a script can write a per-scene fact sheet; what
most command fields mean is still `[NEEDS VERIFICATION]`. Pre-rendered scenes are `.usm` movies with no such timeline.

## Evidence

**Where the files are.** `base.cpk` holds 783 `.EVT` and 783 `.ECS` under `COMMON/event/` (`chapter01`..`chapter15`,
`daily`, `follower`, `quest`), plus three Squirrel scripts in `COMMON/event/script/` (`event_main.cnut` starts with
`fa fa 52 49 51 53`, compiled Squirrel), 12 `.dat` tables (`Character.dat`, `Items.dat`, `Flags.dat` and others) and 70
`entry/*.xml` files of per-day model and condition entries. Lines are in `<lang>/message/event/<id>.msg` (1,059 in
`EN`), lip sync in `<lang>/lipsync/event/<id>.lippack`, movies in `COMMON/movie/event/*.usm` (40). None of the event
files is marked compressed by the tool.

**EVT layout matches the community reader.** All 783 start `EVT\0`; 783 hold their own file size at `+0x10`; all
have frame rate 30 at `+0x20`, objects of `0x510` bytes and commands of `0x70` bytes (P5's are smaller). Versions:
`0x3841c` (730), `0x33853` (52), `0x33528` (1). The command record's first 0x20 bytes follow EvtTool's order (code, two
16-bit fields, object id, skip, start frame, duration, data offset, data size). 743 EVTs name their line file at
`+0x50`, always under `JP/message/event/` (`JP/message/event/e01_004_001.msg`); that the game swaps in the player's
language is `[NEEDS VERIFICATION]`. 122 name a script at `+0x60`, all in `COMMON/event/script`.

**What each fact maps to** (counts are commands across all EVTs; meanings from the wiki for P5 unless checked here):

| Fact | Where | Checked here |
|---|---|---|
| Characters present | objects of type `0x01000101` (4,934), with a resource id | In `e01_004_001` the two characters are resources 1 and 11, and its `.msg` speakers are `#SPEAKER_ID "1"` and `"11"`; in `e01_007_001` resources 2136, 2026, 1036 match speakers `M2136_002`, `M2026_002`, `M1036_001`. The id-to-name link (`CharaID.bin`, [speaker alias](speaker-alias-from-chara-id-table.md)) is `[NEEDS VERIFICATION]` for event ids |
| Dialogue order | `Msg_` (17,391), start frame, line index in data `+0x04` | `e01_007_001`'s `Msg_` indices in frame order are 1, 1, 2, 3, 5, 6, 7, 8, 13, 15, 16, 17, 18; its `.msg` labels are `@MES_e01_007_001_001`..`_018` with the same gaps. `e01_004_001` matches too, choice branch included. What the other data words do (a choice id in one) is `[NEEDS VERIFICATION]` |
| Animations or poses | `MAB_` (15,064), `MSD_` (20,690), `Mbab`, `MAA_` | By number only (`MAB_` data `+0x00`: 1, 2, 3, 5, 37...); no names in the file. Mapping numbers to poses needs the model's animation pack `[NEEDS VERIFICATION]` |
| Look-at | `MLa_` (26,842), object id plus floats | Target fields `[NEEDS VERIFICATION]` |
| Movement, turning | `MMD_` (434), `MRot` (345), `MSD_` positions | Fields `[NEEDS VERIFICATION]` |
| Items changing hands | Item objects, type `0x601` (765); `MAt_` (345) attaches one to a character; `GI__` (114) | `e01_007_001` holds 8 item objects and 14 `MAt_`. That `GI__` is an item-get is a guess `[NEEDS VERIFICATION]` |
| Emotion icons | `MIc_` (2,829) | Icon ids `[NEEDS VERIFICATION]` |
| Spawn, despawn | `MRgs` (3,003) | Not checked |
| Sound effects, music | ECS: `Snd_` (2,993), `SndA` (382), `MFs_` (1,968) | ECS header: count, offset `0x10`, record `0x70`. `Snd_` data `+0x04` is 1, 3 or 4 (P5: 1 BGM, 3 event SFX); cue at data `+0x10` (`0x416` in `e01_004_001`). Cue names need the ACB `[NEEDS VERIFICATION]` |
| Camera, fades, shake | `CSD_` (6,813), `Fd__` (2,801), `CShk` (1,315) | Not checked |
| Subtitled voice | `Subs` (506 in 19 EVTs) | Contents `[NEEDS VERIFICATION]` |

Command codes found: 88. The ten most common, by EVTs that use them: `Fd__` 782, `FS__` 776, `MSD_` 772, `CSD_` 766,
`Msg_` 755, `MFb_` 753, `MAB_` 743, `MLa_` 739, `mskl` 737, `CShk` 695.

**The executable knows the same codes.** The `.debug` section (data, despite the name) holds a table pairing each code
with pointers into code: `MLa_` at RVA `0x279ee08` is followed by `0x140409a10`, `Subs` at `0x279e448`, `Msg_` at
`0x279f558`, `MAB_` at `0x279e650`. That these are the command handlers is `[NEEDS VERIFICATION]`. RTTI names 40
classes in `app::evt`, among them `evtObj`, `evtUnitController`, `evtMoveController`, `evtSoundManager`,
`evtPseudoInput_SelectMessage`, `evtPseudoInput_Skip`, `evtCharacterData` and `evtItemData` (the last two match the
`.dat` names).

**Gaps.** 320 `EN` event `.msg` files have no EVT of the same name (44 EVTs have no `.msg`), among them the opening scene `e00_002_001` (message and lip sync,
no EVT, no movie). Those lines come from somewhere else: `event_main.cnut` or a field script, `[NEEDS VERIFICATION]`.
That fits [event-scene-lines-skip-msgrequest.md](event-scene-lines-skip-msgrequest.md) only partly: `e01_004_001`, whose
lines skipped `MsgRequestByName`, does have an EVT. Movies with lines (`e01_002_001.usm` and `.msg`) are in
[anime-movie-subtitles-skip-dialogue-hooks.md](anime-movie-subtitles-skip-dialogue-hooks.md).

## Steps

Repository at `30ca116f`, .NET SDK 9.0.319, Python 3.12.10, Git Bash; `METAPHOR_GAME_EXE` set to the game exe.

1. `dotnet run --project tools/Metaphor.CpkExtract -c Release -v q -- list base.cpk`, saved outside the repository;
   counted paths by folder and extension.
2. `... -- extract base.cpk <every COMMON/event/**/*.EVT and *.ECS> --out extracted/evt`, in chunks of 200 paths, plus
   `EN/message/event/e01_004_001.msg` and `e01_007_001.msg`, `COMMON/event/script/event_main.cnut` and
   `COMMON/event/entry/d02_001_002.xml`.
3. A throwaway Python script (not kept) read each EVT's header at the offsets above, every object's id, type
   (`+0x04`), resource ids (`+0x10`..`+0x16`), and every command's code, object id (`+0x08`), start frame (`+0x10`),
   duration (`+0x14`) and data offset and size (`+0x18`, `+0x1C`); it counted codes and printed `Msg_` data for the two
   chapter 1 scenes. A second did the same for ECS. In the `.msg` files only `#SPEAKER_ID` and `@MES_` lines were read.
4. `grep -ao` over `METAPHOR.exe` for `.?AV...@evt@app@@` RTTI names and for the codes; file offsets mapped to RVAs from
   the PE section table.

## To confirm next

- A committed EVT reader (like `CpkExtract`) that prints a fact sheet per scene, with a round-trip test on the header.
- By ear or with a hook: that `Msg_` frame order is the order lines show, including choice branches.
- Field meanings for `MAB_`, `MLa_`, `MMD_`, `MAt_`, `MIc_`, `Snd_`, `GI__`, `Subs`: watch one short scene (OBS
  capture) beside its timeline, or read the handlers the table points to.
- Names: character resource ids to names (`CharaID.bin` or `Character.dat`), sound cue ids to names (the event ACB),
  animation numbers to anything readable (the model's animation pack). Without these, a sheet says "character 11 plays
  animation 37", not "Gallica shrugs".
- Where the 320 EVT-less scenes (the opening among them) keep their timeline.
