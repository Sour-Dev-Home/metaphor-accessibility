using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the save and load screens' slots (#622): on entering a list and on each focus move, "Slot n", each
/// cutting off the last; the load list's first row is its autosave, said by the system file's word for it. With the
/// slot details on (#773), a slot whose save file's header reads also says its date and playtime, and the details key
/// (#934) reads its weekday, date, time of day and playtime. A list of another length is not the mapped one, so only its
/// position is said. Each focus also goes to the Debug log with what the slot doesn't say yet. Safe to call from several
/// threads.</summary>
/// <remarks>On build 548ddc95 the load list holds 17 rows and the save list 16, so the load list's extra first row is
/// taken for its autosave, and load row n is <c>save%04d.sav</c> n (docs/findings/save-slot-header-in-save-files.md);
/// that save row n is file n + 1 is <c>[NEEDS VERIFICATION by ear]</c>. The maintainer chose "Slot 3, place, playtime"
/// and "Slot 3, empty" (2026-11-22, #622); the place, the level and what an empty slot's file looks like are not in the
/// pinned header fields, so they are left out.</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.SaveSlots)]
public sealed class SaveSlotFocus : IScreenObserver, IScreenDetails
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
    private readonly ISaveSlotHeaders headers;
    private readonly Func<bool> detailsOn;
    private readonly Action<string>? debug;

    /// <summary>Speaks the slots, with each one's header from <paramref name="headers"/> while
    /// <paramref name="detailsOn"/> says the slot details are on.</summary>
    public SaveSlotFocus(
        IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner, ISaveSlotHeaders headers, Func<bool> detailsOn,
        Action<string>? debug = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(detailsOn);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.headers = headers;
        this.detailsOn = detailsOn;
        this.debug = debug;
    }

    /// <summary>Speaks the focused slot when a slot list is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (Slot(change) is not { } slot)
        {
            if (IsSlotList(change))
            {
                announcer.Announce(new Announcement(change.Position(), Interrupt: true));
            }
            return;
        }
        var said = SlotName(change.Index, slot.Load);
        var header = Header(slot.File);
        if (header is not null)
        {
            said = $"{said}, {header.DateWords}, {header.PlaytimeWords}";
        }
        announcer.Announce(new Announcement(said, Interrupt: true));
        debug?.Invoke(
            $"slot screen={(slot.Load ? "load" : "save")} index={change.Index} said=\"{said}\" "
            + $"unread=place,level{(header is null ? ",header" : "")} (#622)");
    }

    /// <summary>The focused slot's weekday, date, time of day and playtime (#934), or null on another screen, with the
    /// details off or when the slot's header can't be read.</summary>
    public string? Details(ScreenEvent focus)
    {
        ArgumentNullException.ThrowIfNull(focus);
        if (Slot(focus) is not { } slot || Header(slot.File) is not { } header)
        {
            return null;
        }
        string?[] parts =
        [
            Text(header.WeekdayLabel), header.DateWords, Text(header.TimeOfDayLabel), $"played {header.PlaytimeWords}",
        ];
        return string.Join(", ", parts.Where(part => part is not null));
    }

    private static bool IsSlotList(ScreenEvent change) =>
        (change.Screen == Load || change.Screen == Save) && change.Change != ScreenChange.Left && change.Index >= 0;

    // The focused row's save file, on a list of the mapped length.
    private static (bool Load, int File)? Slot(ScreenEvent change)
    {
        if (!IsSlotList(change))
        {
            return null;
        }
        var load = change.Screen == Load;
        return change.Total == (load ? LoadCount : SaveCount) ? (load, load ? change.Index : change.Index + 1) : null;
    }

    private SaveSlotHeader? Header(int file) => detailsOn() ? headers.Read(file) : null;

    private string? Text(string label) =>
        messages.Find(label) is { } message && cleaner.Clean(message.Text) is { Length: > 0 } text ? text : null;

    private string SlotName(int index, bool load)
    {
        if (!load)
        {
            return $"Slot {index + 1}";
        }
        return index > 0 ? $"Slot {index}" : Text(AutosaveLabel) ?? "Autosave";
    }
}
