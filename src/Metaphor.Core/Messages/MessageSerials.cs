using Metaphor.Core.Announcements;

namespace Metaphor.Core.Messages;

/// <summary>A message file's serial numbers on one game build: its path from the <c>message</c> folder on, its first
/// serial and how many messages it holds.</summary>
public sealed record SerialRange(string File, int First, int Count);

/// <summary>Finds a game message by the serial number the game fetches it by.</summary>
public interface IMessageBySerial
{
    /// <summary>The message with this serial, or null when no known file holds it.</summary>
    GameMessage? FindBySerial(int serial);
}

/// <summary>The messages the game fetches by serial number (#584), from the message files it compiles. A file's
/// serials are its first serial plus each message's position in the file, and the game compiles its system files whole at
/// start-up, so their messages are known before any menu asks for one (docs/findings/message-serials-from-file-positions.md).
/// Only the files in its table are kept, and only when they hold the table's number of messages: another count means
/// another build's file, whose positions can't be trusted. Safe to use from several threads.</summary>
public sealed class MessageSerials : IMessageBySerial, IMessageFileObserver
{
    private const string MessageFolder = "message/";

    private static readonly TextCleaner Plain = new(TextCleanerRules.None);

    /// <summary>The files whose serials are known on game build 548ddc95… (Steam build 18330018): each first serial and
    /// count is the range test the game's message-file table holds for it, read from the exe, and matches the file as
    /// the game compiled it in play (docs/findings/message-serials-from-file-positions.md). The item and skill files'
    /// ranges are read from the same table (docs/findings/item-skill-message-serials-static.md); that the game compiles
    /// them with those counts is <c>[NEEDS VERIFICATION]</c>, and a file that doesn't is dropped. The equipment,
    /// character and archetype name files' ranges are read from the same table and match their compiled counts in a
    /// Debug log (docs/findings/equipment-character-archetype-serials-static.md), as do the quest files'
    /// (docs/findings/quest-message-serials-static.md). The class names' and battle commands' ranges are from their
    /// compiled counts and their neighbours' ranges (docs/findings/class-name-and-battle-item-tab-serials.md).</summary>
    public static IReadOnlyList<SerialRange> Steam18330018 { get; } =
    [
        new(CharacterNames, 0, 122),
        new(ClassNames, 1082, 60),
        new("message/system/System_Message.msg", 1142, 790),
        new(BattleCommandMessages, 1932, 159),
        new(EquipmentNames, 2442, 1205),
        new(EquipmentDescriptions, 3647, 1206),
        new(ItemNames, 5143, 773),
        new(ItemDescriptions, 5916, 833),
        new(SkillNames, 6749, 1086),
        new(SkillDescriptions, 7835, 1052),
        new(ArchetypeNames, 9441, 18),
        new(QuestNames, 9508, 136),
        new(QuestTexts, 9644, 457),
        new("message/system/System_Message_PC.msg", 11453, 354),
    ];

    /// <summary>The character names file: the Item and Equipment screens fetch party members' names from it (#216,
    /// #217).</summary>
    public const string CharacterNames = "message/CharaName.msg";

    /// <summary>The weapon, armour, gear and accessory names file (#216, #217, #736).</summary>
    public const string EquipmentNames = "message/system/Equip_Message_1.msg";

    /// <summary>The equipment descriptions file.</summary>
    public const string EquipmentDescriptions = "message/system/Equip_Message_2.msg";

    /// <summary>The archetype names file, one name per archetype line (Seeker, Mage, ...): the Equipment screen fetches
    /// each character's from it (#217).</summary>
    public const string ArchetypeNames = "message/ATCategoryName.msg";

    /// <summary>The specific archetypes file, <c>ClassName_&lt;line&gt;_&lt;rank&gt;</c> under each archetype line (Soul
    /// Hacker under Seeker): the Equipment list and the Archetype Tree fetch the current or focused one from it (#857,
    /// #220). Its range is from a Debug log, not the exe, whose range test for it is in <c>.text1</c>
    /// (docs/findings/class-name-and-battle-item-tab-serials.md).</summary>
    public const string ClassNames = "message/ClassName.msg";

    /// <summary>Battle's command texts, the item list's tab names among them (#851); range from a Debug log
    /// (docs/findings/class-name-and-battle-item-tab-serials.md).</summary>
    public const string BattleCommandMessages = "message/system/CommandBattle_Message.msg";

    /// <summary>The quest names file, <c>QuestName_None</c> then one name per quest (#194): the quest list fetches its
    /// rows' names from it (docs/findings/quest-message-serials-static.md).</summary>
    public const string QuestNames = "message/quest/Quest_1.msg";

    /// <summary>The quest texts file, a few per quest (<c>QuestInfo_&lt;quest&gt;_&lt;n&gt;</c>): an opened quest
    /// fetches its description from it (#194).</summary>
    public const string QuestTexts = "message/quest/Quest_2.msg";

    /// <summary>The item names file: battle's item list resolves its rows' names from it (#247).</summary>
    public const string ItemNames = "message/system/Item_Message_1.msg";

    /// <summary>The item descriptions file.</summary>
    public const string ItemDescriptions = "message/system/Item_Message_2.msg";

    /// <summary>The skill names file: battle's skill list resolves its rows' names from it (#247).</summary>
    public const string SkillNames = "message/system/Skill_Message_1.msg";

    /// <summary>The skill descriptions file.</summary>
    public const string SkillDescriptions = "message/system/Skill_Message_2.msg";

    /// <summary>Whether <paramref name="serial"/> is in the range of the table's file named <paramref name="file"/>,
    /// whether or not the file was kept.</summary>
    public bool InFile(int serial, string file)
    {
        foreach (var range in ranges)
        {
            if (string.Equals(range.File, file, StringComparison.OrdinalIgnoreCase))
            {
                return serial >= range.First && serial < range.First + range.Count;
            }
        }
        return false;
    }

    private readonly SerialRange[] ranges;
    private readonly GameMessage[]?[] files;
    private readonly Lock gate = new();

    public MessageSerials(IEnumerable<SerialRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        this.ranges = [.. ranges];
        files = new GameMessage[]?[this.ranges.Length];
    }

    /// <summary>Keeps a compiled file's messages in order when its table entry names it; a file compiled again replaces
    /// the one before, and one with another count drops it.</summary>
    public void Observe(MessageFileCompiled file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var name = FromMessageFolder(file.Path);
        var index = Array.FindIndex(ranges, range => string.Equals(range.File, name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }
        var messages = MessageLibrary.Parse(file.Source);
        lock (gate)
        {
            files[index] = messages.Count == ranges[index].Count ? [.. messages] : null;
        }
    }

    /// <summary>The message with this serial, or null when no file kept holds it.</summary>
    public GameMessage? FindBySerial(int serial)
    {
        lock (gate)
        {
            for (var i = 0; i < ranges.Length; i++)
            {
                var position = serial - ranges[i].First;
                if (files[i] is { } messages && position >= 0 && position < messages.Length)
                {
                    return messages[position];
                }
            }
        }
        return null;
    }

    /// <summary>The message's text as <paramref name="cleaner"/> says it, or null when it is unknown or empty.</summary>
    public string? TextOf(int serial, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(cleaner);
        return FindBySerial(serial) is { } message && cleaner.Clean(message.Text) is { Length: > 0 } text ? text : null;
    }

    /// <summary>For the dev log (#584): the message's label and its text with its tags dropped, or that it is unknown.
    /// </summary>
    public string Describe(int serial) =>
        FindBySerial(serial) is { } message
            ? $"label={message.Label} text=\"{LogText.Escaped(Plain.Clean(message.Text))}\""
            : "label=(unknown)";

    /// <summary>A compiled file's path from <c>message/</c> on, such as <c>message/system/X.msg</c> from
    /// <c>EN/message/system/X.msg</c>: the language folder before it differs by the game's language.</summary>
    public static string FromMessageFolder(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var slashed = path.Replace('\\', '/');
        if (slashed.StartsWith(MessageFolder, StringComparison.OrdinalIgnoreCase))
        {
            return slashed;
        }
        var at = slashed.IndexOf("/" + MessageFolder, StringComparison.OrdinalIgnoreCase);
        return at < 0 ? slashed : slashed[(at + 1)..];
    }
}
