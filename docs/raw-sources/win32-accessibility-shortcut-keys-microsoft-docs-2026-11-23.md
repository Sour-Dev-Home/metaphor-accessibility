# What Microsoft documents about SystemParametersInfo and the Sticky, Toggle and Filter Keys shortcuts (#821)

- **Source:** Microsoft Learn, read on the date below; the quotes are from each page as read.
  - [SystemParametersInfoA](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfoa)
    (`ms.date` 2024-03-27, updated 2025-07-01).
  - [STICKYKEYS](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-stickykeys),
    [TOGGLEKEYS](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-togglekeys),
    [FILTERKEYS](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-filterkeys) (each `ms.date`
    2018-12-05, sdk-api-build commit `9267262487657894a8af112d7165006fed5035a7`).
  - [Disabling Shortcut Keys in Games](https://learn.microsoft.com/en-us/windows/win32/dxtecharts/disabling-shortcut-keys-in-games)
    (`ms.date` 2022-01-26, win32-pr commit `26465e18219d6311381977f154b0a5b7ed53c8d6`).
  - [WM_ACTIVATE](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-activate) and
    [WM_DESTROY](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-destroy) (each `ms.date` 2025-07-14, win32-pr
    commit `3aee48ac3f48e187d17bdfd56951741737331749`).
- **Saved:** 2026-11-23.
- **Game build:** none; this is about Windows, not the game.

## SystemParametersInfo

Signature: `BOOL SystemParametersInfoA(UINT uiAction, UINT uiParam, PVOID pvParam, UINT fWinIni)`. Return value: "If the
function succeeds, the return value is a nonzero value. If the function fails, the return value is zero."

| Action | Value | Page's text (shortened to its first sentence) |
|---|---|---|
| `SPI_GETFILTERKEYS` | `0x0032` | "Retrieves information about the FilterKeys accessibility feature." |
| `SPI_SETFILTERKEYS` | `0x0033` | "Sets the parameters of the FilterKeys accessibility feature." |
| `SPI_GETTOGGLEKEYS` | `0x0034` | "Retrieves information about the ToggleKeys accessibility feature." |
| `SPI_SETTOGGLEKEYS` | `0x0035` | "Sets the parameters of the ToggleKeys accessibility feature." |
| `SPI_GETSTICKYKEYS` | `0x003A` | "Retrieves information about the StickyKeys accessibility feature." |
| `SPI_SETSTICKYKEYS` | `0x003B` | "Sets the parameters of the StickyKeys accessibility feature." |

Each row also says: "Set the **cbSize** member of this structure and the *uiParam* parameter to `sizeof(...)`."

## The structures and their flags

- `STICKYKEYS` and `TOGGLEKEYS` are `{ UINT cbSize; DWORD dwFlags; }` (8 bytes). `FILTERKEYS` is `{ UINT cbSize;
  DWORD dwFlags; DWORD iWaitMSec; DWORD iDelayMSec; DWORD iRepeatMSec; DWORD iBounceMSec; }` (24 bytes).
- `SKF_STICKYKEYSON` `0x00000001`: "If this flag is set, the StickyKeys feature is on." `SKF_HOTKEYACTIVE` `0x00000004`:
  "If this flag is set, the user can turn the StickyKeys feature on and off by pressing the SHIFT key five times."
  `SKF_CONFIRMHOTKEY` `0x00000008`: "A confirmation dialog appears when the StickyKeys feature is activated by using the
  hot key."
- `TKF_TOGGLEKEYSON` `0x00000001`, `TKF_HOTKEYACTIVE` `0x00000004` ("by holding down the NUM LOCK key for eight
  seconds"), `TKF_CONFIRMHOTKEY` `0x00000008`.
- `FKF_FILTERKEYSON` `0x00000001`, `FKF_HOTKEYACTIVE` `0x00000004` ("by holding down the RIGHT SHIFT key for eight
  seconds"), `FKF_CONFIRMHOTKEY` `0x00000008`.

## Microsoft's advice to games

"The accessibility features are required for some customers and do not themselves interfere with full-screen games;
therefore, you should not change the accessibility settings. However, because the shortcuts for accessibility features
can disrupt game play if triggered accidentally, you should turn off an accessibility shortcut only when that feature is
not enabled". "An accessibility shortcut that is turned off by SystemParametersInfo remains turned off even after the
application has exited. This means that you must restore the settings before exiting the application."

Its steps: "1. Capture the current accessibility settings before disabling them. 2. Disable the accessibility shortcut
when the application goes into full-screen mode if the accessibility feature is off. 3. Restore the accessibility
settings when the application goes into windowed mode or exits." Its sample (DXUT) saves the three structures with
`SPI_GET*` at startup; to disable, for each feature whose `*_ON` flag is clear, it clears `*_HOTKEYACTIVE` and
`*_CONFIRMHOTKEY` on a copy of the saved structure and sets that; to allow, it sets the saved structures back.

## Window messages

- `WM_ACTIVATE` is `0x0006`. wParam's low word is `WA_INACTIVE` (0, deactivated), `WA_ACTIVE` (1) or `WA_CLICKACTIVE`
  (2); "The high-order word specifies the minimized state ... A nonzero value indicates the window is minimized."
- `WM_DESTROY` is `0x0002`: "Sent when a window is being destroyed."
