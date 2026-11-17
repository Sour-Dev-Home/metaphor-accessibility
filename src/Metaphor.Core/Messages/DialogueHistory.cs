namespace Metaphor.Core.Messages;

/// <summary>The last few dialogue lines (#126), kept silently as the cleaned "Name: line" text that is or would be
/// spoken, including lines left unspoken under the voiced-line modes (#57). Dialogue only, no menus. The repeat key
/// (#58) and the backlog fallback (#114) read it. Lines arrive from the game's threads, so every member is safe to call
/// from any of them.
/// The idea of a short history of spoken lines comes from Persona-4-Golden-Access by AquaRose7 (GPL-3.0,
/// <c>M/Components/Speech.cs</c> at <c>dcff255</c>; docs/raw-sources/persona-4-golden-access-2026-11-16.md). No code is
/// copied.</summary>
public sealed class DialogueHistory
{
    /// <summary>How many lines are kept unless a capacity is given.</summary>
    public const int DefaultCapacity = 20;

    private readonly Lock gate = new();
    private readonly Queue<string> lines = new();
    private readonly int capacity;

    public DialogueHistory(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        this.capacity = capacity;
    }

    /// <summary>The newest line, or null when none has been recorded.</summary>
    public string? Latest
    {
        get
        {
            lock (gate)
            {
                return lines.Count == 0 ? null : lines.Last();
            }
        }
    }

    /// <summary>Records a line as the newest, dropping the oldest when the buffer is full.</summary>
    public void Add(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        lock (gate)
        {
            lines.Enqueue(line);
            while (lines.Count > capacity)
            {
                lines.Dequeue();
            }
        }
    }

    /// <summary>A copy of the kept lines, oldest first.</summary>
    public IReadOnlyList<string> Lines()
    {
        lock (gate)
        {
            return [.. lines];
        }
    }
}
