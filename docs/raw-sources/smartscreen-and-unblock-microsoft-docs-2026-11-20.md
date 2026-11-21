# What Microsoft documents about SmartScreen and unblocking a download (#427)

- **Source:** Microsoft Learn, [Unblock-File](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/unblock-file?view=powershell-5.1)
  (PowerShell 5.1 page, `ms.date` 2022-12-12, PowerShell-Docs commit `3bcc275f46fd8537780ee1549c0862bb0266e06c`); Microsoft
  Learn, [Microsoft Defender SmartScreen](https://learn.microsoft.com/en-us/windows/security/operating-system-security/virus-and-threat-protection/microsoft-defender-smartscreen/)
  (`ms.date` 2026-11-03, windows-docs-pr commit `909b3f84d4a447b8f9e80f4b86c49716dc1bfac7`). Each page was read on the date
  below; the quotes are from it. The mark itself is covered in
  [install-launcher-motw-2026-11-20.md](install-launcher-motw-2026-11-20.md).
- **Saved:** 2026-11-20.
- **Game build:** none; this is about Windows, not the game.

## The unblock step

Unblock-File: "`Unblock-File` performs the same operation as the **Unblock** button on the **Properties** dialog box in
File Explorer." "Internally, the `Unblock-File` cmdlet removes the **Zone.Identifier** alternate data stream, which has a
value of `3` to indicate that it was downloaded from the internet." "Before using the `Unblock-File` cmdlet, review the
file and its source and verify that it is safe to open." "If you use the `Unblock-File` cmdlet on a file that is not
blocked, the command has no effect on the unblocked file and the cmdlet does not generate errors."

The page documents the cmdlet, not the Properties dialog: where the Unblock check box sits in the dialog and what a
screen reader says there are not on it.

## SmartScreen

"Checking downloaded files against a list of files that are well known and downloaded frequently. If the file isn't on
that list, Microsoft Defender SmartScreen shows a warning, advising caution." "If there's no reputation, the item is
marked as a higher risk and presents a warning to the user." "SmartScreen protects against malicious files from the
internet."

The page does not show the "Windows protected your PC" window, and does not name its "More info" link or its "Run
anyway" button. A search for Microsoft's wording found only third-party pages, which agree on the pattern (a blue
window, "More info" reveals "Run anyway") but are not saved here. That wording is `[NEEDS VERIFICATION]` by ear
(#573).
