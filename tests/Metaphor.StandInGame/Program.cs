// Stands in for Reloaded-II's launcher and the game in the smoke-test and deploy scripts' own tests.
//
//   Metaphor.StandInGame --launch <game exe>   acts as the launcher: finds the application whose AppLocation is that exe
//                                              (as Reloaded-II's Startup.cs does), starts it, and exits.
//   <game exe>                                 acts as the game: writes each enabled mod's status file, then runs until
//                                              it is killed, as the real game doesn't quit unattended.
//
// The real loader reads ReloadedII.json from a fixed AppData path; the stand-in reads the path from
// METAPHOR_STANDIN_RELOADED_CONFIG. METAPHOR_STANDIN_BEHAVIOUR picks what the game does: ready (default), refuse,
// partial (ready with 0 of 1 hooks), otherbuild (ready on another build), silent (writes nothing) or crash (exits at
// once). Loader facts: docs/raw-sources/reloaded-ii-app-and-mod-config-2026-11-15.md.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Metaphor.Core;

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
// reports (docs/raw-sources/reloaded-ii-log-on-normal-run-2026-11-14.md; seen again in the first signature-spike run).
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
    // As the real mod does after its status, when scripts/signature-spike.ps1 asked; its own file stands in for memory.
    var bytes = File.ReadAllBytes(self);
    SignatureScan.ScanIfRequested(folder, build, () => new ScannedModule(bytes.Length, 0, [(0, bytes.Length)], (rva, buffer) =>
    {
        bytes.AsSpan((int)rva, buffer.Length).CopyTo(buffer);
        return true;
    }));
}

Thread.Sleep(Timeout.Infinite);
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
