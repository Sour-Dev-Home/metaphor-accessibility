# Title menu item images: the third item shows "System", the message says Settings (#537)

- **Source:** the local `base.cpk`, read offline on 2026-11-20 with throwaway Python 3.12.10 scripts (standard library
  only, not committed) and viewed as PNG; FFmpeg 9.0.2 was tried first and can't decode BC7. Nothing extracted is kept
  in the repository: the files went to the git-ignored `extracted/`.
- **Saved:** 2026-11-20.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Finding

The title menu's item words are images, one texture per language, not message text. The English texture holds
`NEW GAME`, `LOAD GAME`, `SYSTEM`, `SETTINGS`, `CREDITS`, `TITLE MENU` and `PRESS ANY BUTTON`. The title menu has
three items; its third opens the two-item submenu (Settings, Credits:
[`title-system-submenu-2026-11-17.md`](title-system-submenu-2026-11-17.md)), so its image is `SYSTEM`, which the
by-ear check of 2026-11-20 (#537) saw on screen.

`Title_System_08` in `EN/message/system/Title_System.msg`, which the mod reads for the items
([`title-menu-labels-2026-11-16.md`](title-menu-labels-2026-11-16.md)), has the lines `NEW GAME`, `LOAD GAME`,
`SETTINGS`. No message in that file says System. So lines 1 and 2 match their images and line 3 doesn't.

## Method

1. **The archive's file table.** `base.cpk` starts with `CPK ` and an unencrypted `@UTF` header table; its
   `TocOffset` leads to a `TOC ` table with `DirName`, `FileName`, `FileSize`, `ExtractSize` and `FileOffset` (offset
   from the lower of `TocOffset` and `ContentOffset`). The title menu's files are `COMMON/ui/ss/title_menu.ssbp`,
   `COMMON/ui/ss/title_menu_1.apk` and `_2.apk` (with `4K/` copies), and one `<LANG>/ui/ss/title_menu_la.apk` per
   language (`EN`, `DE`, `ES`, `ES-419`, `FR`, `IT`, `JP`, `KO`, `PT-BR`, `RU`, `ZH-CN`, `ZH-TW`, each with a `4K/`
   copy). None of these were compressed in the archive (`FileSize` = `ExtractSize`).
2. **The texture.** `EN/ui/ss/title_menu_la.apk` (21,424 bytes) is a `PACK` holding one entry named
   `title_menu_la.dds`, stored as a `ZZZ0` block: uncompressed size at `+0xc`, compressed size at `+0x20`, data at
   `+0x30`, compressed as an LZ4 block. Unpacked, it is a DDS with a `DX10` header, DXGI format 98 (BC7), 1024 × 256;
   the `4K/` copy is 2048 × 512.
3. **Viewing.** BC7 decoded to PNG with a stdlib script that implements modes 1 and 3–7 and paints 3-subset blocks
   (modes 0 and 2) magenta. No magenta appeared and every word decoded clean, so the reading holds.
4. **Another language.** `DE/ui/ss/title_menu_la.apk`, same steps: `NEUES SPIEL`, `SPIEL LADEN`, `SYSTEM`,
   `EINSTELLUNGEN`, `MITWIRKENDE`, `STARTMENÜ`. German `Title_System_08`'s third line is `EINSTELLUNGEN`, so the
   mismatch isn't English only.
5. **The code.** `TitleChoice::vf17` (`0xe2ec80`, shared Ghidra project) fills the three title items with an index and
   an enabled flag only; it passes no message.

## Not established

- Which sprite each item draws: `title_menu.ssbp` (SpriteStudio) maps parts to the texture, and it wasn't read. That
  item 3 is the `SYSTEM` image rests on the on-screen sighting and the submenu it opens. `[NEEDS VERIFICATION]` for the
  mapping itself.
- Whether any game message says System in each language; the mod's word is English only.
