---
status: proposed
---

# We reverse engineer only to interoperate, locally, and ship nothing of the game's

Asked for by the maintainer on 2026-11-17; proposed for their review. We study the game (a local Ghidra database of
the exe, memory reads, Reloaded-II hooks) only to make an independently written screen-reader mod interoperate with it
for accessibility. The analysis stays on the contributor's machine. We never distribute game code, assets, decompiled
output or databases (ground rule 4), never circumvent Denuvo or anti-tamper and never touch online features (ground
rule 5, ADR 0002). The mod is non-commercial and GPL-3.0.

That scope is the one the interoperability exceptions describe: EU Directive 2009/24/EC Articles 5(3) and 6, which
Article 8 says a contract can't override, and in the US 17 U.S.C. § 1201(f) and the fair-use holdings of Sega v.
Accolade and Sony v. Connectix. The texts are in
[`legal-basis-reverse-engineering-2026-11-17.md`](../raw-sources/legal-basis-reverse-engineering-2026-11-17.md).

## Known grey area

The game's EULA (7(e)) and the Steam Subscriber Agreement (2.G) forbid reverse engineering and modification, each
"except where permitted by law". That is a contract question, not copyright, and the US cases and § 1201(f) don't
settle it. Its reach depends on the player's country:
[`eula-and-mod-stance-2026-11-16.md`](../raw-sources/eula-and-mod-stance-2026-11-16.md).

This is not legal advice, and no lawyer has reviewed it.

## Consequences

A feature that needs anything outside this scope (sharing decompiled code, defeating a protection, an online feature)
doesn't ship. Notes quote only the short snippets a finding needs.
