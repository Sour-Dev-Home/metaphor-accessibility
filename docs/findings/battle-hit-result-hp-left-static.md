# One battle GUI function turns each hit into a popup, with the damage and the HP left

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on
  [`battle-turn-icons-and-ui-components-static.md`](battle-turn-icons-and-ui-components-static.md) and
  [`battle-state-session6-2026-11-17.md`](../raw-sources/battle-state-session6-2026-11-17.md)
- **Functions:**
  - BattleHitResult | 0x3231a0 | .ecode | Battle | hooked | Runs once per target of a resolved action and sends the damage popup. | finding; src/Metaphor.Hooks/BattleHooks.cs

`0x3231a0` (`BattleHitResult`, our name) runs once per target of a resolved action and sends the damage popup. Before
it sends, it writes into its first argument the HP and MP change and the HP and MP the target has left, clamped to
0..max. A hook on it gives "35 damage, 89 HP left" per hit without reading the popup. Static only: nothing here has
run against the game.

## Evidence

- **Callers.** Three, all in the battle GUI: `0x326720` (the GUI update that `Package::Update` calls each frame, per
  the leads note on #294's branch), `0x324700` and `0x325c50`. The function is in `.ecode`; nothing it calls was
  followed into `.text1`.
- **Arguments** `(request*, gui*, result*, target*, ushort index, int mode)`, read from the body:
  - `result` holds one 0x20-byte row per target: row `index` `+0x0` the HP change, `+0x4` the MP change, `+0xC`
    flags (`0x80000` and the low 19 bits each add a popup tag through `0xf1ab10`). After the rows, `result+0x300` is a
    flag word, `+0x304` a short kind and `+0x306` a short sub-kind.
  - `target` is the hit unit: `*(*(target+0x18)+0x8)` is its unit record (`0x268130` returns `[x+8]`). `0x3e60f0`
    reads the record's `+0x0C` (HP) and `0x3e6c00` its `+0x10` (MP), the offsets session 6 checked for party members.
- **What it writes** into `request` (as `int` indices): `[0xC]` (`+0x30`) the HP change, `[0xD]` (`+0x34`) the MP
  change, `[0xE]` (`+0x38`) and `[0xF]` (`+0x3C`) values from two getters in thunks (max HP and max MP by how they
  clamp; `[NEEDS VERIFICATION]`), `[0x10]` (`+0x40`) HP + change clamped to 0..`[0xE]`, raised to 1 when it would reach 0
  and `0x322850` returns false (meaning `[NEEDS VERIFICATION]`), and `[0x11]` (`+0x44`) the same for MP.
- **Kinds.** `+0x304` = 4, `0x100` or `0x200` sends a popup with no number (`0xf172a0`, `0xf17250`, `0xf172f0`) and
  returns; 1, `0x400` and `0x1000` send a number, with `+0x306` choosing a style (1, 2 or 4). Which kind is Miss, Null,
  Repel or Drain, and which style is Weak or Critical, is `[NEEDS VERIFICATION]`. The HP-change sign: the earlier note
  read the popup's number as the negated change, so damage is likely negative here `[NEEDS VERIFICATION]`.
- **Signature.** The first 64 bytes, stack and register moves only, match once on disk at rva `0x3231a0`
  (`tools/Metaphor.SignatureCheck/battle-turn-probes.json`, `BattleHitResult`).

So the plan for #247's outcomes: hook `BattleHitResult`, let it run, then read `request+0x30` and `+0x40` (and the
kind at `result+0x304`) and the target's unit record. Which side the target is on comes from comparing the record
with the party units at `G+0x228 + n × 0x16A8` (the turn-icons note). An HP/MP key reads those party units at any
time.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`,
[query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)), build above, 2026-11-20.

1. `decompile 0x3231a0`: the row reads, the writes to `request`, the kind branches. `callers 0x3231a0`.
2. `decompile 0x268130`, `0x3e60f0`, `0x3e6c00`: one-line getters.
3. `dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes <exe> 0x3231a0 64`, then `--probes <exe>
   tools/Metaphor.SignatureCheck/battle-turn-probes.json`: `BattleHitResult` one match.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.
