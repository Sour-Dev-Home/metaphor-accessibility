using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Speech.Synthesis;
using Microsoft.Win32;

namespace Metaphor.Installer.Windows;

/// <summary>The launcher's Windows side, for the exe players run.</summary>
internal sealed class WindowsHost : ILauncherHost, IDisposable
{
    private ReaderSpeech? reader;
    private bool readerTried;
    private SpeechSynthesizer? voice;
    private bool voiceTried;

    public string TempFolder => Path.GetTempPath();

    public string SystemFolder => Environment.SystemDirectory;

    // CI's Windows runners are administrator accounts, so the tests there set the switch install.ps1's Test-Elevated
    // reads (scripts/player/MetaphorInstall.psm1), which counts only with CI=true.
    public bool IsElevated()
    {
        if (Environment.GetEnvironmentVariable("CI") == "true"
            && Environment.GetEnvironmentVariable("METAPHOR_INSTALL_TEST_ALLOW_ADMIN") == "1")
        {
            return false;
        }
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    // As install.ps1's Test-UacEnabled: only a DWORD 0 turns it off; a missing or unreadable value counts as on.
    public bool IsUacEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
            return !(key?.GetValue("EnableLUA") is int value && value == 0
                && key.GetValueKind("EnableLUA") == RegistryValueKind.DWord);
        }
        catch (Exception e) when (e is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    public bool IsInteractive() => Environment.UserInteractive && !Console.IsInputRedirected;

    // As install.ps1's Find-ScreenReader: NVDA by its program, JAWS and ZoomText by the windows Prism looks for.
    public bool IsScreenReaderRunning()
    {
        var nvda = Process.GetProcessesByName("nvda");
        var running = nvda.Length > 0;
        foreach (var process in nvda)
        {
            process.Dispose();
        }
        return running
            || NativeMethods.FindWindow("JFWUI2", null) != IntPtr.Zero
            || NativeMethods.FindWindow("ZXSPEECHWNDCLASS", "ZoomText Speech Processor") != IntPtr.Zero;
    }

    public Stream? OpenResource(string name) => Assembly.GetExecutingAssembly().GetManifestResourceStream(name);

    public void Say(string line, SpeechMode mode, SpeechLibrary? library)
    {
        Console.WriteLine(line);
        if (mode == SpeechMode.Reader)
        {
            Reader(library)?.Speak(line);
        }
        else if (mode == SpeechMode.Voice)
        {
            Speak(line);
        }
    }

    // Unloads prism.dll, which is in the folder about to be deleted; Windows' voice, which isn't, stays for later lines.
    public void EndSpeech()
    {
        reader?.Dispose();
        reader = null;
    }

    public int? RunInstaller(InstallerCommand command)
    {
        var start = new ProcessStartInfo(command.FileName, command.Arguments)
        {
            UseShellExecute = false,
            WorkingDirectory = command.WorkingDirectory,
        };
        // Started from a PowerShell 7 window, the module path names PowerShell 7's modules, which 5.1 can't load; with
        // none set, 5.1 uses its own (install.cmd).
        start.EnvironmentVariables.Remove("PSModulePath");
        // Ctrl+C reaches every process in the console: the installer handles it, and the exe stays to clean up.
        ConsoleCancelEventHandler stay = (_, e) => e.Cancel = true;
        Console.CancelKeyPress += stay;
        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception)
        {
            return null;
        }
        finally
        {
            Console.CancelKeyPress -= stay;
        }
    }

    public bool DeleteFolder(string path) => ScratchFolder.Delete(path);

    public void WaitForKey()
    {
        if (Console.IsInputRedirected)
        {
            return;
        }
        try
        {
            Console.ReadKey(intercept: true);
        }
        catch (InvalidOperationException)
        {
            // No console to read a key from.
        }
    }

    public void Dispose()
    {
        EndSpeech();
        voice?.Dispose();
        voice = null;
    }

    private ReaderSpeech? Reader(SpeechLibrary? library)
    {
        if (!readerTried && library is not null)
        {
            readerTried = true;
            reader = ReaderSpeech.Open(library);
        }
        return reader;
    }

    // Windows' own voice, English as the lines are (install.ps1's Select-EnglishVoice: en-US, then en-GB, then any),
    // spoken to the end before the exe goes on, so it never talks over the installer. Prints only when it can't start.
    private void Speak(string line)
    {
        if (!voiceTried)
        {
            voiceTried = true;
            try
            {
                voice = new SpeechSynthesizer();
                voice.SetOutputToDefaultAudioDevice();
#pragma warning disable CA1304 // Every installed voice, whatever the locale: the filter below picks by culture name.
                var english = voice.GetInstalledVoices()
#pragma warning restore CA1304
                    .Where(installed => installed.Enabled && IsEnglish(installed.VoiceInfo.Culture?.Name))
                    .OrderBy(installed => Rank(installed.VoiceInfo.Culture!.Name))
                    .FirstOrDefault();
                if (english is not null)
                {
                    voice.SelectVoice(english.VoiceInfo.Name);
                }
            }
            catch (Exception e) when (e is InvalidOperationException or PlatformNotSupportedException or ArgumentException)
            {
                voice?.Dispose();
                voice = null;
            }
        }
        try
        {
            voice?.Speak(line);
        }
        catch (InvalidOperationException)
        {
            // The voice stopped working; the line is printed.
        }
    }

    private static bool IsEnglish(string? tag) => tag is not null && (tag == "en" || tag.StartsWith("en-", StringComparison.Ordinal));

    private static int Rank(string tag) => tag switch
    {
        "en-US" => 0,
        "en-GB" => 1,
        _ => 2,
    };
}
