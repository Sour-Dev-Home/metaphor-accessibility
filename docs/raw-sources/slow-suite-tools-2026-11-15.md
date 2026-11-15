# Slow suite tools: Stryker.NET and jscpd

- **Source:** each tool's docs on GitHub at a release tag, read via the GitHub REST API; NuGet and npm registry metadata;
  the `dependabot/dependabot-core` source at commit `1a0125f11e75859a0957b4d73ea6b8656570b30e`; the
  `stryker-mutator/mutation-testing-elements` report schema at commit `1238ed0ed935c2cf17ba5ca1979b2002e4d4b9e1`. Package
  layouts were checked by listing the downloaded `.nupkg`. Facts marked "local run" come from the run under
  [Local run](#local-run). Checked for #19.
- **Saved:** 2026-11-15.
- **Game build:** none; this is about CI tooling.

## Stryker.NET (`dotnet-stryker`)

`S` means <https://github.com/stryker-mutator/stryker-net/blob/dotnet-stryker@4.16.0/docs>.

- Versions on NuGet: 4.16.0 (published 2026-07-03), 5.0.0 (2026-09-11), 5.1.0 (2026-11-10).
- 5.0.0 ships only `tools/net10.0/any/DotnetToolSettings.xml`, so it doesn't install with the .NET 9 SDK that
  `global.json` pins: `dotnet tool restore` fails with "Settings file 'DotnetToolSettings.xml' was not found in the
  package". 4.16.0 ships `tools/net8.0/any`, and runs on the .NET 9 runtime with `rollForward` set in the tool manifest
  (local run).
- A tool manifest pins a version but no hash, so `dotnet tool restore` trusts nuget.org's signed package for
  4.16.0. `rollForward` only lets it run on a newer .NET runtime; it doesn't change the tool version.
- Config file: `stryker-config.json` in the test project's folder, keys under `stryker-config`
  (`S/configuration.md`, "Use a config file"). `project` names the project under test when the test project references
  more than one (`### project`). `test-case-filter` takes `dotnet test --filter` syntax (`### test-case-filter`).
- Thresholds default to high 80, low 60, break 0; only a score below a non-zero `break` exits with code 1
  (`### thresholds`, `### break-at`).
- `--output` sets where logs and reports go (`### output`). The local run put them in `<output>/reports/` as
  `mutation-report.html`, `mutation-report.json` and `mutation-report.md`.
- Reporters (`S/reporters.md`): `html` (each mutant, visually), `markdown` (`mutation-report.md`: per-file score,
  killed, survived and the final score), `json`, `progress` and others. Defaults are `html` and `progress`.
- The `json` report follows `mutation-testing-report-schema.json`
  (<https://github.com/stryker-mutator/mutation-testing-elements/blob/1238ed0ed935c2cf17ba5ca1979b2002e4d4b9e1/packages/report-schema/src/mutation-testing-report-schema.json>):
  `files` maps each path to an object whose `mutants` each have `id`, `mutatorName`, `location` (`start.line`,
  `start.column`) and `status`, one of `Killed`, `Survived`, `NoCoverage`, `CompileError`, `RuntimeError`, `Timeout`,
  `Ignored`, `Pending`, plus an optional `replacement`. The local run's paths were absolute.

## jscpd

`J` means <https://github.com/kucherenko/jscpd/blob/v5.4.2>.

- Versions on npm: 5.4.2 (published 2026-10-20), 5.4.3 (2026-11-08).
- Since 5.x the engine is Rust. The npm package `jscpd@5.4.2` has no install scripts; its binary comes from
  per-platform `optionalDependencies` (`jscpd-windows-x64-msvc`, `jscpd-linux-x64-gnu` and others) at the same version
  (`npm view jscpd@5.4.2`).
- Exit codes (`J/docs/rust.md`, "Exit codes"): 0 when the scan ran and no gate fired, "clones may still have been found
  and reported"; 1 when `--threshold` is exceeded, a reporter can't write, or a scan path doesn't exist; 2 on a
  command-line error.
- Reporters include `console`, `markdown` (`jscpd-report.md`) and `html` (`jscpd-report.html`), written to `--output`
  (`J/docs/rust.md`, "Reporters").
- Config file: `.jscpd.json` or `--config <path>`, with `path`, `ignore`, `reporters`, `gitignore` and others
  (`J/docs/rust.md`, "Config File"). The local run showed `path` entries are resolved relative to the config file's
  folder.

## Local run

Windows 11, Git Bash, .NET SDK 9.0.319 (as `global.json` pins), Node.js 26.3.0, on 2026-11-15: `bash scripts/check.sh
slow` from the repository root. Stryker.NET 4.16.0 mutated `Metaphor.Core` with the `Unit` tests in about 40 seconds:
final score 75.00%, 15 surviving mutants. jscpd 5.4.2 analyzed 43 files and found 5 clones. Both exited 0.

## Dependabot and `dotnet-tools.json`

- `dependabot-core` has `DotNetToolsJsonDiscovery`, `DotNetToolsJsonBuildFile` and `DotNetToolsJsonUpdater` under
  `nuget/helpers/lib/NuGetUpdater/NuGetUpdater.Core/`, so its NuGet updater handles tool manifests. Whether it finds
  `.config/dotnet-tools.json` from a `directory: "/"` entry is [NEEDS VERIFICATION].
