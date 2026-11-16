# CriFs V2 Hook's signature patterns for Metaphor: ReFantazio

- **Source:** <https://github.com/Sewer56/CriFs.V2.Hook.ReloadedII/blob/82c79eef28290fc8e1879fa277c49bd8a36c98e7/CriFs.V2.Hook/CRI/CpkBinderPointers.cs>,
  the file's latest commit (`82c79eef`, 2025-03-09), fetched with `gh api`. License LGPL-3.0
  (`docs/raw-sources/modding-tools-survey-2026-11-13.md`). Quoted: the Release entry's patterns, where the Demo entry
  differs, and the loop that uses them.
- **Saved:** 2026-11-15.
- **Game build:** none named. The entries are labelled by source, below, not by executable hash. Whether the "Release"
  set matches build `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` is what issue #21 checks.

## The two Metaphor entries

`CpkBinderPointers.cs` lists pattern sets per game, each with a `SourcedFrom` label. Two are Metaphor's:

- `"Metaphor: ReFantazio (Demo)"`
- `"Metaphor: ReFantazio (Release) (Code Fucked [Modified] by Denuvo Anti-Tamper)"`

Both have `CriVersion = "CRI File System/PCx64 Ver.2.85.1 Build:Oct  2 2023 10:26:25"`.

The Release set, verbatim (lines 738–767 at that commit):

```text
CriFs_CalculateWorkSizeForLibrary = "48 89 5C 24 ?? 48 89 74 24 ?? 55 57 41 54 41 56 41 57 48 8D 6C 24 ?? 48 81 EC A0 00 00 00 48 8B 05 ?? ?? ?? ??"
CriFs_InitializeLibrary = "49 89 E3 49 89 5B ?? 49 89 6B ?? 49 89 7B"
CriFs_FinalizeLibrary = "48 83 EC 28 83 3D ?? ?? ?? ?? 00 75 ?? 48 8D 15 ?? ?? ?? ?? 33 C9 E8 ?? ?? ?? ?? 83 C8 FF E9 ?? ?? ?? ??"
CriFsBinder_BindCpk = "48 83 EC 48 48 8B 44 24 ?? C7 44 24 ?? 01 00 00 00"
CriFsBinder_BindFile = "48 83 EC 48 48 8B 44 24 ?? 48 89 44 24 ?? 8B 44 24 ?? 89 44 24 ?? 4C 89 4C 24 ?? 41 B9 01 00 00 00"
CriFsBinder_BindFiles = "48 83 EC 48 48 8B 44 24 ?? 48 89 44 24 ?? 8B 44 24 ?? 89 44 24 ?? 4C 89 4C 24 ?? 41 83 C9 FF"
CriFsBinder_Find = "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 40 49 8B F9 49 8B D8 48 8B F2"
CriFsBinder_GetSizeForBindFiles = "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 81 EC 50 04 00 00 48 8B 05 ?? ?? ?? ??"
CriFsBinder_GetStatus = "48 89 5C 24 ?? 57 48 83 EC 20 48 89 D3 89 CF 85 C9"
CriFsBinder_SetPriority = "48 89 5C 24 ?? 57 48 83 EC 20 8B FA E8 ?? ?? ?? ?? 48 8B D8 48 85 C0 75 ?? 8D 58 ??"
CriFsBinder_Unbind = "48 89 5C 24 ?? 57 48 83 EC 20 8B F9 E8 ?? ?? ?? ?? 48 8B D8"
CriFsLoader_RegisterFile = "48 8B C4 48 89 58 ?? 48 89 70 ?? 4C 89 60 ??"
CriFsIo_Exists = "48 89 5C 24 ?? 57 48 81 EC 70 08 00 00"
CriFsIo_Open = "48 8B C4 48 89 58 ?? 48 89 68 ?? 48 89 70 ?? 57 41 54 41 55 41 56 41 57 48 83 EC 50 4D 8B E9"
CriFsIo_IsUtf8 = "83 3D ?? ?? ?? ?? 00 74 ?? E8 ?? ?? ?? ?? 48 8D 4C 24 ?? C7 44 24 ?? 11 04 00 00 48 89 4C 24 ?? 4C 8B C7"
DisableFileBindWarning = ""
```

Two Release patterns differ from the Demo set (lines 705–734); the other 13 and the empty `DisableFileBindWarning`
are identical:

| Pattern | Demo | Release |
|---|---|---|
| `CriFs_InitializeLibrary` | `4C 8B DC 49 89 5B ?? 49 89 6B ?? 49 89 7B ?? 41 56 48 83 EC 60` | `49 89 E3 49 89 5B ?? 49 89 6B ?? 49 89 7B` |
| `CriFsBinder_GetStatus` | `... 48 8B DA 8B F9 85 C9` | `... 48 89 D3 89 CF 85 C9` |

The differing bytes
encode the same register moves with the other operand direction (for example `4C 8B DC` and `49 89 E3` both
encode `mov r11, rsp`), consistent with the label's "Modified by Denuvo Anti-Tamper"
`[NEEDS VERIFICATION: disassembly of both byte strings]`.

## How the mod uses them

After the table, the code tries every set and keeps whatever each pattern finds (lines 1038–1044):

```csharp
foreach (var pos in possibilities)
{
    if (!String.IsNullOrEmpty(pos.Patterns.CriFs_CalculateWorkSizeForLibrary))
    {
        helper.FindPatternOffsetSilent(pos.Patterns.CriFs_CalculateWorkSizeForLibrary,
            offset => pos.Results.CriFs_CalculateWorkSizeForLibrary = baseAddr + offset);
    }
```

Where `helper` scans (whole module, or one section) and when it runs relative to the game's start weren't checked
`[NEEDS VERIFICATION]`.
