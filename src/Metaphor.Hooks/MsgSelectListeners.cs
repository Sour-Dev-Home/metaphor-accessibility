using Metaphor.Core;

namespace Metaphor.Hooks;

// Issue #93: a development-only hook on what MsgGetSelectedID runs, to see which option the game reads back. The
// function, its arguments and the name (ours) are in docs/raw-sources/msg-select-functions-2026-11-16.md. The choice
// itself is reported by DialogueChoiceHook (#82), whose event the dev log observes. Every integer or pointer is
// observed pointer-sized (rule 9) and narrowed only for the log.

/// <summary>Where <c>MsgGetSelectedID</c>'s target jumps: one pointer (an object the target found) in, the selection
/// out as 32 bits, observed after it returns.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class MsgSelectedIdListener : IGameHook, IObserves1WithResult
{
    private readonly MessageLog? log;

    public MsgSelectedIdListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "MsgSelectedId";

    public void Observe(nint first, nint result) => log?.SelectedId((int)result);
}
