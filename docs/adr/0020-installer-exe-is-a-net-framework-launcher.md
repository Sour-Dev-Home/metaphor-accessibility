---
status: accepted
---

# The installer exe is a small C# launcher for .NET Framework 4.8

Accepted by the maintainer on 2026-11-22 (#612, under #608 and ADR 0017): x64 only, a console window, a welcome line
before the installer starts; a setup wizard only if players report install trouble.

The exe is our own console program, compiled by the C# compiler in the pinned .NET SDK (9.0.319, `global.json`, MIT),
targeting .NET Framework 4.8, which every supported Windows 10 and 11 includes. It embeds the release files and their
SHA-256 values, unpacks them to a new folder in the player's own temp folder, checks them, then runs `install.ps1`
exactly as `install.cmd` does, in its console. It asks for no administrator rights; the installer writes the per-user
Installed-apps entry (#614). It waits, deletes the folder and returns the installer's exit code. Deterministic
compilation over the reproducible zip's files keeps the rebuild-compare (#613); the only shipped code is ours, so
ADR 0005 holds. Sources: [installer exe build options](../raw-sources/installer-exe-build-options-2026-11-21.md).

## Considered options

- **.NET 9 single-file launcher:** needs the runtime the installer may be about to install, or carries it.
- **Native-AOT port of the installer:** rewrites the tested PowerShell, and the zip would run a different installer.
- **Inno Setup, NSIS, WiX or PS2EXE:** a wizard before the spoken installer, a second uninstall entry, undocumented
  reproducibility, and licences to check.

The full comparison and open questions: [installer exe options](../contributors/explanation/installer-exe-options.md).

## Consequences

A small C# project with xUnit tests joins the release build. SmartScreen warns until signing (#609).
