using Metaphor.Core;
using Metaphor.Features.Field;

namespace Metaphor.Hooks;

// Issue #55's spike: a hook that logs what one of the game's message functions sees, to HookServices.Log, and since
// #181 feeds "where am I". The loader's adapter passes every call through unchanged (#88). Compiled message files are logged
// by MessageFileHook's log observer (#86). Signature and argument shape are MF.Toolkit's
// (docs/raw-sources/game-text-hooks-community-2026-11-15.md), checked on this build
// (docs/raw-sources/mf-toolkit-message-signatures-2026-11-15.md).

/// <summary>Hands each <c>MapName.msg</c> lookup to "where am I" (#181), with the map id it was made on, and logs each
/// message the game looks up, by message number and label index. MF.Toolkit only calls this function; hooking it is new.
/// It runs in every build since #181: the Debug log of 2026-11-21 saw it fire with message id 3 beside each map strip
/// (docs/findings/field-nearby-unit-names-static.md).</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class GetMessageByLabelIndexListener : IGameHook, IObserves4
{
    private readonly MessageLog? log;
    private readonly FieldPlace? place;
    private readonly FieldMemory? field;

    public GetMessageByLabelIndexListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
        place = services.FieldPlace;
        field = services.Field;
    }

    public string SignatureName => "GetMessageByLabelIndex";

    /// <summary>MF.Toolkit MessageService.cs line 32, with its pointer types to MSG, MsgFlag and MsgConfigs kept
    /// opaque, and its int label index pointer-sized (rule 9).</summary>
    public void Observe(nint msgId, nint labelIdx, nint flags, nint configs)
    {
        // What a caller passes is checked only by the Debug log's ids and labels: read it safely.
        var id = ModuleMemoryReader.ReadInt32(msgId) ?? -1;
        log?.LookedUp(id, (int)labelIdx);
        if (id == FieldPlace.MapNameEntry)
        {
            place?.LookedUp(id, (int)labelIdx, field?.MapId());
        }
    }
}
