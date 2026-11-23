# The .NET Framework 4.8 reference assemblies package: licence and pin

- **Source:** the package's metadata from NuGet with `curl`:
  `https://api.nuget.org/v3-flatcontainer/microsoft.netframework.referenceassemblies.net48/1.0.3/microsoft.netframework.referenceassemblies.net48.nuspec`,
  the same for the meta-package `microsoft.netframework.referenceassemblies`, and the `.nupkg` itself (its file list
  and `build/` targets read with `unzip`). The licence the nuspec links, fetched from
  `https://raw.githubusercontent.com/microsoft/dotnet/master/LICENSE`; GitHub's licence detection with
  `gh api repos/microsoft/dotnet/license` (default branch `main`, head commit
  `fb8d575772ad9a4c39b63e9f71db019e81a48564`; `LICENSE` blob `b73b4a1293c328fdb684ee285e2039f72f4f1c4c`, last changed
  in commit `7cdf34dec038c4da4d14735a34ab259704ec4a1e`, 2019-10-04).
- **Saved:** 2026-11-22
- **Game build:** none (a build tool input, not shipped)

For the installer exe (#608, ADR 0020's open question 2): the launcher targets .NET Framework 4.8, and building it
without the targeting pack installed (CI's Linux runners, a clean Windows) needs Microsoft's reference assemblies.

## The package

- `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3, authors "Microsoft",
  `<developmentDependency>true</developmentDependency>`, `<requireLicenseAcceptance>false</requireLicenseAcceptance>`,
  `<licenseUrl>https://github.com/Microsoft/dotnet/blob/master/LICENSE</licenseUrl>`,
  `<projectUrl>https://github.com/Microsoft/dotnet/tree/master/releases/reference-assemblies</projectUrl>`, copyright
  "© Microsoft Corporation. All rights reserved." It has no dependencies.
- The `.nupkg` (SHA-256 `8a7e348538e7eb91351696911689f49e3d4f63f8bab517432bbe159b8b1104a2`) holds the reference
  assemblies and one `build/Microsoft.NETFramework.ReferenceAssemblies.net48.targets`, which for a v4.8 .NET Framework
  project sets `TargetFrameworkRootPath` to the package's folder, `EnableFrameworkPathOverride` false, `NoStdLib` true
  and references `mscorlib`. It runs no task or program.
- The meta-package `Microsoft.NETFramework.ReferenceAssemblies` 1.0.3 has the same licence URL and depends on one such
  package per framework version, net20 to net481. The launcher references the net48 one alone.

## The licence

`LICENSE` in `microsoft/dotnet` reads "The MIT License (MIT)", "Copyright (c) 2019 Microsoft", with the MIT grant and
warranty disclaimer; GitHub detects it as `MIT`. MIT is GPL-3.0-compatible.

The package is a compile-time input only: the release takes the launcher's exe alone from the build's output
(`scripts/release/installer-exe.ps1`), so no file of the package ships.
