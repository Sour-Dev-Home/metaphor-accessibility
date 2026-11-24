using Metaphor.Core;

namespace Metaphor.Hooks;

/// <summary>Issue #625's probe, development only: one message-log line per call of the archetype learn function
/// <c>LearnClass</c> (<c>0x389c70</c>), with the member, the class record, its class ID and whether the member already
/// had it (<see cref="LearnClassCall"/>), so one awakening in a session shows what a cue would hear. Observed before
/// the original runs, so the slots are as they were before learning. The function is whole in <c>.ecode</c>, not a jump
/// stub into <c>.text1</c> (Ghidra 12.2, <c>disasm 0x389c70</c>, game build 548ddc95…, Steam build 18330018). Only
/// reads and logs: the loader's adapter guards Observe and calls the original unchanged (rule 14); every argument is
/// observed pointer-sized (rule 9) and narrowed only for the log.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class LearnClassListener : IGameHook, IObserves3
{
    private readonly MessageLog? log;
    private readonly GameDataRead data;

    public LearnClassListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
        data = services.GameData;
    }

    public string SignatureName => "LearnClass";

    /// <summary><paramref name="first"/> holds the member in its low 32 bits, <paramref name="second"/> the class
    /// record; <paramref name="third"/> is passed on unread.</summary>
    public void Observe(nint first, nint second, nint third)
    {
        if (log is null)
        {
            return;
        }
        var member = unchecked((int)first);
        var classId = second == 0 ? null : ModuleMemoryReader.ReadInt32(second + LearnClassCall.ClassIdOffset);
        bool? known = null;
        var slots = new byte[LearnClassCall.SlotsLength];
        if (classId is { } id && data.UnitAddress(member) is { } unit &&
            ModuleMemoryReader.ReadBytes(unit + LearnClassCall.SlotsOffset, slots))
        {
            known = LearnClassCall.Known(slots, id);
        }
        log.MenuNote(LearnClassCall.Line(member, second, classId, known));
    }
}
