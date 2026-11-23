// Stands in for Reloaded-II's launcher and the game in scripts/dev/session.ps1's own tests.
//
//   Metaphor.StandInGame --launch <game exe>   acts as the launcher: finds the application whose AppLocation is that exe
//                                              (as Reloaded-II's Startup.cs does), starts it, and exits.
//   <game exe>                                 acts as the game: writes each enabled mod's status file, then runs until
//                                              it is killed, as the real game doesn't quit unattended.
//   --inspector <pipe name>                    acts as the Debug mod's live inspector (#291) for session.ps1 inspect, until killed.
//
// The real loader reads ReloadedII.json from a fixed AppData path; the stand-in reads the path from
// METAPHOR_STANDIN_RELOADED_CONFIG. METAPHOR_STANDIN_BEHAVIOUR picks what the game does: ready (default), refuse,
// partial (ready with 0 of 1 hooks), otherbuild (ready on another build), silent (writes nothing), crash (exits at
// once) or exitlater (ready, then exits a second later). With METAPHOR_STANDIN_MESSAGE_LOG set to a file, each enabled
// mod also gets a copy as its message-log.txt, held open for writing as the Debug mod holds its own (for session.ps1 run),
// and with METAPHOR_STANDIN_TRANSCRIPT a copy as its transcript.txt (for run -SaveLog), and likewise
// METAPHOR_STANDIN_REPORT_LOG as its report-log.txt.
// Loader facts: docs/raw-sources/reloaded-ii-app-and-mod-config-2026-11-15.md.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Metaphor.Core;
using Metaphor.Core.Announcements;

if (args is ["--inspector", var pipeName])
{
    InspectorStandIn.Serve(pipeName);
    return 0;
}

var configPath = Environment.GetEnvironmentVariable("METAPHOR_STANDIN_RELOADED_CONFIG")
    ?? throw new InvalidOperationException("METAPHOR_STANDIN_RELOADED_CONFIG is not set");
using var loaderConfig = JsonDocument.Parse(File.ReadAllText(configPath));
var root = loaderConfig.RootElement;

if (args is ["--launch", var gameExe])
{
    var target = Path.GetFullPath(gameExe);
    if (FindApplication(root, target) is null)
    {
        Console.Error.WriteLine($"no application is registered for {target}");
        return 1;
    }
    Process.Start(new ProcessStartInfo(target) { UseShellExecute = false })!.Dispose();
    return 0;
}

var behaviour = Environment.GetEnvironmentVariable("METAPHOR_STANDIN_BEHAVIOUR") ?? "ready";
if (behaviour == "crash")
{
    return 3;
}

// Reloaded-II loads its own reloaded.sharedlib.hooks mod before any other, and fails to start without it, so no mod
// reports (docs/raw-sources/reloaded-ii-log-on-normal-run-2026-11-14.md; seen again in the first signature spike run).
var modsFolder = Folder(root, "ModConfigDirectory");
var hasSharedHooks = Directory.EnumerateFiles(modsFolder, "ModConfig.json", SearchOption.AllDirectories)
    .Any(file => JsonDocument.Parse(File.ReadAllText(file)).RootElement.GetProperty("ModId").GetString() == "reloaded.sharedlib.hooks");
if (!hasSharedHooks)
{
    Console.Error.WriteLine("Failed to Load Reloaded Hooks Shared Lib Mod");
    Thread.Sleep(Timeout.Infinite);
}

var self = Environment.ProcessPath!;
var application = FindApplication(root, self) ?? throw new InvalidOperationException($"no application for {self}");
var enabled = application.GetProperty("EnabledMods").EnumerateArray().Select(id => id.GetString()).ToHashSet();
var build = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(self)));
var openLogs = new List<FileStream>();

foreach (var modConfig in Directory.EnumerateFiles(Folder(root, "ModConfigDirectory"), "ModConfig.json", SearchOption.AllDirectories))
{
    using var mod = JsonDocument.Parse(File.ReadAllText(modConfig));
    var modId = mod.RootElement.GetProperty("ModId").GetString()!;
    var folder = Path.GetDirectoryName(modConfig)!;
    if (!enabled.Contains(modId) || behaviour == "silent")
    {
        continue;
    }
    ModStatusFile.Write(folder, behaviour switch
    {
        "refuse" => ModStatus.Refused(modId, build, hooksExpected: 1, "unknown game build"),
        "partial" => ModStatus.Ready(modId, build, hooksLoaded: 0, hooksExpected: 1),
        "otherbuild" => ModStatus.Ready(modId, new string('1', 64), hooksLoaded: 1, hooksExpected: 1),
        _ => ModStatus.Ready(modId, build, hooksLoaded: 1, hooksExpected: 1),
    });
    // As the real mod does after its status, when scripts/dev/session.ps1 spike asked; its own file stands in for memory.
    var bytes = File.ReadAllBytes(self);
    SignatureScan.ScanIfRequested(folder, build, () => new ScannedModule(bytes.Length, 0, [(0, bytes.Length)], (rva, buffer) =>
    {
        bytes.AsSpan((int)rva, buffer.Length).CopyTo(buffer);
        return true;
    }));
    if (Environment.GetEnvironmentVariable("METAPHOR_STANDIN_MESSAGE_LOG") is { Length: > 0 } messageLog)
    {
        // Left open until the process ends, as ModEntryPoint.OpenMessageLog leaves the real one.
        var log = new FileStream(Path.Combine(folder, MessageLog.FileName), FileMode.Create, FileAccess.Write, FileShare.Read);
        log.Write(File.ReadAllBytes(messageLog));
        log.Flush();
        openLogs.Add(log);
    }
    if (Environment.GetEnvironmentVariable("METAPHOR_STANDIN_TRANSCRIPT") is { Length: > 0 } transcript)
    {
        File.Copy(transcript, Path.Combine(folder, TranscriptAnnouncer.FileName), overwrite: true);
    }
    if (Environment.GetEnvironmentVariable("METAPHOR_STANDIN_REPORT_LOG") is { Length: > 0 } reportLog)
    {
        File.Copy(reportLog, Path.Combine(folder, ReportLog.FileName), overwrite: true);
    }
}

// With METAPHOR_STANDIN_LOADER_LOG set to a file, its lines stand in for Reloaded-II's log of this run, named and placed
// as the real one (docs/raw-sources/reloaded-ii-launch-and-logs-2026-11-13.md) and held open as the loader holds it.
if (Environment.GetEnvironmentVariable("METAPHOR_STANDIN_LOADER_LOG") is { Length: > 0 } loaderLog)
{
    var logs = Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(configPath)!, "Logs")).FullName;
    var stem = $"{DateTime.UtcNow:yyyy-MM-dd HH.mm.ss} ~ {Path.GetFileNameWithoutExtension(self)}";
    var path = Path.Combine(logs, $"{stem}.txt");
    for (var n = 1; File.Exists(path); n++)
    {
        path = Path.Combine(logs, $"{stem} [{n}].txt");
    }
    var log = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
    log.Write(File.ReadAllBytes(loaderLog));
    log.Flush();
    openLogs.Add(log);
}

if (behaviour == "exitlater")
{
    Thread.Sleep(1000);
    return 4;
}
Thread.Sleep(Timeout.Infinite);
GC.KeepAlive(openLogs);
return 0;

static string Folder(JsonElement root, string property) => root.GetProperty(property).GetString()!;

static JsonElement? FindApplication(JsonElement root, string exe)
{
    foreach (var file in Directory.EnumerateFiles(Folder(root, "ApplicationConfigDirectory"), "AppConfig.json", SearchOption.AllDirectories))
    {
        var application = JsonDocument.Parse(File.ReadAllText(file)).RootElement;
        // As Reloaded-II's ApplicationConfig.GetAbsoluteAppLocation (see the raw source above).
        var location = application.GetProperty("AppLocation").GetString()!;
        var folder = Path.GetDirectoryName(file)!;
        var absolute = Path.GetFullPath(!Path.IsPathRooted(location) || Path.GetPathRoot(location) == "\\"
            ? location.StartsWith('\\')
                ? Path.Combine(Path.GetPathRoot(folder)!, location.TrimStart('\\'))
                : Path.Combine(folder, location)
            : location);
        if (absolute == exe)
        {
            return application;
        }
    }
    return null;
}
