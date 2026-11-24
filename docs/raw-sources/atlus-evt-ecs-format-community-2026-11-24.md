# Community notes on Atlus's EVT and ECS event formats (Persona 5)

- **Source:** the Amicitia wiki pages <https://amicitia.miraheze.org/wiki/EVT> (last edited 2025-02-28, revision
  oldid 5846) and <https://amicitia.miraheze.org/wiki/ECS> (last edited 2022-11-20), both CC BY-SA 4.0; and TGE's
  EvtTool, <https://github.com/tge-was-taken/EvtTool> at commit `92c539ba6f8c1f480bd0f9f9ff4e8f233c7312ed`
  (2024-01-14, branch `main`), files `src/EvtTool/EvtFile.cs`, `Command.cs`, `EvtObject.cs`, `EvtObjectType.cs`,
  fetched with `gh api repos/tge-was-taken/EvtTool/contents/<file>`. EvtTool has no licence file (`gh api` returns
  `license: null`), so nothing is copied: the layout is recorded in our words.
- **Saved:** 2026-11-24.
- **Game build:** none; all three describe Persona 5 / Persona 5 Royal, not Metaphor. What matches Metaphor's files is in
  [event-and-scene-data-static.md](../findings/event-and-scene-data-static.md).

## EVT (wiki)

"EVT is a binary event format used by Atlus in Persona 5." It starts with event parameters (ids, duration), then object
definitions (id, type, resource ids, animation ids), then commands, each with a four-character code and its own data.
Command descriptions quoted from the wiki:

- `Msg_`: "Displays a message in a BMD by the id in the message name"; fields at `+0x04`/`+0x06` are major and minor
  ids, `+0x08`..`+0x0C` handle player choices.
- `MSD_`: "Sets up positioning data for a model object, and can trigger animations as well."
- `MAB_`: makes a model play a body animation; `+0x00` and `+0x10` index the animation pack.
- `MLa_`: "Makes a model object look at a target position." `MRot`: "Rotates a model object."
- `MMD_`: "Makes a model object move in a way specified by the parameters."
- `MAt_`: "Attaches a Prop object (aka item) to the bone of a Character model."
- `MIc_`: displays "an emotion icon (exclamation mark, sweat drop, etc.) above a character model's head."
- `MRgs`: "Seems to spawn or despawn objects of a given object id."
- `CSD_`: "Sets up camera parameters such as position, rotation, fov." `CShk`: camera shake. `Fd__`: fade.
- `FS__`: "Indicates which field object should be loaded as the current scene."
- `Scr_`: runs a procedure from the event's script by index.

## ECS (wiki)

"ECS ... is used to store sound commands, and is to be used along with an EVT file." `Snd_`: "Command used to play BGM
and SFX. More testing required"; its `+0x04` picks the source (1 BGM, 2 system SFX, 3 event SFX), `+0x10` a cue id.
`SFts` toggles footstep sounds for a model.

## EvtTool's reader (our summary)

- Header: magic `EVT`, version, major id, minor id, file size at `+0x10`, duration at `+0x1C`, object count, offset and
  size at `+0x30`..`+0x38`, command count, offset and size at `+0x40`..`+0x48`, the message file path's offset and length
  at `+0x50`/`+0x54`, the script path's at `+0x60`/`+0x64`.
- Command record: code (4 bytes), two 16-bit fields, object id, a skip flag, start frame, duration, data offset, data
  size, then four flag-condition fields.
- Object record: id, type, two fields, resource major id, resource sub and minor ids (16-bit), a field, animation
  major, minor and sub ids. Object type values include `0x01000101` Character, `0x00000601` Item, `0x00000003` Field,
  `0x00000004` Env, `0x01000002` Particle, `0x00000008` Movie.
