@echo off
rem The release zip's first file (#604): scripts/release/zip-package.ps1 puts it at the zip's top level, named
rem "Install Metaphor accessibility.cmd", beside the mod's folder, so a player extracts the zip and opens one file with
rem Enter instead of looking for install.cmd among the mod's files. It is not in the mod's own folder.
rem
rem It hands over to install.cmd in the mod's folder, which holds the launcher's logic (PowerShell 5.1 by full path,
rem the policy bypassed for that process only, the closing line, the pause and the exit code). Naming install.cmd
rem without "call" passes control to it for good, so its exit code is this file's, and the path is never expanded a
rem second time: "call" would re-read a percent sign or a caret in the folder's name. Arguments pass through, for
rem testing; players need none. zip-package.ps1 refuses a launcher that doesn't name the mod's folder as below.
rem
rem Opened inside the zip without extracting it, File Explorer copies this one file out alone, so the folder isn't
rem beside it: it then says so and waits for a key, so the window doesn't close before a screen reader reads it.
rem
rem ASCII only, Windows line endings (.gitattributes): cmd.exe reads this file in the console's code page.
if not exist "%~dp0metaphor.accessibility\install.cmd" (
  echo The mod's folder, metaphor.accessibility, is not beside this file. Extract the whole zip first, then open this
  echo file in the extracted folder. Press any key to close this window.
  pause >nul
  exit /b 1
)
"%~dp0metaphor.accessibility\install.cmd" %*
