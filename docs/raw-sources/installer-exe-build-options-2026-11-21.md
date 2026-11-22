# How a single-file installer exe can be built: what the tools' own docs say (#612)

- **Source:** web pages read with a fetch tool on the date below; the quotes are from them. Microsoft Learn,
  [Create a single file for application deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
  (`ms.date` 2026-03-25, dotnet/docs commit `56750bbe8bd123711bca9e3f2617242ec9bfb6e5`);
  [Native AOT deployment overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) (`ms.date`
  2026-11-03, commit `e7ad8fa5e91fd66813b91c0f43a4338bf41f96b0`);
  [Install .NET Framework on Windows](https://learn.microsoft.com/en-us/dotnet/framework/install/on-windows-and-server)
  (`ms.date` 2025-11-06, commit `7a052c5588630809ed908a93c2428446fbd90fa8`);
  [C# compiler options: code generation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/code-generation)
  (commit `82ad3e72f0bf180cc5045c4d345129951d466b6f`);
  [Uninstall registry key](https://learn.microsoft.com/en-us/windows/win32/msi/uninstall-registry-key) (win32-pr commit
  `c3947cbad533e346e961e5425744789a65407850`). Inno Setup's [licence](https://jrsoftware.org/files/is/license.txt) and
  [PrivilegesRequired](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm); NSIS's
  [licence page](https://nsis.sourceforge.io/License); WiX's [LICENSE.TXT](https://github.com/wixtoolset/wix/blob/main/LICENSE.TXT)
  and [README](https://github.com/wixtoolset/wix); PS2EXE's [README](https://github.com/MScholtes/PS2EXE) and
  [LICENSE](https://raw.githubusercontent.com/MScholtes/PS2EXE/master/LICENSE).
- **Saved:** 2026-11-21.
- **Game build:** none; this is about Windows and build tools, not the game.
- **Not reached:** the FSF licence list (gnu.org refused the connection twice), so GPL-3.0 compatibility of Ms-RL and
  Ms-LPL below is `[NEEDS VERIFICATION]`. No tool was installed or run.

## .NET single-file publish

- "Single-file deployment is available for both the framework-dependent deployment model and self-contained
  applications." "The size of the single file in a self-contained application is large since it includes the runtime
  and the framework libraries."
- "Only managed DLLs are bundled with the app into a single executable. When the app starts, the managed DLLs are
  extracted and loaded in memory, avoiding the extraction to a folder." `IncludeNativeLibrariesForSelfExtract` embeds
  the native files for extraction. "If running on Windows, the files are extracted to a directory under `%TEMP%/.net`."
  "To prevent tampering, these directories shouldn't be writable by users or services with different privileges."
- "To avoid shipping loose files entirely, consider using embedded resources."
- Signing: "The dotnet SDK provides MSBuild extension points to allow processing binaries just before single-file
  bundling" (`PrepareForBundle`, `GenerateSingleFileBundle`).
- The page says nothing about whether the bundle itself is byte-for-byte reproducible.

## Native AOT

- Prerequisite on Windows: "Visual Studio 2022 or later, including the **Desktop development with C++** workload".
- Limitations include: "No dynamic loading", "No runtime code generation, for example, `System.Reflection.Emit`",
  "Windows: No built-in COM", "Requires trimming", "Implies compilation into a single file".
- The page says nothing about determinism of the native output.

## .NET Framework in Windows

- ".NET Framework is included on all current versions of Windows and Windows Server."
- ".NET Framework 4.8 was originally included with Windows 11. Starting with Windows 11 22H2 (released September 2022),
  .NET Framework 4.8.1 is included." Windows 10 22H2, "the last supported version of Windows 10", includes 4.8.

## Building for .NET Framework with the .NET SDK

- The .NET SDK's [LICENSE.TXT](https://github.com/dotnet/sdk/blob/main/LICENSE.TXT): "The MIT License (MIT)",
  "Copyright (c) .NET Foundation and Contributors".
- NuGet, [Microsoft.NETFramework.ReferenceAssemblies](https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies):
  latest 1.0.3, "Last updated 8/19/2022", described only as "Microsoft .NET Framework Reference Assemblies". The page
  links its licence without naming it, so the licence and whether an SDK-style `net48` build needs this package are
  `[NEEDS VERIFICATION]`. It is a build-time input, not shipped.

## Deterministic C# compilation

- `Deterministic`: "Causes the compiler to produce an assembly whose byte-for-byte output is identical across
  compilations for identical inputs." "For modern .NET projects, deterministic compilation is enabled by default."
- Inputs that affect it include "The precise version of the compiler used, and its referenced assemblies", "The current
  directory path", and "The binary contents of all files explicitly passed to the compiler ... Resources".
- "Deterministic compilation can be used for establishing whether a binary is compiled from a trusted source."

## The Installed apps entry

- The Uninstall key page names `HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Uninstall` and the values
  `DisplayName`, `DisplayVersion`, `Publisher`, `UninstallString`, `InstallLocation`, `EstimatedSize` and others, as
  Windows Installer writes them. It says nothing about a per-user (HKCU) key or a non-MSI program; that is
  `[NEEDS VERIFICATION]` from another primary source (#614).

## Existing installer tools

- **Inno Setup:** "Permission is granted to anyone to use this software for any purpose, including commercial
  applications", with conditions to keep copyright notices, not misrepresent origin, and mark modified versions.
  `PrivilegesRequired=lowest`: "Setup will not request to be run with administrative privileges" and "will always run
  in non administrative install mode." The page doesn't say where its uninstall entry goes.
- **NSIS:** "All NSIS source code, plug-ins" are "licensed under the zlib/libpng license"; bzip2 "is licensed under the
  bzip2 license"; LZMA "is licensed under the Common Public License version 1.0", with an exception permitting linking
  "without subjecting your linked code to the terms of the Common Public license version 1.0." The page doesn't say
  which licence covers the stub inside a generated installer.
- **WiX:** "This software is released under the Microsoft Reciprocal License (MS-RL)." Its README: "use of the WiX
  Toolset requires an Open Source Maintenance Fee", and "if you use this project to generate revenue, the Maintenance
  Fee is required."
- **PS2EXE** (1.0.18, 2026-06-06): licensed under the Microsoft Limited Public License 1.1. It uses "the compiler of
  .Net Framework" and "generates .Net 4.x binaries"; "the script (intentionally) is stored in clear text in the
  executable!" `-noConsole` makes "a Windows Forms app without a console window." The README says nothing about
  antivirus false positives.
