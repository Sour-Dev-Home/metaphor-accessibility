# How the installer exe could be built

The comparison behind [ADR 0020](../../adr/0020-installer-exe-is-a-net-framework-launcher.md), which is proposed, not
decided. What the exe must do is settled in #608 and [ADR 0017](../../adr/0017-ship-unsigned-sign-later-with-signpath.md):
one unsigned file beside the zip that carries the release files, checks them against SHA-256 values built into it,
and starts the one installer `install.cmd` runs; per-user, no administrator rights; rebuilt and compared, attested,
and signed later as the last step. This page only weighs how to build it. Quotes and facts are in
[installer exe build options](../../raw-sources/installer-exe-build-options-2026-11-21.md) unless another source is
linked.

## The options

- **A. A small C# launcher for .NET Framework 4.8.** A console exe that embeds the release files as one resource and
  their hashes, unpacks them to a fresh folder in the player's own temp folder, checks them, and runs `install.ps1`
  exactly as `install.cmd` does (Windows PowerShell 5.1 by full path, `-ExecutionPolicy Bypass`, `-FromLauncher`),
  in the same console. It waits, deletes the folder and passes on the exit code. .NET Framework 4.8 or 4.8.1 is part
  of every supported Windows 10 and 11, so nothing is installed first.
- **B. The same launcher on .NET 9, published single-file.** Framework-dependent needs the .NET 9 runtime, which the
  installer itself may be about to install (step 4 of `install.ps1`). Self-contained carries the runtime and is
  "large"; trimmed or native AOT is smaller but needs trimming and, for AOT, Visual Studio's C++ workload on the
  runner.
- **C. A native-AOT C# port of the installer.** The exe *is* the installer. About 2,000 lines of PowerShell and their
  Pester tests are rewritten, and the zip either ships the port too or runs a different installer, against #608's
  "the one installer".
- **D. An existing tool:** Inno Setup or NSIS (a setup wizard that unpacks, then runs our script), WiX (an MSI), or
  PS2EXE (compiles a script into a .NET Framework exe).

## Judged

| | A. .NET Framework launcher | B. .NET 9 single-file | C. AOT port | D. Inno/NSIS/WiX/PS2EXE |
| --- | --- | --- | --- | --- |
| Blind use | The spoken installer as today, in the exe's console; no clicks | Same as A | New UI to make speak again | A wizard's pages first (Inno, NSIS, MSI) unless run silent; PS2EXE runs only the script |
| Rebuild-compare (#613) | Roslyn is deterministic by default; payload is the reproducible zip's files | Bundle determinism undocumented; AOT output undocumented | Same as B | Undocumented for all four `[NEEDS VERIFICATION]` |
| Attestation | `actions/attest`, as the zip | Same | Same | Same |
| Signing later (#609) | Plain PE, sign last | Same; SDK hooks before bundling | Same | Inno and NSIS sign their own stub; untested with SignPath |
| Installed apps (#614) | Written by `install.ps1` (HKCU), so the zip gets it too | Same as A | In C# | Tool writes its own entry, a second one beside #614's |
| Size | Zip's payload plus a small exe `[NEEDS VERIFICATION]` | Self-contained adds the runtime | Same as B | Payload plus the tool's stub |
| Licence | Our code only; .NET Framework is part of Windows; SDK is MIT | Ships .NET runtime files (MIT `[NEEDS VERIFICATION]`) | Same as B | Inno permissive, NSIS zlib, WiX Ms-RL plus a maintenance fee, PS2EXE Ms-LPL; GPL fit `[NEEDS VERIFICATION]` |
| AV, SmartScreen | Unsigned, unknown hash: SmartScreen warns, Smart App Control may block ([code signing options](../../raw-sources/code-signing-options-2026-11-21.md)); heuristics on "exe starts PowerShell" `[NEEDS VERIFICATION]` | Same | Fewer script launches | Same, plus PS2EXE's clear-text script |
| Maintenance | A few hundred lines of C#, xUnit tests | Same, plus runtime and trim upkeep | Two installers or a full rewrite | A second build language and tool pin |

## Recommendation for the maintainer

**A.** It adds no runtime to ship or install, the only shipped code is ours, the compiler's determinism is documented,
and it keeps the zip and the exe on the same spoken installer and the same Installed-apps code. B is the fallback if a
.NET Framework build proves awkward in CI. C and D cost more than they save.

## Open questions

1. Unpack into a fresh folder under `%TEMP%` (per-user, deleted after), or a fixed one under `%LOCALAPPDATA%`?
2. Is the `Microsoft.NETFramework.ReferenceAssemblies` package (build-time only) an acceptable pin, if the SDK needs it?
3. Should the exe be a console app, so the screen reader reads the installer's console as it does with `install.cmd`?
4. Do Arm64 PCs matter now? An AnyCPU .NET Framework exe on Arm64 Windows is `[NEEDS VERIFICATION]`.
