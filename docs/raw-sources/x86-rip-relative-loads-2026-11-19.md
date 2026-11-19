# x86-64 encoding of a RIP-relative mov or lea

- **Source:** sandpile.org's x86 tables <https://www.sandpile.org/x86/opc_rm.htm> (the ModR/M byte) and
  <https://www.sandpile.org/x86/opc_enc.htm> (the REX prefix); Félix Cloutier's x86 reference, which is extracted
  from Intel's Software Developer's Manual, vol. 2: <https://www.felixcloutier.com/x86/mov> and
  <https://www.felixcloutier.com/x86/lea>. Read with a web fetch tool.
- **Saved:** 2026-11-19.
- **Game build:** none. This is the instruction set, the same for every build; each global spec names its own builds.

## Quotes

The REX prefix (opc_enc.htm): its high nibble is fixed, "0100b = 4xh", followed by the bits W, R, X and B; "the REX
prefix must immediately precede the opcode byte(s)". W: "0 = default operand size", "1 = 64-bit operand size". R:
"mod R/M byte reg field extension".

The ModR/M byte (opc_rm.htm): the table's cell for mod=00, r/m=101 points to footnote 1: "if PM64 and adsize=64, then
effective address = [RIP+sdword]".

The opcodes (felixcloutier.com):

- MOV: "REX.W + 8B /r | MOV r64, r/m64 | RM | Valid | N.E. | Move r/m64 to r64."
- LEA: "REX.W + 8D /r | LEA r64,m | RM | Valid | N.E. | Store effective address for m in register r64."

## What we take from it

A 64-bit `mov reg,[rip+disp32]` or `lea reg,[rip+disp32]` is REX (`48` with W set, or `4C` with W and R set, for
r8-r15), then `8B` or `8D`, then a ModR/M byte with mod 00, any reg and r/m 101 (`05`, `0D`, `15`, ... `3D`), then the
4-byte signed displacement ("sdword"): 7 bytes in all. Our two globals' loads, `48 8B 0D`
followed by four bytes, fit this shape (`hook-signatures.json`).
