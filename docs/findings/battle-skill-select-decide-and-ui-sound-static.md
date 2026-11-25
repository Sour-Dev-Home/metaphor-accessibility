# The skill list's confirm handler and the UI-sound play are both hookable in `.ecode`

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on
  [`battle-not-enough-mp-path-static.md`](battle-not-enough-mp-path-static.md)
- **Functions:**
  - UiSoundPlay | 0x92e3e0 | .ecode | Battle | known only | Plays a UI sound effect by its id; the buzzer is id 3. | finding
  - SkillSelectDecide | 0xd77810 | .ecode | Battle | known only | `BTL_SkillSelect`'s confirm handler: posts the focused row's skill id to the UI flow. | finding
  - BTL_SkillSelect::vf64 | 0xd96990 | .ecode | Battle | known only | Hands the list a callback whose body (`0xd8c000`) calls `SkillSelectDecide`. | finding

For #692. Two `.ecode` functions, neither in `.text1` nor a jump stub into it, together tell an unaffordable-skill
refusal from other buzzers: `SkillSelectDecide` (`0xd77810`) runs on each confirm in `BTL_SkillSelect` with the
component and the focused skill's id, and `UiSoundPlay` (`0x92e3e0`) is the UI-sound chain's caller of `0x1493f30`
that the 2026-11-23 cue log saw (return address `0x92e475`). The cost-versus-MP check itself wasn't found: the
handler hands the confirm to the UI flow through `.text1` code, and the check isn't in analysed code, so the refusal
has to be recognised as "skill-list confirm followed by sound 3" `[NEEDS VERIFICATION]` by log.

## Evidence

- **The logged caller is CRI code.** `0x1287f5c` is the return address of the call at `0x1287f57` in `0x1287dc4`, an
  `.ecode` function that looks like the audio library's own code `[NEEDS VERIFICATION]`, which is why it is the same
  for every cue (#692's comment from the #818 batch).
  The UI side of the chain is one frame further out: `0x92e475` is the return from `UiSoundPlay`'s call to
  `0x1493f30`.
- **`UiSoundPlay`.** `0x92e3e0` opens with a full prologue (saves `RBX`, pushes `RDI`, reserves `0x70`), takes the sound
  id in its first argument, stores it at `+0x84` of the sound object it fetches, and passes it to `0x1493f30`. It has
  369 call sites, among them every list's shared code and `BTL_CommandSelectChoice::vf16` (`0xd87054`). That the id
  here is the `GeneralSE_ID` a list's check returns, and cue 3 is id 3, rests on the matching numbers
  `[NEEDS VERIFICATION]`.
- **`SkillSelectDecide`.** `BTL_SkillSelect` overrides slot 64 (`0xd96990`, vtable `0x21c9fe8`), which builds a
  `std::function<GeneralSE_ID(fclAppUIEvent*)>` (lambda vtable `0x21d10c8`). Its call slot `0xd8c000` only calls
  `0xd77810(component, event)`, which is `0xd77810`'s one caller. `0xd77810` reads the cursor (slot `+0x58`), takes that
  row's skill id from the array at component `+0x4260` (8-byte rows, count at `+0x4268`), sets the UI flow's `SkillID`
  variable to it and `COMMON_VAL` to `0xf000000`, then posts the event through `0xd46610(component + 0x198, …)`. The
  variable lookup goes through a thunk to `0x8ce7fc0`, in `.text1`, so what the flow then does (accept, or refuse
  with the buzzer) isn't readable.
- **Which slot the framework calls on confirm.** That the list calls slot 64's callback on the confirm button, and plays
  its result through `UiSoundPlay`, is inferred from the callback's type and the earlier note's lambdas
  `[NEEDS VERIFICATION]`. Slot 64 is a new lead: the #795 note listed the other overrides but not this one.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`,
[query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)), build above, 2026-11-23. The game
wasn't run. Section bounds from the exe's PE section table, read as data.

1. `disasm 0x1287f5c`, `callers 0x1493f30`, `disasm 0x92e475`, `callers 0x92e3e0`.
2. `vtable app::ui::ope::BTL_SkillSelect`; `decompile` and `disasm 0xd96990`; `vtable 0x21d10c8`; `decompile 0xd8c000`.
3. `callers 0xd77810`, `decompile 0xd77810`.
4. `decompile 0xd485c0`, `callers 0xd485c0`, `decompile 0xd45730`: the shared cursor-move sound path, not the confirm.

Only `0xd77810`'s calls into `.text1` were seen, by address; nothing there was decompiled.

## Build plan

1. **Log first (Debug).** Hook `SkillSelectDecide` (`0xd77810`) and `UiSoundPlay` (`0x92e3e0`) as observers. In
   `BTL_SkillSelect`, confirm an affordable skill, an MP skill you can't afford and an HP skill you can't afford. Check
   that a refusal is exactly a `SkillSelectDecide` call followed in the same frame by `UiSoundPlay(3)`, and an accepted
   skill isn't.
2. **Build.** If it holds, both as Release `[GameHook]` observers (security review scope): `SkillSelectDecide` keeps the
   skill id; a following `UiSoundPlay(3)` says "Not enough MP", or "Not enough HP" from the skill's cost type
   `[NEEDS VERIFICATION]`: a read of the skill table, or the in-game message 73 and 74 of `jyokyo_help_Message.msg`
   (#795 note). It replaces the dev-only `criAtomExPlayer_SetCueId` signal.
3. **Signatures.** Neither function has a signature yet: write them and check each matches once with
   `tools/Metaphor.SignatureCheck --probes` before step 1.
