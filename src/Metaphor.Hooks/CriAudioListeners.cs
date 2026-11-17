using Metaphor.Core;
using Metaphor.Core.Messages;

namespace Metaphor.Hooks;

/// <summary>Issues #93 (lead 1: the cursor-move sound) and #103 (voice cues): a development-only hook on CRI ADX2's
/// <c>criAtomExPlayer_SetCueId</c>, which names the cue a player will play next. Each call is logged with the cue's
/// number and the calling code (<see cref="CallerAddress"/>), so the callers that repeat with each menu move lead to the
/// focus code. The function, its pattern and its arguments come from Ryo Framework
/// (docs/raw-sources/ryo-cri-atom-patterns-2026-11-16.md): <c>void (nint playerHn, nint acbHn, int cueId)</c>. The cue
/// number is a 32-bit integer there, so only the low half of the third register is read (rule 9: the hook still passes
/// all 64 bits through). That this is the function CRI names <c>criAtomExPlayer_SetCueId</c> in Metaphor's build rests
/// on Ryo's pattern alone, and is unverified until a run logs cues as menus play sounds.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class CriCueSetListener : IGameHook, IObserves3
{
    private readonly MessageLog? log;
    private readonly VoiceCueCheck? voiceCues;
    private readonly CallerAddress caller = new();

    public CriCueSetListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
        voiceCues = services.VoiceCues;
    }

    public string SignatureName => "criAtomExPlayer_SetCueId";

    public void Observe(nint player, nint acb, nint cueId)
    {
        if (log is null)
        {
            return;
        }
        log.CueSet((int)cueId, acb, player, caller.Find());
        // The check hears lines only once CriPlayerCategoryListener's opt-in starts it; until then this does nothing.
        voiceCues?.CueSet((int)cueId, player);
    }
}

// Issue #103: development-only, opt-in listeners to find where the game holds the voice volume and the voice language,
// and which CRI category its voices play in. Functions, patterns and arguments come from Ryo Framework
// (docs/raw-sources/ryo-cri-atom-patterns-2026-11-16.md); each matched once on disk on build
// 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504, and its prologue fits Ryo's arguments
// (docs/raw-sources/cri-voice-probes-2026-11-17.md). That these are the CRI functions Ryo names, and that the game sets
// its volumes through them, is unverified until a run. Numbers, file paths and calling code only.

/// <summary>CRI's <c>criAtomExCategory_SetVolumeById</c>: <c>void (uint id, float volume)</c>. Logs each category's
/// volume when it changes, with the calling code, so moving the game's voice volume slider names the voice category
/// and leads to the code that holds the setting. Opt-in.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true, OptInFile = VoiceVolumeOptIn.File)]
internal sealed class CriCategoryVolumeListener : IGameHook, IObserves2WithDouble
{
    private readonly MessageLog? log;
    private readonly CallerAddress caller = new();
    private readonly ChangeGate gate = new();

    public CriCategoryVolumeListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "criAtomExCategory_SetVolumeById";

    public void Observe(nint first, double second)
    {
        var volume = RegisterBits.LowSingle(second);
        if (log is null || !gate.Changed((uint)first, BitConverter.GetBytes(volume)))
        {
            return;
        }
        log.CategoryVolume((uint)first, volume, caller.Find());
    }
}

/// <summary>CRI's <c>criAtomExPlayer_SetVolume</c>: <c>void (player, float volume)</c>. Logs a player's volume when it
/// changes, in case the game applies its voice volume per player rather than per category. Opt-in, with the category
/// volume listener.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true, OptInFile = VoiceVolumeOptIn.File)]
internal sealed class CriPlayerVolumeListener : IGameHook, IObserves2WithDouble
{
    private readonly MessageLog? log;
    private readonly CallerAddress caller = new();
    private readonly ChangeGate gate = new();

    public CriPlayerVolumeListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "criAtomExPlayer_SetVolume";

    public void Observe(nint first, double second)
    {
        var volume = RegisterBits.LowSingle(second);
        if (log is null || !gate.Changed(first, BitConverter.GetBytes(volume)))
        {
            return;
        }
        log.PlayerVolume(first, volume, caller.Find());
    }
}

/// <summary>CRI's <c>criAtomExPlayer_SetCategoryById</c>: <c>void (player, uint id)</c>. Logs which category each player
/// is given, and hands it to the <see cref="VoiceCueCheck"/>, which this listener starts: with it on, each requested
/// line gets a <c>voice-check</c> record of whether it is <c>&lt;VOICE&gt;</c>-tagged and which cues followed. Opt-in.
/// </summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true, OptInFile = "opt-in-voice-cues.txt")]
internal sealed class CriPlayerCategoryListener : IGameHook, IObserves3
{
    private readonly MessageLog? log;
    private readonly VoiceCueCheck? voiceCues;
    private readonly CallerAddress caller = new();
    private readonly ChangeGate gate = new();

    public CriPlayerCategoryListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
        voiceCues = services.VoiceCues;
        if (voiceCues is not null)
        {
            services.Messages.ObserveLines(voiceCues);
        }
    }

    public string SignatureName => "criAtomExPlayer_SetCategoryById";

    public void Observe(nint first, nint second, nint third)
    {
        var category = (uint)second;
        voiceCues?.PlayerCategory(first, category);
        if (log is null || !gate.Changed(first, BitConverter.GetBytes(category)))
        {
            return;
        }
        log.PlayerCategory(first, category, caller.Find());
    }
}

/// <summary>CRI's <c>criAtomExAcb_LoadAcbData</c>: <c>nint (void* acbData, int acbDataSize, nint awbBinder, char* awbPath,
/// void* work, int workSize)</c>. Logs each cue sheet loaded, with its wave bank's path, so switching the game's voice
/// language shows which banks (and folders) each language loads. Opt-in.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true, OptInFile = "opt-in-voice-banks.txt")]
internal sealed class CriAcbLoadListener : IGameHook, IObserves6
{
    // Longer than Windows' classic path limit, so a full path isn't cut.
    private const int PathBytes = 520;

    private readonly MessageLog? log;
    private readonly CallerAddress caller = new();

    public CriAcbLoadListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "criAtomExAcb_LoadAcbData";

    public void Observe(nint first, nint second, nint third, nint fourth, nint fifth, nint sixth)
    {
        if (log is null)
        {
            return;
        }
        var awbPath = fourth == 0 ? null : ModuleMemoryReader.ReadAnsiString(fourth, PathBytes);
        log.AcbLoaded((int)second, awbPath, caller.Find());
    }
}

/// <summary>The opt-in file shared by the two #103 volume listeners.</summary>
internal static class VoiceVolumeOptIn
{
    public const string File = "opt-in-voice-volume.txt";
}
