# Sources for the players' install check (#283)

- **Source:** Prism's source at tag `v0.18.3` (commit `94329ebeccbaee7f70b3494a290c9dfaac0184ee`), NVDA's user guide at
  commit `0cede9681d10283d1a3482c393f8e55b0a27bd98` (`master` on the date below), both read with `gh api
  repos/<owner>/<repo>/contents/<path>?ref=<commit>`; BMMValidator's project and latest commit from GitLab's REST API
  (`/api/v4/projects/BMMProject%2FTools%2FBMMValidator?license=true` and `.../repository/commits/main`); and runs of
  `scripts/check-install.ps1` on Windows 11 with Windows PowerShell 5.1.26100 and PowerShell 7.7.0. Nothing was
  downloaded or installed.
- **Saved:** 2026-11-19.
- **Game build:** none for the sources; the runs were on the build with SHA-256 `548ddc95...` (Steam build 18330018).

The Reloaded-II facts the check relies on are in `reloaded-ii-config-location-2026-11-13.md` (the fixed path of
`ReloadedII.json`, `ModConfigDirectory`, `ApplicationConfigDirectory`) and `reloaded-ii-app-and-mod-config-2026-11-15.md`
(`AppConfig.json`'s `AppLocation` and `EnabledMods`, one `ModConfig.json` per mod folder, `ModId`).

## How Prism finds the screen readers the mod uses

`P` below means <https://github.com/ethindp/prism/blob/94329ebeccbaee7f70b3494a290c9dfaac0184ee>. The mod's fixed list is
NVDA, JAWS, ZoomText, then OneCore and SAPI (ADR 0005).

- JAWS: the backend checks `IsWindow(FindWindow(_T("JFWUI2"), nullptr))` before it uses the JAWS API
  (`P/source/backends/jaws.cpp#L34`, `#L45`).
- ZoomText: it checks `FindWindow(_T("ZXSPEECHWNDCLASS"), _T("ZoomText Speech Processor"))`
  (`P/source/backends/zoom_text.cpp#L37`, `#L49`).
- NVDA: it binds over `ncalrpc` to the endpoint `NvdaCtlr.<session id>.<desktop name>` and calls
  `nvdaController_testIfRunning` (`P/source/backends/nvda.cpp#L102-L103`, `#L164`). No window or process name is used.

## NVDA's program name

- The user guide: "To start the portable version, go to the directory you unpacked NVDA to, and press enter or double
  click on nvda.exe." (`nvaccess/nvda`, `user_docs/en/userGuide.md#L526`). The check looks for a process named `nvda`.
  That an installed copy runs under the same name is [NEEDS VERIFICATION].

## BMMValidator

- <https://gitlab.com/BMMProject/Tools/BMMValidator>: "A command-line tool designed to validate installation of
  accessibility mods." Licence Apache-2.0 (`apache-2.0` in the API). Default branch `main`, latest commit
  `d69404ee9fab7df5cbb178d94caa2ecdaf0f538b` (2026-09-24, "docs: add link to clone project").
- Only its idea is used: a checker players run on their install that says what is wrong. No code was read or copied.

## Runs

1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <package>/check-install.ps1 -NoSpeech`, with the package
   from `scripts/package-mod.sh`, started from Git Bash inside a PowerShell 7 session: the first version failed with
   "The term 'Get-FileHash' is not recognized". `$PSModulePath` there listed PowerShell 7's module folders first, which
   Windows PowerShell 5.1 inherited. Hashing through .NET's `SHA256` (`ReloadedDev.psm1`'s `Get-Sha256`) avoids the
   module.
2. The same run after that change printed a pass for Reloaded-II, the mod installed and enabled, the game version,
   `prism.dll` and the last start (20 of 20 hooks), and a problem for the screen reader, with NVDA off.
3. `Add-Type -AssemblyName System.Speech` and `New-Object System.Speech.Synthesis.SpeechSynthesizer` load in PowerShell
   7.7.0 as well as 5.1. Nothing was spoken during the check.
4. In Windows PowerShell 5.1, `Find-ScreenReader` (whose `Add-Type` compiles the `FindWindow` helper with the C#
   compiler) left no new or changed entry at the top of the temp folder once it returned. Whether the compiler writes
   files deeper down while it runs wasn't checked, so the docs say it writes temporary files and removes them.
