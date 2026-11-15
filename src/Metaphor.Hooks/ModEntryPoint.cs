using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Metaphor.Core;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;

namespace Metaphor.Hooks;

/// <summary>The Reloaded-II entry point: the loader starts the first <see cref="IModV1"/> type in the mod's DLL
/// (docs/raw-sources/reloaded-ii-mod-entry-point-2026-11-15.md). A thin adapter: the work is in
/// <see cref="ModStartup"/>, and nothing here may throw into the game.</summary>
public sealed class ModEntryPoint : IMod
{
    /// <summary>How many hooks the mod has: its <see cref="GameHookAttribute"/> classes. Counts the types that did load
    /// if some didn't, so a broken type can't stop the mod reporting.</summary>
    private static int HooksExpected()
    {
        Type?[] types;
        try
        {
            types = typeof(ModEntryPoint).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types;
        }
        return types.Count(type => type?.IsDefined(typeof(GameHookAttribute), inherit: false) == true);
    }

    public Action Disposing => () => { };

    [SuppressMessage("Design", "CA1031", Justification = "An exception here would reach the game through the loader.")]
    public void StartEx(IModLoaderV1 loader, IModConfigV1 config)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(loader);
            ArgumentNullException.ThrowIfNull(config);
            var modLoader = (IModLoader)loader;
            var folder = modLoader.GetDirectoryForModId(config.ModId);
            // The mod runs inside the game, so this process's exe is the game executable whose hash identifies the
            // build. That it is the same file the known-builds hashes were taken from is [NEEDS VERIFICATION] in-game.
            var status = ModStartup.CheckAndReport(
                config.ModId, Environment.ProcessPath ?? "", folder, () => KnownGameBuilds.All, HooksExpected());
            ((ILogger)modLoader.GetLogger()).WriteLine(
                $"[{config.ModId}] build {ModBuild.Revision.CommitSha}{(ModBuild.Revision.HasUncommittedChanges ? "-dirty" : "")}: {status.State} on game build {status.GameBuildSha256}" +
                (status.Reason is null ? "" : $" ({status.Reason})"));
        }
        catch (Exception)
        {
            // ModStartup doesn't throw; this covers the loader API, whose failures we can't report anywhere.
        }
    }

    // No hooks are installed yet, so there is nothing to suspend or unload; false hides those launcher buttons.
    public void Suspend() { }

    public void Resume() { }

    public void Unload() { }

    public bool CanUnload() => false;

    public bool CanSuspend() => false;
}
