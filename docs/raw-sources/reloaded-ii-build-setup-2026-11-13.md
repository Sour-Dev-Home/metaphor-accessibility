# Reloaded-II mod build and test setup (template and Metaphor mods)

- **Source:** GitHub REST API (`gh api`), reading files on each repo's default branch. Nothing was downloaded or run.
- **Saved:** 2026-11-13.
- **Game build:** none; this is about build tooling, not the game.

## Official template

- No separate template repo. The C# template lives in Reloaded-II under `source/Reloaded.Mod.Template/templates/configurable`
  (<https://github.com/Reloaded-Project/Reloaded-II/tree/master/source/Reloaded.Mod.Template/templates/configurable>).
  The org's other template repo is `reloaded-templates-rust`.
- Its project file targets `net9.0-windows`, with `Microsoft.NET.ILLink.Tasks` 9.0.3, `Reloaded.Mod.Interfaces` 2.4.0 and
  `Reloaded.SharedLib.Hooks` 1.9.0
  (<https://github.com/Reloaded-Project/Reloaded-II/blob/master/source/Reloaded.Mod.Template/templates/configurable/Reloaded.Mod.Template.csproj>).
- No `global.json` and no test project.
- Its workflow runs on `windows-latest` with pwsh and installs .NET 5.0.x and 9.0.x
  (<https://github.com/Reloaded-Project/Reloaded-II/blob/master/source/Reloaded.Mod.Template/templates/configurable/.github/workflows/reloaded.yml>).
- `Publish.ps1` runs `dotnet publish -r win-x86` and `-r win-x64` with `PublishReadyToRun=true`, lines 270-271
  (<https://github.com/Reloaded-Project/Reloaded-II/blob/master/source/Reloaded.Mod.Template/templates/configurable/Publish.ps1>).

## Metaphor mods

| Repo | Mod project | Library or interfaces project | Tests | Runs tests in CI |
|---|---|---|---|---|
| DeathChaos25/mrfpc.modloader | `net7.0-windows` | `net7.0` | none | no workflow |
| RyoTune/MF.Toolkit | `net8.0-windows` | `net8.0` | xunit 2.5.3, `net8.0-windows`, parses sample `.msg` files | no (`windows-latest`, publish only) |
| Sewer56/CriFs.V2.Hook.ReloadedII | `net7.0-windows` | `net7.0` | xunit 2.4.2, `net7.0-windows`, small CPK-style folder fixtures | no (`windows-latest`, publish only) |

Sources:
- <https://github.com/DeathChaos25/mrfpc.modloader/blob/master/mrfpc.modloader.csproj>
- <https://github.com/DeathChaos25/mrfpc.modloader/blob/master/Persona.Merger.Common/Persona.Merger.Common.csproj>
- <https://github.com/RyoTune/MF.Toolkit/blob/main/MF.Toolkit.Reloaded/MF.Toolkit.Reloaded.csproj>
- <https://github.com/RyoTune/MF.Toolkit/blob/main/MF.Toolkit.Interfaces/MF.Toolkit.Interfaces.csproj>
- <https://github.com/RyoTune/MF.Toolkit/blob/main/MF.Toolkit.Tests/MF.Toolkit.Tests.csproj>
- <https://github.com/RyoTune/MF.Toolkit/blob/main/.github/workflows/reloaded.yml>
- <https://github.com/Sewer56/CriFs.V2.Hook.ReloadedII/blob/main/CriFs.V2.Hook/CriFs.V2.Hook.csproj>
- <https://github.com/Sewer56/CriFs.V2.Hook.ReloadedII/blob/main/Interfaces/CriFs.V2.Hook.Interfaces/CriFs.V2.Hook.Interfaces.csproj>
- <https://github.com/Sewer56/CriFs.V2.Hook.ReloadedII/blob/main/CriFs.V2.Hook.Tests/CriFs.V2.Hook.Tests.csproj>
- <https://github.com/Sewer56/CriFs.V2.Hook.ReloadedII/blob/main/.github/workflows/reloaded.yml>

None of the three repos has a `global.json` or `Directory.Build.*` file.

## Open

- Whether a `net*-windows` project builds on Linux (the SDK's `EnableWindowsTargeting` property, and cross-OS ReadyToRun):
  [NEEDS VERIFICATION].
- The plain `netX.0` library projects carry no Windows target, so they may build on Linux: [NEEDS VERIFICATION].
