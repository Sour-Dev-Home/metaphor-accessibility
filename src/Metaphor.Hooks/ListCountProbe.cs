using Metaphor.Core;
using Metaphor.Core.Menus;

namespace Metaphor.Hooks;

/// <summary>Logs, for each cursor input, the moving manager's item count and item vector when they differ from the last
/// read of that manager (#249), so a session can match them with the number of rows on screen. No hook of its own: it
/// hears the always-on cursor hook's inputs and reads the manager through <see cref="ModuleMemoryReader"/>. The offsets
/// are in <see cref="CursorListCount"/>. Numbers only.</summary>
/// <remarks>Debug only (a log exists) and opt-in, when <see cref="OptInFile"/> is in the mod's folder (#250).</remarks>
internal sealed class ListCountProbe : ICursorObserver
{
    /// <summary>The file in the mod's folder that turns the probe on.</summary>
    public const string OptInFile = "opt-in-list-count.txt";

    // Most items whose flag bytes are read on one input; a longer list logs its flags as unknown.
    private const int MaxItemsRead = 512;
    private const int FirstFlag = 8;

    private readonly MessageLog log;
    private readonly InputStateChanges changes = new();

    public ListCountProbe(MessageLog log) => this.log = log;

    public void Observe(CursorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var manager = (nint)input.Manager;
        var bytes = new byte[CursorListCount.ManagerBytes];
        if (!ModuleMemoryReader.ReadBytes(manager, bytes) || CursorListCount.From(bytes) is not { } list)
        {
            return;
        }
        var (flagged8, flagged9) = list.Items <= MaxItemsRead ? CountFlags(manager, list.Items) : (null, null);
        var read = new CursorListCount(input.Manager, list.Count, list.Items, flagged8, flagged9);
        if (changes.Changed(input.Manager.ToString("x", System.Globalization.CultureInfo.InvariantCulture), read.ToString()))
        {
            log.ListCountRead(read);
        }
    }

    /// <summary>How many items have their byte <c>+8</c>, and their byte <c>+9</c>, set; null when any item is
    /// unreadable.</summary>
    private static (int?, int?) CountFlags(nint manager, long items)
    {
        if (ModuleMemoryReader.ReadPointer(manager + CursorListCount.ItemsBeginOffset) is not { } begin)
        {
            return (null, null);
        }
        int set8 = 0, set9 = 0;
        var flags = new byte[2];
        for (var i = 0; i < items; i++)
        {
            if (ModuleMemoryReader.ReadPointer(begin + (nint)(i * CursorListCount.ItemSize)) is not { } item || item == 0 ||
                !ModuleMemoryReader.ReadBytes(item + FirstFlag, flags))
            {
                return (null, null);
            }
            set8 += flags[0] != 0 ? 1 : 0;
            set9 += flags[1] != 0 ? 1 : 0;
        }
        return (set8, set9);
    }
}
