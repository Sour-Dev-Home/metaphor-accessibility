using System;
using System.Collections.Generic;
using System.Linq;

namespace Metaphor.Installer;

/// <summary>How the exe speaks a line it prints.</summary>
internal enum SpeechMode
{
    /// <summary>Printed only.</summary>
    None,

    /// <summary>Windows' own voice (System.Speech).</summary>
    Voice,

    /// <summary>The running screen reader, through the prism.dll the payload carries.</summary>
    Reader,
}

/// <summary>The speech library to speak through the reader with: prism.dll at PATH, used only while it still has the
/// SHA-256 the payload's list gives it.</summary>
internal sealed class SpeechLibrary(string path, string sha256)
{
    public string Path { get; } = path;

    public string Sha256 { get; } = sha256;
}

/// <summary>The choice install.ps1's Select-Speech makes for the installer's lines (scripts/player/MetaphorInstall.psm1),
/// made the same way for the exe's own, so both speak alike: <c>-NoSpeech</c> wins, then <c>-Speak</c>; without a
/// console it prints only; with a screen reader running it speaks through the reader, else with Windows' voice.</summary>
internal static class SpeechChoice
{
    public static SpeechMode Select(IReadOnlyList<string> args, bool readerRunning, bool interactive)
    {
        if (Has(args, "-NoSpeech"))
        {
            return SpeechMode.None;
        }
        if (Has(args, "-Speak"))
        {
            return SpeechMode.Voice;
        }
        // A scripted run (-Answers) counts as having a console, as in install.ps1.
        if (!interactive && !Has(args, "-Answers"))
        {
            return SpeechMode.None;
        }
        return readerRunning ? SpeechMode.Reader : SpeechMode.Voice;
    }

    private static bool Has(IReadOnlyList<string> args, string flag) =>
        args.Any(arg => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));
}
