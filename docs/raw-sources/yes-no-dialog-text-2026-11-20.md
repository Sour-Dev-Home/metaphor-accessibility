# Yes/no dialog text: the select window call takes the prompt's label, and the name entry calls it directly (#569)

- **Source:** our reading of the shipped exe as-is (ADR 0002) in the local Ghidra project
  (`docs/contributors/how-to/query-the-ghidra-project.md`): Ghidra 12.2 with Temurin JDK 25.0.5.8, through
  `scripts/dev/ghidra.ps1` `decompile` and `callers`; the signature's bytes read from the exe's file with a throwaway
  PowerShell 7 script that maps an rva to a file offset by the section table and counts pattern matches in the
  section, then checked with `bash scripts/check.sh signatures`. Nothing was patched or run; the game was not
  launched. `.text1` was not followed.
- **Saved:** 2026-11-20.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Addresses are rvas. `SelectWindowRequest` is our name for `0x9ee5e0`.

## Finding

`0x9ee5e0` (`.ecode`) opens a select window. Its second and third arguments are C strings: the prompt message's label
and the choice message's label. `MsgRequestSelectByName` (`0x9f8d70`) passes them on to it unchanged
([`msg-select-functions-2026-11-16.md`](msg-select-functions-2026-11-16.md)), and the name entry and the difficulty
list call it themselves, so their dialogs never reach the scripts' choice hook. A hook on `0x9ee5e0` hears all three.

## Method

1. **The arguments.** `decompile 0x9ee5e0`: it measures the second and third arguments up to their zero byte and copies
   each into a string, looks each up as a message, and compares the second with the label `MES_npc_0013_010_ARENA_100`
   with `memcmp`: so both are labels, the second the prompt's. The first is compared with 1 to choose which message
   gives a field (the kind); the fourth is 16 bits (`45 0F B7 E9`, `movzx r13d, r9w`); the fifth is an options block.
2. **Callers.** `callers 0x9ee5e0` lists at least 37 calls, among them `MsgRequestSelectByName` (`0x9f8fa7`), the name entry
   function `0x4ffe60` (twice) and `UUIC_Common_DifficultySelect::vf13` (`0xf50130`, three times).
3. **Name entry.** `decompile 0x4ffe60`: on one branch of the byte at `this+0x481` it calls
   `0x9ee5e0(1, "System_NameEntry_Confirm", "System_Choice_YESNO", 1, options)`, on the other the same with
   `"System_NameEntry_08"`. Which branch the first name entry takes is not settled statically; the hook reads whichever
   label is passed. Before either call it copies the typed name (UTF-16 units widened to 17 UTF-32 units) into a vector
   and passes it to `0xa8f74b0` with 0, and on the `_08` branch also to `0x3b4070`; both bodies are protected code that
   doesn't decompile, so whether that sets the text's `<V 0>` is `[NEEDS VERIFICATION]`.
4. **Difficulty list.** `decompile 0xf50130`: on confirm it calls `0x9ee5e0` with `"System_Choice_YESNO"` and a prompt
   of `"System_mode_13"`, `"System_mode_14"`, or, when the byte at `this+0x129` is 0, a label taken from a string list
   at `this+0x78` by the chosen row. `System_mode_12` goes to `0x9ec320`, another function, not to a select window.
5. **Signature.** The first 51 bytes at `0x9ee5e0`, up to the `rip`-relative load that follows, match once in `.ecode`.

## Not established

- Where the typed name is held while the confirmation shows. After confirming, it is in the game-data block
  ([`player-name-in-game-data.md`](../findings/player-name-in-game-data.md)); whether it is there already, or still holds
  the previous or default name, is `[NEEDS VERIFICATION]` in game.
- That the hook fires in memory where it matches on disk: `[NEEDS VERIFICATION]` by a run.
