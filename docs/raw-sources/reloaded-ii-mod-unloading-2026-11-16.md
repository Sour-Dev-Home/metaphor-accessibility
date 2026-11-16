# Reloaded-II: unloading and reloading a mod while the game runs

- **Source:** source read on GitHub through the REST API (`gh api .../contents/<path>?ref=<commit>`); nothing was
  downloaded, built or run:
  - Reloaded-II tag `1.31.0` = commit `458987f26bc8d78fdc0f86b73fc0d6fd4b673d01`. `R` below means
    <https://github.com/Reloaded-Project/Reloaded-II/blob/458987f26bc8d78fdc0f86b73fc0d6fd4b673d01>.
  - Reloaded.Hooks tag `3.4.1` = commit `5b6f2904e0d2f59d6a6274a341a41a44b5bd6618` (the version our spikes used,
    `shared-delegate-hook-spike-2026-11-16.md`). `H` means
    <https://github.com/Reloaded-Project/Reloaded.Hooks/blob/5b6f2904e0d2f59d6a6274a341a41a44b5bd6618>.
  - McMaster.NETCore.Plugins `2.0.0` (the loader's `PackageReference`, `R/source/Reloaded.Mod.Loader/Reloaded.Mod.Loader.csproj#L104`),
    tag `v2.0.0` = commit `a6094157bfd1ace9d6b13df108c488526cfe850f`. `M` means
    <https://github.com/natemcmaster/DotNetCorePlugins/blob/a6094157bfd1ace9d6b13df108c488526cfe850f>.
  - Metaphor Essentials (`DeathChaos25/mrfpc.modloader`) at commit `6a9bdf408c6bf382478f7a0778dd5c9c3d4c799e` (HEAD on
    the date below).
  - Microsoft Learn, read on the date below: "How to use and debug assembly unloadability in .NET"
    (<https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability>, docs commit `156931bb`) and
    `Marshal.GetFunctionPointerForDelegate` remarks
    (<https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal.getfunctionpointerfordelegate>).
- **Saved:** 2026-11-16.
- **Game build:** none; this is about the loader and the hooking library. Nothing here was tried in the game.

Checked for #30. Builds on `reloaded-ii-mod-entry-point-2026-11-15.md` (the `IModV1` members) and
`reloaded-ii-shared-hooks-library-2026-11-15.md`.

## Short answer

The loader can unload and reload a mod at run time, but only through the "Reloaded II Server" mod and the launcher's
process page, and only for a mod that answers `CanUnload` true. Unloading our mod's *code* is not safe with Reloaded.Hooks
3.4.1 as we use it: a hook can be disabled but never removed, and the disabled hook still jumps into memory the .NET
runtime owns for our delegate. If the delegate is kept alive, the mod's load context can't be collected (and the DLL file
stays locked). If it isn't, the game may jump into freed memory. Whether the second case crashes is
`[NEEDS VERIFICATION]` (game test below).

## 1. What the loader does on unload, and how to trigger it

- `PluginManager.UnloadMod(modId)` (`R/source/Reloaded.Mod.Loader/Mods/PluginManager.cs#L113-L127`):
  1. throws `ModUnloadNotSupported` if the mod's `CanUnload` is false (`#L119-L120`);
  2. raises the loader's `ModUnloading` event (`#L122`). The loader itself subscribes two handlers that drop the
     mod's published controllers and plugins and call `GC.Collect()` (`R/source/Reloaded.Mod.Loader/Mods/LoaderAPI.cs#L14-L52`);
  3. forgets the mod's folder, exports and instance (`#L123-L125`);
  4. calls `ModInstance.Dispose()` (`#L126`).
- `ModInstance.Dispose()` (`R/source/Reloaded.Mod.Loader/Mods/Structs/ModInstance.cs#L52-L69`) does nothing unless
  `CanUnload` is true. Then, in order: invokes the `Disposing` action, calls `Unload()`, disposes the `LoadContext`, drops
  its references, and runs a forced, blocking `GC.Collect`. `CanUnload` is read once, when the instance is built
  (`#L37-L45`). The finalizer also calls `Dispose` (`#L47-L50`), and so does `PluginManager.Dispose` for every mod
  (`PluginManager.cs#L40-L48`).
- `Suspend`/`Resume` are separate: they call the mod's `Suspend()`/`Resume()` only if `CanSuspend` is true
  (`ModInstance.cs#L88-L104`, `PluginManager.cs#L132-L157`).
- Loading again: `Loader.LoadMod(modId)` throws if the mod is still loaded, re-reads the mod configs from disk
  (`FindMod`) and loads it with any missing dependencies (`R/source/Reloaded.Mod.Loader/Loader.cs#L58-L71`,
  `#L148-L172`); already-loaded dependencies are skipped (`#L166-L168`).
- Who calls these: the `Reloaded II Server` mod (`reloaded.utils.server`, "Hosts a remote control server for Reloaded
  II; allowing for functionality such as remote mod loading/unloading",
  `R/source/Mods/Reloaded.Utils.Server/ModConfig.json`) maps `SetModState` requests to `LoadMod`, `UnloadMod`,
  `SuspendMod` and `ResumeMod` (`R/source/Mods/Reloaded.Utils.Server/Server.cs#L30-L47`). The client library is the
  `Reloaded.Mod.Loader.Server` NuGet package (`R/docs/InternalLibrariesServer.md`).
- Launcher UI: the running-process page lists loaded mods with their `CanUnload`/`CanSuspend` and has buttons Load Mod,
  Resume, Suspend and Unload; Unload is enabled only when the selected mod's `CanUnload` is true
  (`R/source/Reloaded.Mod.Launcher/Pages/BaseSubpages/ApplicationSubPages/ReloadedProcessPage.xaml#L48-L71`). The buttons
  send the requests above over LiteNetLib (`R/source/Reloaded.Mod.Launcher.Lib/Models/ViewModel/Application/ReloadedAppViewModel.cs#L48`,
  `#L120-L134`). The page says "Requires 'Reloaded II Server' Mod to be Enabled w/ LiteNetLib Host. Download it from
  'Download Mods' menu." and "This menu is primarily intended for use by developers."
  (`R/source/Reloaded.Mod.Launcher/Assets/Languages/en-GB.xaml#L148`, `#L528`). There is no single "reload" action:
  it is Unload, then Load Mod.

## 2. Collectible load contexts and what keeps them alive

- Each managed mod gets its own `AssemblyLoadContext`, built with McMaster's `AssemblyLoadContextBuilder`; it is
  collectible only when `isUnloadable` is true (`R/source/Reloaded.Mod.Loader/Utilities/LoadContext.cs#L53-L70`;
  `EnableUnloading` sets `_isCollectible`, `M/src/Plugins/Loader/AssemblyLoadContextBuilder.cs#L304-L306`).
  `LoadContext.Dispose` calls `AssemblyLoadContext.Unload()` only if the context is collectible (`LoadContext.cs#L22-L26`).
- `isUnloadable` is `config.HasExports.GetValueOrDefault(true) | config.CanUnload.GetValueOrDefault(false)`, read from
  the mod's `ModConfig.json`, not from the running mod (`PluginManager.cs#L268-L272`). When either field is missing
  the loader writes them: `HasExports` from whether the mod has an `IExports` type, `CanUnload` from `plugin.CanUnload()`,
  logging "Updating Config w/ HasExports & CanUnload." and saving the file (`#L212`, `#L244`, `#L251`, `#L258-L259`,
  `#L389-L394`). Consequences:
  - The first load after `ModConfig.json` lacks those fields is collectible (`HasExports` defaults to true); later
    loads of a mod that answered `CanUnload` false and has no exports are not.
  - Changing `CanUnload` in code does not change the cached value. A test must remove `CanUnload`/`HasExports` from the
    deployed `ModConfig.json` (or deploy a fresh one) so the loader re-reads them. Our repository's `ModConfig.json`
    has neither field (`src/Metaphor.Hooks/ModConfig.json`); whether our deploy step overwrites the cached copy each
    build is `[NEEDS VERIFICATION]`.
- The mod DLL is loaded from its path (`LoadContext.LoadDefaultAssembly` → `LoadFromAssemblyPath`,
  `LoadContext.cs#L31`); the loader never calls McMaster's `PreloadAssembliesIntoMemory`, whose comment says it is
  "required to prevent dotnet from locking loaded files" (`M/src/Plugins/Loader/AssemblyLoadContextBuilder.cs#L316-L318`,
  `M/src/Plugins/Loader/ManagedLoadContext.cs#L179-L193`). So the DLL file is locked while its context lives. That the
  lock goes away once a collectible context is collected is `[NEEDS VERIFICATION]`.
- ReadyToRun: "Using ReadyToRun is incompatible with unloadable mods due to a runtime limitation"
  (`R/docs/ModTemplate.md#L43-L46`); Microsoft says R2R code "will be ignored" in a collectible context. Our
  `ModConfig.json` names no R2R DLL.
- What keeps a context alive (Microsoft, unloadability page): unloading is "cooperative"; `Unload` "just initiates
  the unloading", which finishes only when no thread has a method from the context on its stack, and nothing outside
  the context (statics, locals, strong or pinned GC handles) references its assemblies, types or instances. Also
  listed: threads running its code, and pending `RegisteredWaitHandle` callbacks into it.
- Native libraries the mod loads (our `prism.dll`, `src/Metaphor.Hooks/PrismSpeech.cs`) are not covered by any of the
  above; whether they stay loaded and locked after unload is `[NEEDS VERIFICATION]`.

## 3. What a mod must implement, and removing hooks

- `IModV1` members (`R/source/Reloaded.Mod.Interfaces/Internal/IModV1.cs#L6-L60`): `Disposing` (an `Action` property,
  invoked by the loader), `Suspend()` ("undo any modifications ... such as direct ASM modifications or function hooks"),
  `Resume()`, `Unload()` ("same as suspend, and releases resources (e.g. File Handles, Native Memory) for the mod to be
  ejected"; "clear all traces of your mod's existence"), `CanUnload()` ("If you return false ... the mod loader will
  never call the Unload function") and `CanSuspend()`.
- The template's advice: `Suspend` = undo memory changes and "Deactivate hooks. (Reloaded.Hooks Supports This!)";
  `Unload` = call `Suspend` and release unmanaged resources
  (`R/source/Reloaded.Mod.Template/templates/configurable/Template/ModBase.cs#L25-L45`). Published controllers are
  removed automatically on unload, or by hand with `RemoveController` (`R/docs/DependencyInjection_Publisher.md#L127-L136`).
- Our mod today: `Disposing` is a no-op, `Unload`/`Suspend` are empty, `CanUnload`/`CanSuspend` return false
  (`src/Metaphor.Hooks/ModEntryPoint.cs#L50`, `#L112-L120`), and hooks are kept alive for the process
  (`src/Metaphor.Hooks/HookLoader.cs`, rule 12 in `docs/coding-standards.md`).
- Reloaded.Hooks 3.4.1 has no way to remove a hook. `IHook` offers `Activate`, `Enable` and `Disable` only
  (`H/source/Reloaded.Hooks.Definitions/IHook.cs#L31-L90`; `Disable` "Temporarily disables the hook ... re-routed back
  to the original function", "implemented in such a fashion that the hook shall never touch C# code"). `Hook<T>` is
  not `IDisposable` and never restores the function's original bytes (`H/source/Reloaded.Hooks/Hook.cs`).
- How `Disable` works: activation writes a jump at the game function's start to `ReverseWrapper.WrapperPointer`
  (`Hook.cs#L100-L102`, `#L128`). `Disable` overwrites the start of `WrapperPointer` with a jump to the relocated
  original code, and `Enable` writes the saved bytes back (`#L135-L138`, `#L157-L172`).
- What `WrapperPointer` is: for a delegate, `Marshal.GetFunctionPointerForDelegate(function)`; it is replaced by a
  Reloaded-generated stub only when the delegate's calling convention is not Microsoft x64
  (`H/source/Reloaded.Hooks/X64/ReverseWrapper.cs#L33-L45`, `#L61-L68`). A delegate with no `[Function]` attribute is
  treated as Microsoft (`H/source/Reloaded.Hooks.Definitions/X64/FunctionAttribute.cs#L155-L164`); ours have none
  (`src/Metaphor.Hooks/PassThrough.cs`). So for our hooks the game jumps straight into the runtime's thunk for our
  delegate, and `Disable` patches that thunk.
- The delegate is held only by `ReverseWrapper.CSharpFunction` (`ReverseWrapper.cs#L35`). Microsoft: "You must
  manually keep the delegate from being collected ... The garbage collector does not track references to unmanaged
  code"; "If native code stores the function pointer beyond the duration of the call, root the delegate for its entire
  lifetime." The game's patched function stores it forever.
- Hence the conflict for a full unload: keeping the delegate (or the hook) alive roots our context, so it can't be
  collected; letting it go frees or reuses the thunk the game still jumps to. Whether the runtime frees that thunk memory
  when the delegate or the context is collected is `[NEEDS VERIFICATION]`. A thread inside a hook callback at unload
  time also keeps the context alive until it returns (Microsoft, above).
- Not checked: whether hooks built from `[UnmanagedCallersOnly]` function pointers (the value-type path,
  `ReverseWrapper.cs#L37-L38`) are allowed in a collectible context `[NEEDS VERIFICATION]`.

## 4. Other mods

- Metaphor Essentials' "Support for hot reload" is "(update files on the fly without game reboot)"
  (`mrfpc.modloader` `README.md#L21`), i.e. reloading data files, not unloading code. Its `Template/ModBase.cs` answers
  `CanUnload() => false` (`#L18`), and its `Mod.cs` overrides neither `CanUnload` nor `Unload` (no match for either at
  the commit above), so it can't be unloaded.
- No mod found that unloads code with live hooks. Reloaded's own Server mod supports unload: `CanUnload() => true`,
  and `Unload()` only disposes its network server (`R/source/Mods/Reloaded.Utils.Server/Program.cs#L65`, `#L70`). Its docs expect it to be "unloaded and reloaded" (`R/docs/InternalLibrariesServer.md#L21`). A
  wider search (other Reloaded-II mods on GitHub) was not done.

## Options this leaves for #30

- **Suspend/Resume, not unload:** `Disable`/`Enable` every hook keeps the code loaded and safe, but does not load new
  code, so it doesn't save a restart after a rebuild.
- **Unload with leaked hooks:** disable every hook in `Unload` and deliberately keep the hook objects reachable from
  outside the context, so the context leaks. Reload would then need a DLL at a path that is not locked
  `[NEEDS VERIFICATION]`, and two copies of the code would sit in memory.
- **Split host:** a small, never-unloaded hook layer that owns the hooks and forwards calls into a feature assembly it
  loads in its own collectible context. This is our design, not something found in a source `[NEEDS VERIFICATION]`.

## Game test to run

Goal: confirm the unload path, the file lock, and what a disabled-then-dropped hook does, and time it against a
restart. Use a throwaway mod outside the repository (own `ModId`, e.g. `test.unload`, depends on
`reloaded.sharedlib.hooks`); keep our mod disabled. Record the game build, the loader version and the Server mod
version.

1. In the launcher, download and enable "Reloaded II Server" for Metaphor, with the LiteNetLib host on.
2. Throwaway mod v1: `CanUnload` true, `CanSuspend` false; `StartEx` logs "v1 started"; `Unload` logs "v1 unloading".
   No hooks. Deploy with a fresh `ModConfig.json` (no `CanUnload`/`HasExports`).
3. Start the game; after the run, check the deployed `ModConfig.json` now has `"CanUnload": true`.
4. On the launcher's process page, select the mod and press Unload. Check the log for "v1 unloading", and try to
   delete or overwrite the mod DLL: success means the file lock went away (context collected).
5. Build v2 (logs "v2 started"), copy it over, press Load Mod and pick it. Check "v2 started" appears.
6. Repeat 2-5 with one hook added: a pass-through hook on a function the game calls often (one of our signature-checked
   functions), activated in `StartEx`, `Disable()`d and dropped in `Unload`. After Unload, play for a few minutes where
   that function runs. Note: crash or not; whether step 4's delete still works.
7. If 6 crashes, repeat it keeping the hook objects in a static of a type outside the mod (or simply never unloading
   the hook) to see whether the leak variant survives.

Time, each with a stopwatch: (a) Unload click to the "unloading" log line; (b) Load Mod click to the "started" log line;
(c) for comparison, a full restart: quit, relaunch from the launcher, reach the title screen and load a save. Run each
twice.
