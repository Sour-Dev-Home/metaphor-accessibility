using System.Buffers.Binary;

namespace Metaphor.Features.Menus;

/// <summary>The fields of a save file's slot header that match what the load list shows (#773): the in-game day count,
/// its weekday and time of day, and the playtime in seconds.</summary>
/// <remarks>On game build 548ddc95 (Steam build 18330018) each <c>save%04d.sav</c> starts with a 0x228-byte header:
/// <c>+0x00</c> format version 22, <c>+0x04</c> the slot number, <c>+0x08</c> the day count, <c>+0x0C</c> the weekday
/// (day count mod 5), <c>+0x10</c> the time-of-day index and <c>+0x54</c> the playtime in seconds, each matched against
/// the load list in docs/findings/save-slot-header-in-save-files.md. A header of another version or slot, or with a
/// field out of range, is not read, so another build's format says nothing rather than something wrong.</remarks>
public sealed record SaveSlotHeader(int DayCount, int Weekday, int TimeOfDay, int PlaytimeSeconds)
{
    /// <summary>How many bytes the header takes at the start of the file.</summary>
    public const int Size = 0x228;

    /// <summary>The format version the fields were matched on.</summary>
    public const int KnownVersion = 22;

    // The time of day's serials, System_General_Time_01 to _07, run 9297 to 9303: seven indexes.
    private const int TimesOfDay = 7;

    // The calendar's weekdays (GetCalendarDow is the day count mod 5).
    private const int Weekdays = 5;

    // Far past any playthrough (ten in-game years), so a damaged count can't overflow the date's arithmetic.
    private const int MaxDays = 365 * 10;

    /// <summary>The header at the start of <paramref name="file"/> when it is slot <paramref name="slot"/>'s in the
    /// known format, or null.</summary>
    public static SaveSlotHeader? Parse(ReadOnlySpan<byte> file, int slot)
    {
        if (file.Length < Size || Int(file, 0x00) != KnownVersion || Int(file, 0x04) != slot)
        {
            return null;
        }
        var day = Int(file, 0x08);
        var weekday = Int(file, 0x0C);
        var time = Int(file, 0x10);
        var playtime = Int(file, 0x54);
        return day is < 0 or > MaxDays || weekday != day % Weekdays || time is < 0 or >= TimesOfDay || playtime < 0
            ? null
            : new SaveSlotHeader(day, weekday, time, playtime);
    }

    /// <summary>The month, 1 to 12, as the calendar's <c>GetMonth</c> computes it from the day count
    /// (docs/findings/calendar-date-time-and-quests-static.md): day 0 is month 6, day 1, and every month has 30 days
    /// but the last, which has 35.</summary>
    public int Month => Math.Min(DayOfYear / 30 + 1, 12);

    /// <summary>The day of the month, as the calendar's <c>GetDay</c> computes it.</summary>
    public int Day => Month == 12 ? DayOfYear - 329 : DayOfYear % 30 + 1;

    /// <summary>The weekday's message label, <c>System_General_Week_01</c> to <c>_05</c>, which the load list fetches
    /// for the row by serial 9285 plus the weekday.</summary>
    public string WeekdayLabel => $"System_General_Week_{Weekday + 1:D2}";

    /// <summary>The time of day's message label, <c>System_General_Time_01</c> to <c>_07</c>, which the load list
    /// fetches for the row by serial 9297 plus the index.</summary>
    public string TimeOfDayLabel => $"System_General_Time_{TimeOfDay + 1:D2}";

    /// <summary>The date as words a screen reader says well: "month 10, day 5".</summary>
    public string DateWords => $"month {Month}, day {Day}";

    /// <summary>The playtime in hours and whole minutes, as the list shows it: "47 hours 18 minutes", "10 minutes",
    /// "less than a minute".</summary>
    public string PlaytimeWords
    {
        get
        {
            var hours = PlaytimeSeconds / 3600;
            var minutes = PlaytimeSeconds % 3600 / 60;
            var hourWords = hours == 0 ? null : hours == 1 ? "1 hour" : $"{hours} hours";
            var minuteWords = minutes == 0 ? null : minutes == 1 ? "1 minute" : $"{minutes} minutes";
            return (hourWords, minuteWords) switch
            {
                (null, null) => "less than a minute",
                (null, _) => minuteWords!,
                (_, null) => hourWords,
                _ => $"{hourWords} {minuteWords}",
            };
        }
    }

    private int DayOfYear => (DayCount + 150) % 365;

    private static int Int(ReadOnlySpan<byte> file, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(file[offset..]);
}
