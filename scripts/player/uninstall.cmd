@echo off
rem Starts the spoken uninstall, install.ps1 -Uninstall beside this file, for a player who opens this file with Enter in
rem File Explorer (#575). It is install.cmd with -Uninstall added; keep the two the same. Arguments pass through, such
rem as -Speak or -NoSpeech.
rem
rem - Windows PowerShell 5.1 by its full path, so no powershell file in this folder or on PATH runs instead.
rem - -ExecutionPolicy Bypass holds for this one process only; nothing is saved. A Group Policy setting still wins
rem   (docs/raw-sources/install-launcher-motw-2026-11-20.md).
rem - install.ps1 is found from this file's own folder (dp0 below), so any working folder works, and the quotes keep
rem   a path with spaces or an ampersand whole.
rem - install.ps1 keeps its own checks: it refuses to run as administrator, and asks before it removes anything.
rem - The pause keeps the window open, so a screen reader can read the result, and passes on install.ps1's exit code.
rem   -FromLauncher has install.ps1 say, through the screen reader too, that the uninstall has finished and to press a key.
rem
rem ASCII only, Windows line endings (.gitattributes): cmd.exe reads this file in the console's code page.
setlocal
rem Started from a PowerShell 7 window, the module path names PowerShell 7's modules, which 5.1 can't load; with none
rem set, 5.1 uses its own.
set "PSModulePath="
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -Uninstall -FromLauncher %*
set "result=%errorlevel%"
pause >nul
exit /b %result%
