# Which analysis methods stay inside rule 5 while the game uses Denuvo

The Steam release uses Denuvo Anti-tamper (`docs/raw-sources/drm-status-2026-11-13.md`), and ground rule 5 forbids
bypassing it. We study the game only in ways that leave the protection untouched, and we stop the moment a method needs
anything that defeats it.

| Method | Allowed |
|---|---|
| Reading the open-source mods that already hook this game (OpenGFD, MF.Toolkit, MetaphorFix and others) | Yes |
| Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do | Yes |
| Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable | Yes |
| Reading the game's own data archives (CPK) offline with our read-only tool (`tools/Metaphor.CpkExtract`), output kept in git-ignored `extracted/` | Yes |
| Attaching a debugger to the running game | Only if it works with no help; if the game resists, stop |
| Anti-debug hiding plugins, dumping or unpacking protected code, extracting decryption keys from protected code, patching or disabling Denuvo checks | No |

## Consequences

When a feature seems to need a method from the last row, we look for another route: a different hook point, data the
game already exposes, or what the community mods have found. If there is none, the feature waits. Every note about a
finding records how it was obtained, so a reviewer can check it against this table.
