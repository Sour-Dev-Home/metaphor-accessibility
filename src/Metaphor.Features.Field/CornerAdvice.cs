using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>Says the top-left corner advice (#587): Gallica's, and the party's corner lines, as the game opens each
/// one, with the speaker's name, queued so it never cuts a line. The hook layer hands it each open's key, a label or the
/// label's 16-byte hash, and the text comes from the compiled report file in the <see cref="IMessageLookup"/>.</summary>
/// <remarks>Build 548ddc95… (Steam build 18330018): every corner line is a <c>message/report/*.msg</c> entry, opened
/// through <c>0xf81cd0</c>, whose key is a label or a 16-byte label hash
/// (docs/findings/corner-advice-report-message-open-static.md). That the hash is the label's MD5, and that the other
/// kind's key is the label's name, are [NEEDS VERIFICATION]: a miss is logged with its key. The game queues a line and
/// shows it later, so a line opened outside free roam waits for free roam (rule 19), up to
/// <see cref="HoldMilliseconds"/>, checked on each input read.</remarks>
[FeatureSetting<FieldFeature>(FieldFeature.CornerAdvice)]
public sealed class CornerAdvice : IInputControls
{
    /// <summary>How long a line opened outside free roam waits for it before it is dropped.</summary>
    public const long HoldMilliseconds = 10_000;

    // A few lines at most queue in the game's corner at once; more waiting means free roam is long gone.
    private const int MostHeld = 4;

    private readonly IMessageLookup messages;
    private readonly TextCleaner cleaner;
    private readonly IAnnouncer announcer;
    private readonly FreeRoamGate gate;
    private readonly Func<long> milliseconds;
    private readonly Action<string>? log;
    private readonly Queue<(long At, string Line)> held = new();
    private readonly Lock heldGate = new();

    /// <param name="messages">The compiled message files, report files among them.</param>
    /// <param name="cleaner">Cleans the line and the name for speech.</param>
    /// <param name="announcer">Where a line is said, behind its setting.</param>
    /// <param name="gate">The free-roam gate (rule 19).</param>
    /// <param name="milliseconds">The clock.</param>
    /// <param name="log">The Debug log, or null.</param>
    public CornerAdvice(
        IMessageLookup messages, TextCleaner cleaner, IAnnouncer announcer, FreeRoamGate gate, Func<long> milliseconds,
        Action<string>? log = null)
    {
        this.messages = messages ?? throw new ArgumentNullException(nameof(messages));
        this.cleaner = cleaner ?? throw new ArgumentNullException(nameof(cleaner));
        this.announcer = announcer ?? throw new ArgumentNullException(nameof(announcer));
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        this.milliseconds = milliseconds ?? throw new ArgumentNullException(nameof(milliseconds));
        this.log = log;
    }

    /// <summary>Whether the hook should read an open's key: the setting, which the module sets. Always, when unset.
    /// </summary>
    public Func<bool> Wanted { get; init; } = () => true;

    /// <summary>The game opened a report line by its <paramref name="label"/> or by its label's hash,
    /// <paramref name="labelHash"/> as 32 hex digits; either may be null. Said now in free roam, else held.</summary>
    public void Opened(string? label, string? labelHash)
    {
        var message = label is not null ? messages.Find(label)
            : labelHash is not null ? messages.FindByLabelHash(labelHash) : null;
        if (message is null)
        {
            log?.Invoke($"corner advice: no message for {(label is not null ? "label " + label : "hash " + labelHash)}");
            return;
        }
        log?.Invoke($"corner advice: matched label {message.Label}");
        var text = cleaner.Clean(message.Text);
        if (text.Length == 0)
        {
            return;
        }
        var name = cleaner.Clean(message.Speaker ?? "");
        var line = name.Length == 0 ? text : $"{name}: {text}";
        if (gate.Open())
        {
            Say(line);
            return;
        }
        lock (heldGate)
        {
            if (held.Count == MostHeld)
            {
                held.Dequeue();
            }
            held.Enqueue((milliseconds(), line));
        }
    }

    /// <summary>Says the held lines once free roam is back; hands the buttons on unchanged.</summary>
    public int Pad(int pad, int buttons, bool made)
    {
        SayHeld();
        return buttons;
    }

    /// <summary>Says the held lines once free roam is back; hands the keys on unchanged.</summary>
    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        SayHeld();
        return keys;
    }

    private void SayHeld()
    {
        List<string> due;
        lock (heldGate)
        {
            if (held.Count == 0 || !gate.Open())
            {
                return;
            }
            var now = milliseconds();
            due = [.. held.Where(entry => now - entry.At <= HoldMilliseconds).Select(entry => entry.Line)];
            held.Clear();
        }
        foreach (var line in due)
        {
            Say(line);
        }
    }

    private void Say(string line) => announcer.Announce(new Announcement(line, Interrupt: false));
}
