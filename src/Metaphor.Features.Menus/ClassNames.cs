using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus;

/// <summary>The specific archetypes in <see cref="MessageSerials.ClassNames"/> (#857, #220): each label is
/// <c>ClassName_&lt;line&gt;_&lt;rank&gt;</c>, and <c>&lt;line&gt;</c> is the number of the
/// <see cref="MessageSerials.ArchetypeNames"/> label (<c>ATCategoryName_&lt;line&gt;</c>) it belongs to, on build 548ddc95
/// (docs/findings/class-name-and-battle-item-tab-serials.md). An unused slot's text is its own label, never said.</summary>
internal static class ClassNames
{
    private const string ClassPrefix = "ClassName_";
    private const string LinePrefix = "ATCategoryName_";

    /// <summary>The line and rank a class name's label gives, or null for any other label.</summary>
    public static (int Line, int Rank)? Place(MessageSerials serials, int serial)
    {
        if (!serials.InFile(serial, MessageSerials.ClassNames) || serials.FindBySerial(serial) is not { } message
            || !message.Label.StartsWith(ClassPrefix, StringComparison.Ordinal))
        {
            return null;
        }
        var parts = message.Label[ClassPrefix.Length..].Split('_');
        return parts.Length == 2 && Number(parts[0]) is { } line && Number(parts[1]) is { } rank ? (line, rank) : null;
    }

    /// <summary>The line an archetype line's label gives, or null for any other label.</summary>
    public static int? Line(MessageSerials serials, int serial) =>
        serials.InFile(serial, MessageSerials.ArchetypeNames) && serials.FindBySerial(serial) is { } message
            && message.Label.StartsWith(LinePrefix, StringComparison.Ordinal)
            ? Number(message.Label[LinePrefix.Length..])
            : null;

    /// <summary>The class's text, or null when it is unknown or an unused slot's.</summary>
    public static string? Text(MessageSerials serials, int serial, TextCleaner cleaner) =>
        serials.TextOf(serial, cleaner) is { } text && !text.StartsWith('@') ? text : null;

    /// <summary>The name to say for a character's archetype: the class when it is in <paramref name="line"/>'s line,
    /// else the line's own name; null when neither is known.</summary>
    public static string? Current(MessageSerials serials, int? classSerial, int? line, TextCleaner cleaner)
    {
        if (classSerial is { } known && line is { } lineSerial && Place(serials, known) is { } place
            && Line(serials, lineSerial) == place.Line && Text(serials, known, cleaner) is { } name)
        {
            return name;
        }
        return line is { } fallback ? serials.TextOf(fallback, cleaner) : null;
    }

    private static int? Number(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
}
