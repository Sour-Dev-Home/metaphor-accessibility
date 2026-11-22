using System.Text;

namespace Metaphor.Installer.Tests;

/// <summary>The launcher's Windows side, recorded: what it said, what it ran and in what order.</summary>
internal sealed class FakeHost : ILauncherHost
{
    public const string ZipName = "Metaphor.Installer.payload.zip";
    public const string ManifestName = "Metaphor.Installer.payload.sha256";

    public sealed record Saying(string Line, SpeechMode Mode, SpeechLibrary? Library);

    public TestPayload Payload { get; } = new();
    /// <summary>The list the exe carries; by default the one matching <see cref="Payload"/> at run time.</summary>
    public string? Manifest { get; set; }
    public string? Missing { get; set; }
    public bool Elevated { get; set; }
    public bool UacEnabled { get; set; } = true;
    public bool ReaderRunning { get; set; }
    public bool InstallerStarts { get; set; } = true;
    public bool CleanUpFails { get; set; }
    public int InstallerExitCode { get; set; }
    public string? TempFolderOverride { get; set; }

    public List<Saying> Said { get; } = [];
    public List<InstallerCommand> Commands { get; } = [];
    public List<string> Events { get; } = [];
    public List<string> FilesAtStart { get; } = [];
    public bool FolderExistedAtWait { get; private set; }
    private string? lastFolder;

    public string TempFolder { get; } = Directory.CreateTempSubdirectory("metaphor-host-").FullName;

    string ILauncherHost.TempFolder => TempFolderOverride ?? TempFolder;

    public string SystemFolder { get; } = Path.Combine("C:", "Windows", "System32");

    public bool IsElevated() => Elevated;

    public bool IsUacEnabled() => UacEnabled;

    public bool IsInteractive() => true;

    public bool IsScreenReaderRunning() => ReaderRunning;

    public Stream? OpenResource(string name)
    {
        if (name == Missing)
        {
            return null;
        }
        return name switch
        {
            ZipName => Payload.Zip(),
            ManifestName => new MemoryStream(Encoding.UTF8.GetBytes(Manifest ?? Payload.Manifest())),
            _ => null,
        };
    }

    public void Say(string line, SpeechMode mode, SpeechLibrary? library)
    {
        Events.Add("say");
        Said.Add(new Saying(line, mode, library));
    }

    public void EndSpeech() => Events.Add("end-speech");

    public int? RunInstaller(InstallerCommand command)
    {
        Events.Add("run");
        Commands.Add(command);
        var folder = Path.GetDirectoryName(command.WorkingDirectory)!;
        lastFolder = folder;
        FilesAtStart.AddRange(Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(folder, path).Replace('\\', '/')).Order(StringComparer.Ordinal));
        return InstallerStarts ? InstallerExitCode : null;
    }

    public bool DeleteFolder(string path) => !CleanUpFails && ScratchFolder.Delete(path);

    public void WaitForKey()
    {
        Events.Add("wait");
        FolderExistedAtWait = lastFolder is not null && Directory.Exists(lastFolder);
    }
}
