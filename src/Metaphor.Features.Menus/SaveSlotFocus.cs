using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the save and load screens' slots (#622): on entering a list and on each focus move, "Slot n", each
/// cutting off the last; the load list's first row is its autosave, said by the system file's word for it. A list of
/// another length is not the mapped one, so only its position is said. Each focus also goes to the Debug log with what
/// the slot doesn't say yet. Safe to call from several threads.</summary>
/// <remarks>On build 548ddc95 the load list holds 17 rows and the save list 16, so the load list's extra first row is
/// taken for its autosave [NEEDS VERIFICATION by ear]. The maintainer chose "Slot 3, place, playtime" and "Slot 3,
/// empty" (2026-11-22, #622); the place, the playtime and whether a slot is empty aren't readable from the logs yet, and
/// the weekday and time-of-day serials the list fetches belong to the rows in view, not the focus
/// (docs/findings/save-load-slot-screens.md).</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.SaveSlots)]
public sealed class SaveSlotFocus : IScreenObserver
{
    /// <summary>The load screen's slot list, named by <see cref="SlotScreens"/>.</summary>
    public static Screen Load { get; } = new("Load screen", SlotScreens.LoadClass);

    /// <summary>The save screen's slot list, named by <see cref="SlotScreens"/>.</summary>
    public static Screen Save { get; } = new("Save screen", SlotScreens.SaveClass);

    private const int LoadCount = 17;
    private const int SaveCount = 16;
    private const string AutosaveLabel = "System_Save_11";

    private readonly IMessageLookup messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly Action<string>? debug;

    public SaveSlotFocus(IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner, Action<string>? debug = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.debug = debug;
    }

    /// <summary>Speaks the focused slot when a slot list is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var load = change.Screen == Load;
        if ((!load && change.Screen != Save) || change.Change == ScreenChange.Left || change.Index < 0)
        {
            return;
        }
        var said = change.Total == (load ? LoadCount : SaveCount)
            ? SlotName(change.Index, load)
            : change.Total is { } total ? $"{change.Index + 1} of {total}" : $"{change.Index + 1}";
        announcer.Announce(new Announcement(said, Interrupt: true));
        debug?.Invoke(
            $"slot screen={(load ? "load" : "save")} index={change.Index} said=\"{said}\" unread=place,playtime,empty (#622)");
    }

    private string SlotName(int index, bool load)
    {
        if (!load)
        {
            return $"Slot {index + 1}";
        }
        if (index > 0)
        {
            return $"Slot {index}";
        }
        return messages.Find(AutosaveLabel) is { } message && cleaner.Clean(message.Text) is { Length: > 0 } text
            ? text
            : "Autosave";
    }
}
