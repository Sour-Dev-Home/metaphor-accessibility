using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Metaphor.Core;
using Metaphor.Core.Announcements;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;

namespace Metaphor.Hooks;

/// <summary>The Reloaded-II entry point: the loader starts the first <see cref="IModV1"/> type in the mod's DLL
/// (docs/raw-sources/reloaded-ii-mod-entry-point-2026-11-15.md). A thin adapter: the work is in
/// <see cref="ModStartup"/>, and nothing here may throw into the game.</summary>
public sealed class ModEntryPoint : IMod
{
    /// <summary>Starts the announcer every feature speaks through (ADR 0004): Prism when it starts, silence otherwise,
    /// and in Debug builds the transcript in front of either.</summary>
    private static IAnnouncer StartSpeech(string folder, string modId, ILogger logger)
    {
        var (speech, whyNot) = PrismSpeech.TryStart(folder);
        logger.WriteLine(speech is null
            ? $"[{modId}] speech unavailable: {whyNot}"
            : $"[{modId}] speech: {speech.BackendName}");
        IAnnouncer announcer = speech is null ? new SilentAnnouncer() : speech;
#if DEBUG
        announcer = new TranscriptAnnouncer(announcer, folder);
#endif
        return announcer;
    }

#if DEBUG
    /// <summary>#55's message log, fresh each run, readable while the game writes it. Null when it can't be opened.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A spike log must never stop the mod.")]
    [SuppressMessage("Reliability", "CA2000", Justification = "Lives as long as the game process.")]
    private static MessageLog? OpenMessageLog(string folder)
    {
        try
        {
            var stream = new FileStream(
                Path.Combine(folder, MessageLog.FileName), FileMode.Create, FileAccess.Write, FileShare.Read);
            var started = System.Diagnostics.Process.GetCurrentProcess().StartTime;
            return new MessageLog(new StreamWriter(stream), () => (long)(DateTime.Now - started).TotalMilliseconds);
        }
        catch (Exception)
        {
            return null;
        }
    }
#endif

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
            var logger = (ILogger)modLoader.GetLogger();
            // Speech and the dev log first: hooks are created with them, and one may fire at once.
            var announcer = StartSpeech(folder, config.ModId, logger);
#if DEBUG
            var messageLog = OpenMessageLog(folder);
#else
            MessageLog? messageLog = null;
#endif
            var services = HookServices.Speaking(announcer, messageLog, folder);
            // The mod runs inside the game, so this process's exe is the game executable whose hash identifies the
            // build (seen in docs/raw-sources/mod-entry-point-first-run-2026-11-15.md). Hooks go in only on a known
            // build, through the loader (#39). Issue #21's spike scans first, so it reads the game's own bytes (#92).
            var candidates = HookLoader.Candidates(folder);
            var status = ModStartup.CheckAndReport(
                config.ModId, Environment.ProcessPath ?? "", folder, () => KnownGameBuilds.All, HookLoader.Expected(candidates),
                build => HookLoader.Install(
                    build, candidates, modLoader, services, line => logger.WriteLine($"[{config.ModId}] {line}")),
                build => ScanIfRequested(folder, build, config.ModId, logger));
            logger.WriteLine(
                $"[{config.ModId}] build {ModBuild.Revision.CommitSha}{(ModBuild.Revision.HasUncommittedChanges ? "-dirty" : "")}: {status.State} on game build {status.GameBuildSha256}, hooks {status.HooksLoaded}/{status.HooksExpected}" +
                (status.Reason is null ? "" : $" ({status.Reason})"));
#if DEBUG
            // The by-ear check (#54): with a screen reader running, this is the first thing the mod says.
            announcer.Announce(new Announcement($"Metaphor accessibility mod {status.State.ToString().ToLowerInvariant()}.", Interrupt: true));
#endif
        }
        catch (Exception)
        {
            // ModStartup doesn't throw; this covers the loader API, whose failures we can't report anywhere.
        }
    }

    /// <summary>Issue #21's spike: runs once, only when scripts/signature-spike.ps1 put a probes file in the mod's
    /// folder, and before any hook installs.</summary>
    private static void ScanIfRequested(string folder, string build, string modId, ILogger logger)
    {
        var scan = SignatureScan.ScanIfRequested(folder, build, OpenMainModuleForScan);
        if (scan is not null)
        {
            logger.WriteLine(scan.Error is null
                ? $"[{modId}] signature scan: {scan.Probes.Count(probe => probe.Matches == 1)} of {scan.Probes.Count} probes matched once; {scan.ReadableBytes} of {scan.ModuleBytes} bytes read, {scan.UnreadableBytes} failed; {scan.MillisecondsSinceGameStart} ms after start ({SignatureScan.ReportFileName})"
                : $"[{modId}] signature scan failed: {scan.Error}");
        }
    }

    /// <summary>The game's main module for the signature scan, read a chunk at a time through
    /// <see cref="ModuleMemoryReader"/> as the hook loader reads it.</summary>
    private static ScannedModule OpenMainModuleForScan()
    {
        using var process = Process.GetCurrentProcess();
        var sinceStart = (long)(DateTime.Now - process.StartTime).TotalMilliseconds;
        var moduleBytes = process.MainModule?.ModuleMemorySize ??
            throw new InvalidOperationException("the game process has no main module");
        var (moduleBase, regions) = ModuleMemoryReader.MainModuleRegions();
        return new(moduleBytes, sinceStart, regions, (rva, buffer) => ModuleMemoryReader.ReadAt(moduleBase, rva, buffer));
    }

    // No hooks are installed yet, so there is nothing to suspend or unload; false hides those launcher buttons.
    public void Suspend() { }

    public void Resume() { }

    public void Unload() { }

    public bool CanUnload() => false;

    public bool CanSuspend() => false;
}
