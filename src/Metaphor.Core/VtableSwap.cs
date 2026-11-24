namespace Metaphor.Core;

/// <summary>What <see cref="VtableSwap.Swap"/> did.</summary>
public enum VtableSwapResult
{
    /// <summary>The slot now holds the replacement.</summary>
    Swapped,

    /// <summary>The vtable wasn't the one expected (its class or the slot's pointer differed, or a read failed), so
    /// nothing was written.</summary>
    NotMatched,

    /// <summary>The write failed or didn't read back; the original pointer was written back if the slot had changed.
    /// </summary>
    Failed,
}

/// <summary>One vtable slot swapped for the mod's own function and back (#253, ADR 0007), as kept here so it is tested
/// without the game: the hook layer hands it its reads and its one write. It writes only after reading that the
/// vtable's class is the one expected and the slot holds <c>expected</c>, reads each write back, and on any failure
/// puts the original pointer back. Not for several threads at once.</summary>
public sealed class VtableSwap
{
    private readonly string expectedClass;
    private readonly long moduleBase;
    private readonly long moduleSize;
    private readonly Func<long, long?> readPointer;
    private readonly Func<long, int?> readInt32;
    private readonly Func<long, string?> readAnsi;
    private readonly Func<long, long, bool> writePointer;
    private long slot;
    private long original;
    private long replacement;

    /// <summary>A swap in the module at <paramref name="moduleBase"/> for a vtable of class
    /// <paramref name="expectedClass"/>, as <see cref="RttiName.OfVtable"/> names it; <paramref name="writePointer"/>
    /// writes one pointer and says whether it could.</summary>
    public VtableSwap(
        string expectedClass, long moduleBase, long moduleSize, Func<long, long?> readPointer,
        Func<long, int?> readInt32, Func<long, string?> readAnsi, Func<long, long, bool> writePointer)
    {
        ArgumentException.ThrowIfNullOrEmpty(expectedClass);
        ArgumentNullException.ThrowIfNull(readPointer);
        ArgumentNullException.ThrowIfNull(readInt32);
        ArgumentNullException.ThrowIfNull(readAnsi);
        ArgumentNullException.ThrowIfNull(writePointer);
        this.expectedClass = expectedClass;
        this.moduleBase = moduleBase;
        this.moduleSize = moduleSize;
        this.readPointer = readPointer;
        this.readInt32 = readInt32;
        this.readAnsi = readAnsi;
        this.writePointer = writePointer;
    }

    /// <summary>Whether the slot holds the replacement now.</summary>
    public bool IsSwapped { get; private set; }

    /// <summary>Puts <paramref name="with"/> in slot <paramref name="index"/> of the vtable at
    /// <paramref name="vtable"/>, if that slot holds <paramref name="expected"/>. Called at most once.</summary>
    public VtableSwapResult Swap(long vtable, int index, long expected, long with)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (IsSwapped || slot != 0 || expected == 0 || with == 0)
        {
            return VtableSwapResult.NotMatched;
        }
        var at = vtable + 8L * index;
        if (RttiName.OfVtable(vtable, moduleBase, moduleSize, readPointer, readInt32, readAnsi) != expectedClass ||
            readPointer(at) != expected)
        {
            return VtableSwapResult.NotMatched;
        }
        (slot, original, replacement) = (at, expected, with);
        if (writePointer(at, with) && readPointer(at) == with)
        {
            IsSwapped = true;
            return VtableSwapResult.Swapped;
        }
        // A write that failed may still have landed, or half landed: put the original back unless it's there.
        if (readPointer(at) != expected)
        {
            writePointer(at, expected);
        }
        return VtableSwapResult.Failed;
    }

    /// <summary>Puts the original pointer back, if the slot still holds the replacement (nothing else has swapped it
    /// since). True when the slot holds the original afterwards.</summary>
    public bool Restore()
    {
        if (!IsSwapped)
        {
            return slot == 0 || readPointer(slot) == original;
        }
        if (readPointer(slot) == replacement && writePointer(slot, original) && readPointer(slot) == original)
        {
            IsSwapped = false;
            return true;
        }
        return readPointer(slot) == original;
    }
}
