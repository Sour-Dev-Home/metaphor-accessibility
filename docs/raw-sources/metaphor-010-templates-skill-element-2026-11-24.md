# Metaphor 010 Editor templates: the skill element and enemy attack element enums (#164)

- **Source:** `DeathChaos25/metaphor-010-templates` at commit `cfccaa2f4e4352d2881884b1c7d61c53661259d3`, read through
  the GitHub REST API (`gh api repos/DeathChaos25/metaphor-010-templates/contents/<file>?ref=cfccaa2f…`):
  `Skill.bt`, `metaphor_structs.h`, `metaphor_enums.bt`. No licence file, so nothing is copied: only names, order and
  values.
- **Saved:** 2026-11-24.
- **Game build:** the source names none; whether it matches `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504`
  (Steam build 18330018) is `[NEEDS VERIFICATION]`.

`Skill.bt` declares the 0x22-byte `Skill.TBL` record as an element (`SkillElement`, a byte), an active or passive byte,
then two 16-byte message hashes.

`metaphor_structs.h` declares `SkillElement` as: Slash 0, Pierce 1, Strike 2, Fire 3, Ice 4, Electric 5, Wind 6,
Light 7, Dark 8, Almighty 9, Healing 19, Support 20, Ailment 21. Its affinity entry is a multiplier byte (units of 5 %,
20 neutral, 0 ignored) and a flags byte of Block, Repel, Drain, Weak, Resist, Ailment Immune, Guarantee Ailment,
Double Ailment Chance, bit 0 first. The enemy's attack property starts with a `SkillElement` too.

`metaphor_enums.bt` declares `ElementalAttack` (a short) with the same ten values, Slash 0 to Almighty 9.

So the first ten affinity entries (Slash to Almighty, `metaphor-010-templates-tables-2026-11-17.md`) are in element
order, and a skill's element byte below 10 names its entry.
