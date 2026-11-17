using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Metaphor.Core;
using Metaphor.Core.Announcements;
using Metaphor.Core.Settings;
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
    private static IAnnouncer StartSpeech(string folder, Action<string> log)
    {
        var (speech, whyNot) = PrismSpeech.TryStart(folder);
        log(speech is null ? $"speech unavailable: {whyNot}" : $"speech: {speech.BackendName}");
        IAnnouncer announcer = speech is null ? new SilentAnnouncer() : speech;
#if DEBUG
        announcer = new TranscriptAnnouncer(announcer, folder);
#endif
        return announcer;
    }

    /// <summary>The report log a player attaches to a bug report (#188), fresh each run, in every build. It redacts the
    /// mod's folder, the settings folder, the user profile and the user name, since a path to any of them can turn up in
    /// an error. A user name under three characters isn't redacted on its own, as replacing it everywhere would garble
    /// the log. Null when it can't be opened.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A report log must never stop the mod.")]
    [SuppressMessage("Reliability", "CA2000", Justification = "Lives as long as the game process.")]
    private static ReportLog? OpenReportLog(string folder, string configFolder)
    {
        try
        {
            var stream = new FileStream(
                Path.Combine(folder, ReportLog.FileName), FileMode.Create, FileAccess.Write, FileShare.Read);
            var started = Process.GetCurrentProcess().StartTime;
            var userName = Environment.UserName;
            return new ReportLog(
                new StreamWriter(stream), () => (long)(DateTime.Now - started).TotalMilliseconds,
                [folder, configFolder, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    userName.Length >= 3 ? userName : ""]);
        }
        catch (Exception)
        {
            return null;
        }
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

    // Kept so the watcher lives as long as the game process.
    private SettingsFile? settingsFile;

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
            // Every line for the loader's console also goes to the report log (#188).
            // The settings live in Reloaded-II's per-mod user-config folder (IModLoaderV3.GetModConfigDirectory, in
            // Reloaded.Mod.Interfaces 2.4.0), read now and again when the file changes (#104).
            var configFolder = modLoader.GetModConfigDirectory(config.ModId);
            var report = OpenReportLog(folder, configFolder);
            void Log(string line)
            {
                logger.WriteLine($"[{config.ModId}] {line}");
                report?.Note(line);
            }
            // Speech and the logs first: hooks are created with them, and one may fire at once.
            var announcer = StartSpeech(folder, Log);
#if DEBUG
            var messageLog = OpenMessageLog(folder);
#else
            MessageLog? messageLog = null;
#endif
            var settings = new SettingsReloader(announcer, Log);
            settingsFile = SettingsFile.Open(configFolder, settings, Log);
            var services = HookServices.Speaking(announcer, settings, report, messageLog, folder);
            // The mod runs inside the game, so this process's exe is the game executable whose hash identifies the
            // build (seen in docs/raw-sources/mod-entry-point-first-run-2026-11-15.md). Hooks go in only on a known
            // build, through the loader (#39). Issue #21's spike scans first, so it reads the game's own bytes (#92).
            var candidates = HookLoader.Candidates(folder);
            var status = ModStartup.CheckAndReport(
                config.ModId, Environment.ProcessPath ?? "", folder, () => KnownGameBuilds.All, HookLoader.Expected(candidates),
                build => HookLoader.Install(
                    build, candidates, modLoader, services, Log),
                build => ScanIfRequested(folder, build, config.ModId, logger),
                announcer);
            // The load message is spoken (#187); the log repeats it for when speech didn't start.
            Log(
                $"build {ModBuild.Revision.CommitSha}{(ModBuild.Revision.HasUncommittedChanges ? "-dirty" : "")}: {status.State} on game build {status.GameBuildSha256}, hooks {status.HooksLoaded}/{status.HooksExpected}" +
                (status.Reason is null ? "" : $" ({status.Reason})") + $"; load message: {LoadMessage.For(status).Text}");
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
