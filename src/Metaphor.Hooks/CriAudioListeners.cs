using Metaphor.Core;

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
    private readonly CallerAddress caller = new();

    public CriCueSetListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "criAtomExPlayer_SetCueId";

    public void Observe(nint player, nint acb, nint cueId)
    {
        if (log is null)
        {
            return;
        }
        log.CueSet((int)cueId, acb, player, caller.Find());
    }
}
