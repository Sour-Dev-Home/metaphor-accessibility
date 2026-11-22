using System.IO;

namespace Metaphor.Installer;

/// <summary>What the launcher needs from Windows: the exe's own host (Windows/WindowsHost.cs), or a stand-in in tests.</summary>
internal interface ILauncherHost
{
    /// <summary>The player's own temp folder.</summary>
    string TempFolder { get; }

    /// <summary>Windows' System32 folder.</summary>
    string SystemFolder { get; }

    bool IsElevated();

    /// <summary>False only when User Account Control is turned off (EnableLUA is 0).</summary>
    bool IsUacEnabled();

    /// <summary>True when the exe has a console a player can read and answer in.</summary>
    bool IsInteractive();

    bool IsScreenReaderRunning();

    /// <summary>One of the exe's embedded resources, or null when it has no such resource.</summary>
    Stream? OpenResource(string name);

    /// <summary>Prints LINE and speaks it by MODE: through the reader only with LIBRARY, a checked prism.dll.</summary>
    void Say(string line, SpeechMode mode, SpeechLibrary? library);

    /// <summary>Stops the exe's own speech and unloads the speech library, so the folder it is in can be deleted.</summary>
    void EndSpeech();

    /// <summary>Runs COMMAND in this console, waits for it and returns its exit code, or null when it didn't start.</summary>
    int? RunInstaller(InstallerCommand command);

    /// <summary>Deletes the unpacked folder (<see cref="ScratchFolder.Delete"/>); true when it is gone.</summary>
    bool DeleteFolder(string path);

    /// <summary>Waits for a key, so the window stays open for the screen reader to read it; returns at once without a
    /// console to read a key from.</summary>
    void WaitForKey();
}
