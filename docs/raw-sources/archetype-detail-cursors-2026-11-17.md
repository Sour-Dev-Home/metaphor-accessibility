# Archetype details' cursors: a hookable setup function for the tabbed choices (#227)

- **Source:** our reading of the game exe on disk, as shipped (ADR 0002: nothing patched, unpacked or run). Tools: a
  throwaway .NET 9.0 reader on Iced 1.21.0 (RTTI to vtables, vtable slots, `.pdata` function bounds, call sites, byte
  pattern counts; method as in `screen-class-link-2026-11-17.md`), not committed; and `tools/Metaphor.SignatureCheck
  --probes` with `screen-class-probes.json`.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Class names and offsets only: identifiers, not game content (ground rule 4).

## Findings

**Slot 10 of the tabbed choices makes their three cursor managers, and it can be hooked.** Confidence: high for the
code; that the game calls it each time archetype details opens is `[NEEDS VERIFICATION]` until a run.

- The tabbed getter `0xd976f0` (`screen-class-link-2026-11-17.md`) and its copies `0xd9e030` and `0xe4df10` are
  byte-identical through their end and the padding after it, so no pattern starting at the getter is unique.
- Every choice class with a getter in slot 11 uses one of four: `0xd4ab90` (279 vtables) or the three tabbed copies
  (9 vtables, all `ClassDetailBase<…>` and its subclasses). No other screen getter exists, so any screen not named by
  the existing listener is one of these 9.
- Slot 10 (`+0x50`) of the 5 vtables on `0xd976f0` (`ClassDetailBase<CampArchetypeDetailLayout>`,
  `EquipClassDetailChoice`, `StatusMenuClassDetailChoice`, `AcademiaClassDetailChoice`, `BTL_ClassChange`) is
  `0xd8adf0`, `void (Choice* this)`. It calls `0x4c3150` three times (makes a shared pointer to a new `0xE0`-byte object)
  and move-assigns each result (`0xd776b0`) into `this+0x1740`, `+0x1750` and `+0x1760`: the three managers the getter
  returns by tab. For the 279 plain choices slot 10 is `0x7febc0`, a bare `ret`.
- The other two families have their own slot 10 (`0xd9bc50`, `0xe46160`) with the same instructions; the three differ
  only in their call displacements. The 43-byte pattern in `screen-class-probes.json`, call displacements included,
  matches once (at `0xd8adf0`), so it is tied to this build's code layout.

## The listener (Debug, opt-in)

`ClassDetailCursorSetupListener` hooks `0xd8adf0` when `opt-in-screen-class.txt` is in the mod's folder (the same file
as `ChoiceCursorGetterListener`). After each call it reads the three manager pointers and records the choice as their
owner, so each later cursor input on archetype details logs `screen … ownerClass=<class>`. If archetype details still
logs `owner=none`, slot 10 wasn't called after the hook went in, or the managers were replaced later.

## Archetype Tree

Not read here. If it is a choice with a cursor, its getter is `0xd4ab90` (above), so `ChoiceCursorGetterListener`
names it; RTTI candidates are `AcademiaTopChoice`, `AcademiaClassSelectChoice` and the layout class
`Tree@tree@academia@layout@ui@app` `[NEEDS VERIFICATION]`.

## Steps

1. `slot 11` of every `@ope@ui@app` vtable, grouped by target; then the vtables holding `0xd976f0`, `0xd9e030`,
   `0xe4df10`.
2. Compare the 5 tabbed vtables' 72 slots with `ResultClassDetail`'s; disassemble the slots unique to the family.
3. Count the whole bytes of each candidate in the executable sections, with and without call displacements.
4. `--probes <exe> tools/Metaphor.SignatureCheck/screen-class-probes.json`: `ClassDetailSetupCursors` 1 match at rva
   `0xd8adf0`.
