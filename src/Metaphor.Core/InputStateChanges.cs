namespace Metaphor.Core;

/// <summary>The #58 input spike's change filter: the game reads each pad and the keyboard every frame, and the spike logs
/// only when one's state differs from the last read. Keyed by source (a pad number or the keyboard). Safe to call from
/// any thread.</summary>
public sealed class InputStateChanges
{
    private readonly Dictionary<string, string> last = new(StringComparer.Ordinal);
    private readonly object gate = new();

    /// <summary>Records <paramref name="state"/> as <paramref name="source"/>'s latest, and says whether it differs from
    /// the one before. The first state seen for a source counts as a change.</summary>
    public bool Changed(string source, string state)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(state);
        lock (gate)
        {
            if (last.TryGetValue(source, out var before) && before == state)
            {
                return false;
            }
            last[source] = state;
            return true;
        }
    }
}
