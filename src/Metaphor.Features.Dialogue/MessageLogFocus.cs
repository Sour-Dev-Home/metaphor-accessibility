using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue;

/// <summary>Speaks the game's message log, the backlog of recent dialogue, as the player moves through it (#685): on
/// opening "Message log, line n of total", then "Line n of total" at each line the cursor settles on, each cutting off
/// the one before (ADR 0023). The log is the screen whose cursor's owner is <see cref="ScreenClass"/>; the hook layer
/// names it from the log's update (docs/findings/message-log-screen-static.md). The lines' words aren't read yet: where
/// the screen keeps them is <c>[NEEDS VERIFICATION]</c>, and a Debug build logs what it can for the next session. Safe
/// to call from several threads.</summary>
[FeatureSetting<DialogueFeature>(DialogueFeature.MessageLog)]
public sealed class MessageLogFocus : ICursorObserver
{
    /// <summary>The message log's class as its run-time type information names it, on game build 548ddc95… (Steam
    /// build 18330018): <c>.?AVUUIC_Common_MessageLog@ui@app@@</c>, docs/findings/message-log-screen-static.md.</summary>
    public const string ScreenClass = "UUIC_Common_MessageLog@ui@app";

    private readonly IAnnouncer announcer;
    private readonly Lock gate = new();
    private long? manager;
    private int? lastIndex;

    /// <param name="announcer">Where the log's lines are said.</param>
    public MessageLogFocus(IAnnouncer announcer)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        this.announcer = announcer;
    }

    /// <summary>Speaks where the log's cursor settled. A move is skipped, as the game can refuse it. An input equal to
    /// the last one is the log told again after it went uncalled (<see cref="CursorEvents.QuietMilliseconds"/>), so it
    /// was opened again, and says its name; any other screen's input closes it.</summary>
    public void Observe(CursorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.IsMove)
        {
            return;
        }
        bool opened;
        lock (gate)
        {
            if (input.ScreenClass != ScreenClass)
            {
                manager = null;
                lastIndex = null;
                return;
            }
            opened = manager != input.Manager || lastIndex == input.Index;
            manager = input.Manager;
            lastIndex = input.Index;
        }
        var position = input.Count is { } count && count > 0 ? $"line {input.Index + 1} of {count}" : $"line {input.Index + 1}";
        announcer.Announce(new Announcement(
            opened ? $"Message log, {position}" : char.ToUpperInvariant(position[0]) + position[1..], Interrupt: true));
    }
}
