using System;
using System.Collections.Generic;
using System.IO;

namespace Metaphor.Installer;

/// <summary>
/// The installer exe's steps (#608, ADR 0020 option A), each through <see cref="ILauncherHost"/>:
/// <list type="number">
/// <item>As administrator, with User Account Control on, it stops before unpacking anything, as install.ps1 does; with
/// it off, install.ps1 asks.</item>
/// <item>Reads the list of the payload's files, makes a new folder in the player's temp folder, unpacks the release zip
/// into it and checks every file against the list. A difference is a damaged download: it says so and installs
/// nothing.</item>
/// <item>Says the welcome line, through the screen reader with the checked prism.dll, then lets go of it.</item>
/// <item>Runs install.ps1 in this console (<see cref="InstallerCommand"/>), deletes the folder, and waits for a key.</item>
/// </list>
/// The uninstall runs from the installed mod's own copy, and nothing outside the folder remembers it, so the exe needs
/// nothing left behind; the Installed-apps entry (#614) belongs to install.ps1, which the zip runs too.
/// Exit codes: install.ps1's own (0 installed, 1 not); 1 when the exe stopped before it.
/// </summary>
internal static class Launcher
{
    public const string ZipResource = "Metaphor.Installer.payload.zip";
    public const string ManifestResource = "Metaphor.Installer.payload.sha256";

    public const string Welcome = "Welcome to the Metaphor accessibility installer. Download checked. The installer starts now.";
    public const string RunsAsAdministrator = "This window runs as administrator, so the installer stopped without changing anything. Open the installer again from File Explorer, without administrator rights: it asks Windows for permission itself where a step needs it.";
    public const string Damaged = "This download is damaged. Download it again from the Releases page.";
    public const string LinkFound = "Another program changed the installer's files while it unpacked them, so it stopped without changing anything. Run the installer again.";
    public const string CouldNotUnpack = "The installer could not unpack its files into your temp folder, so it stopped without changing anything. Free some disk space, then run it again.";
    public const string PowerShellDidNotStart = "Windows PowerShell did not start, so nothing was installed. Use the zip from the Releases page instead.";
    public const string CouldNotCleanUp = "The installer could not delete its unpacked files from your temp folder. Windows' Disk Cleanup removes them.";
    public const string PressAKey = "Press any key to close this window.";

    public const int Stopped = 1;

    public static int Run(ILauncherHost host, IReadOnlyList<string> args)
    {
        var mode = SpeechChoice.Select(args, host.IsScreenReaderRunning(), host.IsInteractive());
        if (host.IsElevated() && host.IsUacEnabled())
        {
            return Stop(host, mode, RunsAsAdministrator);
        }

        PayloadManifest manifest;
        try
        {
            manifest = PayloadManifest.Parse(ReadManifest(host));
        }
        catch (PayloadDamagedException)
        {
            return Stop(host, mode, Damaged);
        }

        string folder;
        try
        {
            folder = ScratchFolder.Create(host.TempFolder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Stop(host, mode, CouldNotUnpack);
        }

        int? exitCode = null;
        string? failure = null;
        try
        {
            failure = Unpack(host, manifest, folder);
            if (failure is null)
            {
                host.Say(Welcome, mode, Library(manifest, folder));
                host.EndSpeech();
                exitCode = host.RunInstaller(InstallerCommand.For(host.SystemFolder, folder, manifest.ModFolder, args));
                failure = exitCode is null ? PowerShellDidNotStart : null;
            }
        }
        finally
        {
            if (!host.DeleteFolder(folder))
            {
                failure ??= CouldNotCleanUp;
            }
        }
        if (exitCode is null)
        {
            return Stop(host, mode, failure ?? PowerShellDidNotStart);
        }
        // After the installer, only the clean-up can have failed; install.ps1 has already said to press a key.
        if (failure is not null)
        {
            host.Say(failure, mode, null);
        }
        host.WaitForKey();
        return exitCode.Value;
    }

    // Says LINE and that a key closes the window, waits for one, and returns the exit code for a stop.
    private static int Stop(ILauncherHost host, SpeechMode mode, string line)
    {
        host.Say(line, mode, null);
        host.Say(PressAKey, mode, null);
        host.WaitForKey();
        return Stopped;
    }

    private static string ReadManifest(ILauncherHost host)
    {
        using var stream = host.OpenResource(ManifestResource)
            ?? throw new PayloadDamagedException("the exe carries no file list");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // Unpacks and checks the payload into FOLDER; returns the line to stop with, or null when it all checked out.
    private static string? Unpack(ILauncherHost host, PayloadManifest manifest, string folder)
    {
        try
        {
            using var zip = host.OpenResource(ZipResource) ?? throw new PayloadDamagedException("the exe carries no payload");
            Payload.Unpack(zip, manifest, folder);
            Payload.Verify(folder, manifest);
            return null;
        }
        catch (PayloadDamagedException)
        {
            return Damaged;
        }
        catch (PayloadLinkException)
        {
            return LinkFound;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return CouldNotUnpack;
        }
    }

    // The checked prism.dll in the unpacked mod folder, when the payload carries one.
    private static SpeechLibrary? Library(PayloadManifest manifest, string folder)
    {
        var sha256 = manifest.HashOf(manifest.ModFolder + "/prism.dll");
        return sha256 is null ? null : new SpeechLibrary(Path.Combine(folder, manifest.ModFolder, "prism.dll"), sha256);
    }
}
