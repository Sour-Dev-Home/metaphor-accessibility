# Reloaded-II's shared hooks library: how a mod gets it, what ships, under which licence

- **Source:** GitHub REST API (`gh api`) on `Reloaded-Project/Reloaded-II` at commit
  `458987f26bc8d78fdc0f86b73fc0d6fd4b673d01` (the commit the other Reloaded-II raw sources cite), and on
  `RyoTune/MF.Toolkit` at `b1f876da5b4c1f42b5eef07fc6c2174030beb77b`; NuGet metadata and packages from
  `https://api.nuget.org/v3-flatcontainer/` (`reloaded.sharedlib.hooks` 1.9.0, `reloaded.hooks.definitions` 1.11.0),
  listed and read, nothing run.
- **Saved:** 2026-11-15.
- **Game build:** none; this is about the mod loader's library.

## How the official template uses it

`source/Reloaded.Mod.Template/templates/configurable/`:

- `ModConfig.json` line 17: `"ModDependencies": [ "reloaded.sharedlib.hooks" ],`
- `Reloaded.Mod.Template.csproj` lines 47–48:

  ```xml
  <PackageReference Include="Reloaded.Mod.Interfaces" Version="2.4.0" ExcludeAssets="runtime" />
  <PackageReference Include="Reloaded.SharedLib.Hooks" Version="1.9.0" />
  ```

  Unlike `Reloaded.Mod.Interfaces`, the hooks package is not compile-only, so its DLLs are part of the mod's output.
- `Template/Startup.cs` line 60: `_modLoader.GetController<IReloadedHooks>()?.TryGetTarget(out _hooks!);` with
  `using Reloaded.Hooks.ReloadedII.Interfaces;` (line 6).

The loader loads `reloaded.sharedlib.hooks` itself on every run ("Loading: Reloaded II Shared Lib: Reloaded.Hooks",
`reloaded-ii-log-on-normal-run-2026-11-14.md`).

## The packages

| Package | Version | DLLs | Licence | Depends on |
|---|---|---|---|---|
| `Reloaded.SharedLib.Hooks` | 1.9.0 (the only version listed) | `lib/netstandard2.0/Reloaded.Hooks.ReloadedII.Interfaces.dll` | `LICENSE`: GNU LGPL v3 | `Reloaded.Hooks.Definitions` 1.11.0 |
| `Reloaded.Hooks.Definitions` | 1.11.0 | `lib/{net5.0,net6.0,netcoreapp3.0,netstandard2.0}/Reloaded.Hooks.Definitions.dll` | `LICENSE.md`: GNU LGPL v3 | none |

Project URLs: <https://github.com/Sewer56/Reloaded.SharedLib.Hooks>, <https://github.com/Reloaded-Project/Reloaded.Hooks>.

## How a Metaphor mod declares and creates hooks (MF.Toolkit)

`MF.Toolkit.Reloaded/Messages/MessageService.cs` (`using Reloaded.Hooks.Definitions;`, line 10) declares plain
delegates with no calling-convention attribute, for example line 32:

```csharp
private delegate MSG* GetMessageByLabelIndex(int* msgId, int labelIdx, MsgFlag* flags, MsgConfigs* configs);
```

and creates hooks as `hooks.CreateHook<CompileMsgFile>(this.CompileMsgFileImpl, result).Activate()` (line 96), or
wrappers as `hooks.CreateWrapper<GetMessageByLabelIndex>(result, out _)` (line 101), from an address found by signature
scan. That plain delegates get the x64 Microsoft calling convention by default is inferred from this working usage, not
read from Reloaded.Hooks' source `[NEEDS VERIFICATION]`.
