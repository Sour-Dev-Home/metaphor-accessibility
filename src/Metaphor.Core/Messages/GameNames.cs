using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Metaphor.Core.Announcements;

namespace Metaphor.Core.Messages;

/// <summary>A name the player typed (#107).</summary>
public enum TypedName
{
    /// <summary>The name typed at the prompt asking who will guide the protagonist: <c>&lt;PLAYER_NAME&gt;</c>.</summary>
    Player,

    /// <summary>The protagonist's name: <c>&lt;HERO_NAME&gt;</c>.</summary>
    Hero,
}

/// <summary>The game memory names come from, which the hook layer reads (#81, #107). Every read gives null when it
/// fails or the game hasn't set it up yet.</summary>
public interface IGameNameMemory
{
    /// <summary>The <see cref="CharacterEntry.Size"/> bytes of <c>CharaID.bin</c>'s entry for
    /// <paramref name="character"/>, 0 to <see cref="CharacterEntry.Last"/>.</summary>
    byte[]? CharacterEntry(int character);

    /// <summary>The character number of <c>FollowerTable.bin</c>'s entry for <paramref name="follower"/>, 0 to
    /// <see cref="GameNames.LastFollower"/>.</summary>
    int? FollowerCharacter(int follower);

    /// <summary>Whether story flag <paramref name="flag"/> (0 to 0xfffe) is set.</summary>
    bool? StoryFlag(int flag);

    /// <summary>The <see cref="TypedNameText.Size"/> bytes of a typed name's field.</summary>
    byte[]? TypedName(TypedName name);
}

/// <summary>One entry of the game's character name table, <c>CharaID.bin</c>, read as the game reads it: a 16-byte
/// default key, then five slots of a 16-byte key and a 16-bit story flag number (<c>0xffff</c> for none); the first slot
/// whose flag is set wins, else the default. Each key is the MD5 of a <c>CharaName.msg</c> label. Game build 548ddc95…
/// (Steam build 18330018): the static note
/// (docs/findings/title-names-voice-tutorials-static.md#2-the-speaker-name-table-is-charaidbin-behind-a-static-pointer-74),
/// checked live on entry 51 in docs/findings/speaker-alias-from-chara-id-table.md.</summary>
public static class CharacterEntry
{
    /// <summary>An entry's size in bytes.</summary>
    public const int Size = 0x6c;

    /// <summary>The highest character number the game reads (docs/raw-sources/game-names-static-2026-11-20.md).</summary>
    public const int Last = 100;

    private const int KeySize = 16;
    private const int Slots = 5;
    private const int SlotSize = 0x12;
    private const ushort NoFlag = 0xffff;

    /// <summary>The key the game shows for this entry, given which story flags are set; null when the entry isn't
    /// <see cref="Size"/> bytes or a slot's flag can't be read, since the game might have picked that slot.</summary>
    public static byte[]? ChooseKey(ReadOnlySpan<byte> entry, Func<int, bool?> flagSet)
    {
        ArgumentNullException.ThrowIfNull(flagSet);
        if (entry.Length != Size)
        {
            return null;
        }
        for (var slot = 0; slot < Slots; slot++)
        {
            var at = KeySize + slot * SlotSize;
            var flag = BinaryPrimitives.ReadUInt16LittleEndian(entry[(at + KeySize)..]);
            if (flag == NoFlag)
            {
                continue;
            }
            switch (flagSet(flag))
            {
                case null:
                    return null;
                case true:
                    return entry.Slice(at, KeySize).ToArray();
            }
        }
        return entry[..KeySize].ToArray();
    }
}

/// <summary>A typed name's field as the game stores it: 17 little-endian UTF-32 units, ended by a zero. Game build
/// 548ddc95… (Steam build 18330018): the encoding from docs/findings/player-name-in-game-data.md, the size from the
/// new-game reset in docs/raw-sources/game-names-static-2026-11-20.md.</summary>
public static class TypedNameText
{
    /// <summary>The field's size in bytes.</summary>
    public const int Size = 0x44;

    /// <summary>The name up to its first zero unit, or null when it's empty or a unit isn't a Unicode scalar value, as a
    /// field that was never filled or was read from the wrong place would be.</summary>
    public static string? Decode(ReadOnlySpan<byte> field)
    {
        var text = new StringBuilder();
        for (var at = 0; at + 4 <= field.Length; at += 4)
        {
            var unit = BinaryPrimitives.ReadUInt32LittleEndian(field[at..]);
            if (unit == 0)
            {
                break;
            }
            if (unit > int.MaxValue || !Rune.IsValid((int)unit))
            {
                return null;
            }
            text.Append(new Rune((int)unit).ToString());
        }
        var name = text.ToString().Trim();
        return name.Length == 0 ? null : name;
    }
}

/// <summary>The names the screen shows for the game's name tags and speakers (#81, #107), read from the game when
/// asked, so a story flag or a rename applies at once. Each gives null when a read fails, and its caller falls back to
/// what it said before.</summary>
public sealed class GameNames
{
    /// <summary>The highest follower number read. The game doesn't bound it where read
    /// (docs/raw-sources/game-names-static-2026-11-20.md); the message log's tags go up to 15.</summary>
    public const int LastFollower = 63;

    private const string CharacterKey = "character-name";
    private const string FollowerKey = "follower-name";
    private const string PlayerKey = "player-name";
    private const string HeroKey = "hero-name";

    private static readonly TextCleaner Plain = new(TextCleanerRules.None);

    private readonly IGameNameMemory memory;
    private readonly IMessageLookup messages;

    public GameNames(IGameNameMemory memory, IMessageLookup messages)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(messages);
        this.memory = memory;
        this.messages = messages;
    }

    /// <summary>The name tags the game's text uses, as placeholders (docs/raw-sources/message-listening-run-2026-11-16.md;
    /// codes and meanings in docs/raw-sources/game-names-static-2026-11-20.md).</summary>
    public static TextCleanerRules Rules { get; } = new()
    {
        Placeholders = new Dictionary<string, string>
        {
            ["CHAR_NAME"] = CharacterKey,
            ["FOLLOWER_NAME"] = FollowerKey,
            ["PLAYER_NAME"] = PlayerKey,
            ["HERO_NAME"] = HeroKey,
        },
    };

    /// <summary>The name character <paramref name="character"/> goes by now, or null.</summary>
    public string? Character(int character)
    {
        if (character is < 0 or > CharacterEntry.Last || memory.CharacterEntry(character) is not { } entry ||
            CharacterEntry.ChooseKey(entry, memory.StoryFlag) is not { } key ||
            messages.FindByLabelHash(Convert.ToHexString(key)) is not { } message)
        {
            return null;
        }
        var name = Plain.Clean(message.Text);
        return name.Length == 0 ? null : name;
    }

    /// <summary>The name follower <paramref name="follower"/> goes by now, or null.</summary>
    public string? Follower(int follower) =>
        follower is >= 0 and <= LastFollower && memory.FollowerCharacter(follower) is { } character
            ? Character(character)
            : null;

    /// <summary>A typed name, or null.</summary>
    public string? Typed(TypedName name)
    {
        var typed = memory.TypedName(name) is { } field ? TypedNameText.Decode(field) : null;
        TypedNameRead?.Invoke(typed);
        return typed;
    }

    /// <summary>Told each typed name read, or null when none was: the log scrubber, so a name the player chose never
    /// reaches a log they share (#824).</summary>
    public Action<string?>? TypedNameRead { get; init; }

    /// <summary>A placeholder's value for <see cref="Rules"/>, from its key and its tag's arguments, or null.</summary>
    public string? Placeholder(string key, string arguments) => key switch
    {
        CharacterKey => Number(arguments) is { } n ? Character(n) : null,
        FollowerKey => Number(arguments) is { } n ? Follower(n) : null,
        PlayerKey => Typed(TypedName.Player),
        HeroKey => Typed(TypedName.Hero),
        _ => null,
    };

    /// <summary>The speaker's name as the screen shows it: from the character table when the message names a speaker,
    /// its <c>#SPEAKER_ID</c> is a character number and the read works, else the name the file stores. A message with
    /// no <c>&lt;NS&gt;</c> name keeps none, since its box may show none.</summary>
    public string? Speaker(GameMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return message.Speaker is not null && Number(message.SpeakerId ?? "") is { } character &&
            Character(character) is { } name
            ? name
            : message.Speaker;
    }

    // Plain decimal digits only: "N965" (an NPC's number, docs/raw-sources/game-names-static-2026-11-20.md) and
    // "FFFFFF" aren't character numbers.
    private static int? Number(string text) =>
        text.Length is > 0 and <= 5 && text.All(char.IsAsciiDigit) &&
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;
}

/// <summary>Messages with their speaker named as the screen shows it (#81): each found message's speaker comes from
/// <see cref="GameNames.Speaker"/>, read when the message is found.</summary>
public sealed class ShownSpeakers : IMessageLookup
{
    private readonly IMessageLookup messages;
    private readonly GameNames names;

    public ShownSpeakers(IMessageLookup messages, GameNames names)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(names);
        this.messages = messages;
        this.names = names;
    }

    public GameMessage? Find(string label) => Shown(messages.Find(label));

    public GameMessage? FindByLabelHash(string labelHash) => Shown(messages.FindByLabelHash(labelHash));

    private GameMessage? Shown(GameMessage? message) =>
        message is null ? null : message with { Speaker = names.Speaker(message) };
}
