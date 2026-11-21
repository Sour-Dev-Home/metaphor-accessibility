using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus;

/// <summary>Speaks a name entry screen (#585): its title when the player reaches it, then each character typed or
/// deleted, how to confirm when Enter is pressed, and the game's "name cannot be used" message. The title and the error are the game's own messages, looked up
/// at run time (<see cref="MessageLibrary"/>). Typed characters are the player's name: they go out as private
/// announcements, spoken but never recorded. Safe to call from several threads.</summary>
/// <remarks>On build 548ddc95 the screen's update shows <c>System_NameEntry_07</c> for the player's own name and
/// <c>System_NameEntry_01</c> for the protagonist's, and <c>System_NameEntry_Error</c> as a kind-1 window line when a
/// name is refused (docs/findings/name-entry-static.md). That the title is up when the first update runs, and that the
/// error reaches the window hook, are <c>[NEEDS VERIFICATION]</c> by ear.</remarks>
public sealed class NameEntrySpeech : INameEntryObserver, ILineShownObserver, IKeyWatcher
{
    /// <summary>What is said when Enter is pressed on the screen: the name is confirmed with Tab or the pad's Menu
    /// button, not Enter (the maintainer, by ear and by hand, on #593, 2026-11-21).</summary>
    public const string ConfirmHint = "Press Tab or the Menu button to confirm.";

    /// <summary>An Enter pressed this long after the screen's last update isn't on the screen.</summary>
    public const long OnScreenMilliseconds = 1000;

    // Windows' virtual-key code for Enter, which the game's keyboard read lists when held
    // (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md).
    private const int EnterKey = 0x0D;

    /// <summary>The player's own name entry title.</summary>
    public const string PlayerTitleLabel = "System_NameEntry_07";

    /// <summary>The protagonist's name entry title.</summary>
    public const string ProtagonistTitleLabel = "System_NameEntry_01";

    /// <summary>The message the game shows when it refuses a name.</summary>
    public const string ErrorLabel = "System_NameEntry_Error";

    /// <summary>What is said when the title's message isn't in any compiled file.</summary>
    public const string FallbackTitle = "Name entry";

    /// <summary>An update this long after the last one starts a new visit, so the title is spoken again.</summary>
    public const long VisitGapMilliseconds = 5000;

    // The error's request kind, the first argument of its window request (docs/findings/name-entry-static.md).
    private const int ErrorKind = 1;

    private readonly IMessageLookup messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly Func<long> milliseconds;
    private readonly Lock gate = new();
    private long? entry;
    private bool playerName;
    private long lastSeen;
    private string text = "";
    private bool enterHeld;

    public NameEntrySpeech(IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner, Func<long> milliseconds)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(milliseconds);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.milliseconds = milliseconds;
    }

    /// <summary>On a new visit, speaks the title, then the box's text if the game filled one in; afterwards speaks what
    /// changed in the box, if anything.</summary>
    public void Observe(NameEntryFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var now = milliseconds();
        List<Announcement> said = [];
        lock (gate)
        {
            var visit = entry != frame.Entry || playerName != frame.PlayerName || now - lastSeen >= VisitGapMilliseconds;
            lastSeen = now;
            if (visit)
            {
                entry = frame.Entry;
                playerName = frame.PlayerName;
                text = frame.Text;
                said.Add(new Announcement(Title(frame.PlayerName), Interrupt: true));
                if (frame.Text.Length > 0)
                {
                    said.Add(new Announcement(frame.Text, Interrupt: false, Private: true));
                }
            }
            else if (frame.Text != text)
            {
                var change = Change(text, frame.Text);
                text = frame.Text;
                if (change is not null)
                {
                    said.Add(new Announcement(change, Interrupt: true, Private: true));
                }
            }
        }
        foreach (var announcement in said)
        {
            announcer.Announce(announcement);
        }
    }

    /// <summary>Whether the screen updated within the last <see cref="OnScreenMilliseconds"/>, so the keys being read
    /// may be the player's name: the keyboard's dev log then gives a count instead of key codes.</summary>
    public bool OnScreen()
    {
        lock (gate)
        {
            return entry is not null && milliseconds() - lastSeen <= OnScreenMilliseconds;
        }
    }

    /// <summary>Says how to confirm the name when Enter goes down while the screen is up: once a press, nothing for a
    /// held key or away from the screen. The keys reach the game unchanged.</summary>
    public void Keys(IReadOnlyList<int> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var held = keys.Contains(EnterKey);
        bool pressed;
        lock (gate)
        {
            pressed = held && !enterHeld && entry is not null && milliseconds() - lastSeen <= OnScreenMilliseconds;
            enterHeld = held;
        }
        if (pressed)
        {
            announcer.Announce(new Announcement(ConfirmHint, Interrupt: true));
        }
    }

    /// <summary>Speaks the game's refusal of a name when its window line is shown.</summary>
    public void Observe(LineShown line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Kind == ErrorKind && messages.FindByLabelHash(line.LabelHash) is { Label: ErrorLabel } message &&
            Clean(message.Text) is { Length: > 0 } error)
        {
            announcer.Announce(new Announcement(error, Interrupt: true));
        }
    }

    /// <summary>What to say when the box goes from <paramref name="before"/> to <paramref name="after"/>: the characters
    /// added, or "deleted" and the characters removed, a lone space said as "space"; null when nothing changed. An edit
    /// in the middle is found from the common start and end.</summary>
    public static string? Change(string before, string after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var start = 0;
        while (start < before.Length && start < after.Length && before[start] == after[start])
        {
            start++;
        }
        var end = 0;
        while (end < before.Length - start && end < after.Length - start &&
               before[before.Length - 1 - end] == after[after.Length - 1 - end])
        {
            end++;
        }
        var added = after[start..(after.Length - end)];
        var removed = before[start..(before.Length - end)];
        if (added.Length > 0)
        {
            return Spoken(added);
        }
        return removed.Length > 0 ? "deleted " + Spoken(removed) : null;
    }

    private static string Spoken(string characters) => characters == " " ? "space" : characters;

    private string Title(bool player) =>
        Clean(messages.Find(player ? PlayerTitleLabel : ProtagonistTitleLabel)?.Text ?? "") is { Length: > 0 } title
            ? title
            : FallbackTitle;

    // A message's lines, cleaned of tags, empty ones dropped, joined by spaces.
    private string Clean(string message) =>
        string.Join(' ', message.Split('\n').Select(cleaner.Clean).Where(line => line.Length > 0));
}
