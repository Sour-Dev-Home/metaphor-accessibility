# From a menu cursor manager to its screen class: static leads (#213)

- **Source:** our reading of the game exe on disk, as shipped (ADR 0002: nothing patched, unpacked or run). Tools: a
  throwaway .NET 9.0 reader on Iced 1.21.0 that maps MSVC RTTI to vtables (method in
  `ui-focus-candidates-2026-11-16.md`), lists vtable slots, disassembles `.ecode` and finds code and data that refer to
  an address; and `tools/Metaphor.SignatureCheck` (`--bytes`, `--probes`). The reader isn't committed; the steps below
  repeat it. Only plain-code `.ecode` was read; functions that jump into `.text1` were not followed.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Class names and offsets only: identifiers, not game content (ground rule 4).

## Findings

**The link runs from the screen to the cursor, through a virtual getter.** Confidence: high for the layout, which the
code reads directly; whether the game calls the getter while a menu is open is `[NEEDS VERIFICATION]`.

- Each menu screen is a choice object (`app::ui::ope`, RTTI `...Choice`), with two vtables (at `+0x00` and `+0x08`).
  `TitleMenuChoice`'s constructor (`0xe2c1c0`) stores vtables `0x21f10b0` and `0x21f12f8`; its deleting destructor
  (slot 2, `0xe2dd70`) frees `0x410` bytes.
- Slot 11 of the primary vtable (`+0x58`) is the cursor getter. In 279 vtables, `TitleMenuChoice` and `RootMenuChoice`
  among them, it is `0xd4ab90` (we call it `ChoiceGetCursor`): `shared_ptr* (Choice* this, shared_ptr* out)`, returning `out`, which copies a
  `shared_ptr<CursorManagerBase>` from `this+0x230` (pointer) and `this+0x238` (control block, count at `+8`) into `out`.
- Slots 6, 7 and 8 (`0xd46810`, `0xd46c80`, `0xd46920`) call the getter, then read the manager's `+0x60` (top) and
  `+0x64` (row), or call its slot 1 (index): the fields and getter of `ui-focus-candidates-2026-11-16.md`. So the
  object at `[choice+0x230]` is the manager whose `CursorManagerInput` the mod already hooks.
- In plain code, nothing else reads `+0x230` of a choice; other callers go through slot 11.
- Five vtables use a tabbed getter instead, `0xd976f0`, which returns one of three managers (`+0x1740`, `+0x1750`,
  `+0x1760`) by a tab index at `+0x1738`: `ClassDetailBase<CampArchetypeDetailLayout>`, `EquipClassDetailChoice`,
  `StatusMenuClassDetailChoice`, `AcademiaClassDetailChoice` and `BTL_ClassChange`. Two byte-identical copies serve other
  layouts (`0xd9e030`, `0xe4df10`), so its bytes match 3 times and it has no unique signature yet.

**No back pointer from the manager was found.** `CursorManager`'s readable slots (vtable `0x22045e8`, slots 0–12) read
`+0x48`/`+0x50` (as virtual calls), `+0x58`, `+0x60`, `+0x64`, `+0x88`/`+0x90` (a vector of 16-byte item entries) and
`+0xAC`; none is an owner. Its constructor is in `.text1` and wasn't read. A back pointer is `[NEEDS VERIFICATION]`.

## The listener (Debug, opt-in)

`ChoiceCursorGetterListener` hooks `ChoiceGetCursor` (one match on disk, `screen-class-probes.json`) when
`opt-in-screen-class.txt` is in the mod's folder, and records which choice handed out which manager. On each cursor input
it logs `screen mgr=0x… mgrClass=… owner=0x…|none ownerClass=… fields=+0x…:Class,…|none`: the owner from the getter
(the forward link) and every pointer in the manager's first `0x100` bytes whose target has an RTTI name (a back link).
One session decides which works.

## Candidate screen classes

From RTTI names alone; which one each screen uses is `[NEEDS VERIFICATION]` until the session's `ownerClass`.

| Screen | Candidates |
|---|---|
| Title menu | `TitleChoice`, `TitleMenuChoice` |
| Title System submenu | `TitleMenuChoice`, `TitleChoice`, `SystemMenuChoice` |
| Main menu | `RootMenuChoice` |
| Skill | `SkillCharaSelChoice`, `SkillSelectChoice`, `SkillTargetCharaSelChoice` |
| Item | `ItemMenuChoice`, `ItemCharaSelChoice`, `ItemMenuClassSelectChoice` |
| Equipment | `EquipCharaSelChoice`, `EquipPartsChoice`, `EquipSwapCharaSelChoice` |
| Party, Status | `StatusCharaSelChoice`, `StatusMenuChoice` |
| Tactics | `TacticalChangeChoice` |
| Archetype details | `EquipClassDetailChoice`, `StatusMenuClassDetailChoice` (tabbed getter, not hooked) |
| Archetype Tree | none found by name; `AcademiaTopChoice`, `AcademiaClassSelectChoice` are guesses |

## Steps

1. Map RTTI names to vtables; list `TitleMenuChoice`'s and `RootMenuChoice`'s 72 primary slots side by side.
2. Find code that loads each vtable (`lea` to it): the constructors. Disassemble the slots they share.
3. Find every 8-byte pointer to `0xd4ab90` and `0xd976f0` in the file (vtable slots), and name each vtable.
4. `--bytes <exe> 0xd4ab90 0x3a`, then `--probes <exe> tools/Metaphor.SignatureCheck/screen-class-probes.json`.
