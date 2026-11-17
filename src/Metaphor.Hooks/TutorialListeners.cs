using System.Globalization;
using Metaphor.Core;

namespace Metaphor.Hooks;

// Issues #118 and #109: development-only hooks to find how a tutorial pop-up gets its pages, and so which page (and
// which <PAD_ICON n> numbers in it) is on screen. The functions, their arguments and the names (ours) are in
// docs/raw-sources/tutorial-and-icon-leads-2026-11-16.md. Both are in .ecode, so neither needs an opt-in. Integers are
// pointer-sized (rule 9) and narrowed only for the log; numbers and labels only, no game text.

/// <summary>The function every tutorial pop-up request goes through, from the script function
/// <c>TutorialWindowOpen</c> and ten direct callers in native code: <c>bool (manager*, const request*)</c>, the request
/// being a 32-bit tutorial number and three bytes. Observed after it runs, with its result.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class TutorialWindowOpenRequestListener : IGameHook, IObserves4WithResult
{
    private readonly MessageLog? log;

    public TutorialWindowOpenRequestListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "TutorialWindowOpenRequest";

    public void Observe(nint manager, nint request, nint unused3, nint unused4, nint result)
    {
        if (log is null)
        {
            return;
        }
        var flags = new byte[3];
        var flagsText = ModuleMemoryReader.ReadAt(request, 4, flags)
            ? string.Create(CultureInfo.InvariantCulture, $"flags={flags[0]},{flags[1]},{flags[2]}")
            : "flags=(unreadable)";
        log.Requested(SignatureName,
            $"{NameListening.Number("id", NameListening.Int32At(request))} {flagsText} result={(byte)result}");
    }
}

/// <summary>A message label resolved to where it lives: <c>void (const char* label, int* file, int* index)</c>, which
/// sets both to -1, then looks the label's MD5 up. Observed after it runs, so the file and index are its answer. About
/// 45 direct callers; whether the tutorial window is one is what this hook is here to find out.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class MessageLocationByLabelListener : IGameHook, IObserves4WithResult
{
    private readonly MessageLog? log;
    private readonly PlayerNameCheck playerName;
    private readonly RepeatGate repeats = NameListening.NewGate();

    public MessageLocationByLabelListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
        playerName = PlayerNameCheck.FromModFolder(services.ModFolder);
    }

    public string SignatureName => "MessageLocationByLabel";

    public void Observe(nint label, nint file, nint index, nint unused4, nint unused5)
    {
        var fileNumber = NameListening.Int32At(file);
        var indexNumber = NameListening.Int32At(index);
        if (log is null || !repeats.LetThrough(label, ((long)(fileNumber ?? -2) << 32) | (uint)(indexNumber ?? -2)))
        {
            return;
        }
        log.Resolved(SignatureName,
            $"{playerName.LabelField(ModuleMemoryReader.ReadAnsiString(label))} {NameListening.Number("file", fileNumber)} {NameListening.Number("index", indexNumber)}");
    }
}
