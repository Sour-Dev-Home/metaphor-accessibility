using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests.Messages;

/// <summary>Speakers and name tags named as the screen shows them (#81, #107), from a made-up character table, flags
/// and names in the layout of docs/findings/speaker-alias-from-chara-id-table.md and
/// docs/findings/player-name-in-game-data.md. Labels and names are made up.</summary>
[Trait("Category", "Unit")]
public sealed class GameNamesTests
{
    private const string Names = """
        @Name_Ada
        {
        Ada
        <WAIT>
        }

        @Name_Ada_00
        {
        Masked Woman
        <WAIT>
        }

        @Name_Ada_01
        {
        Old Friend
        <WAIT>
        }

        #SPEAKER_ID "51"
        @MES_line
        {
        <NS>Ada<NE>
        Who's there?
        }

        #SPEAKER_ID "51"
        @MES_aside
        {
        Nobody answers.
        }

        #SPEAKER_ID "N965"
        @MES_npc
        {
        <NS>Guard<NE>
        Halt.
        }
        """;

    private sealed class Memory : IGameNameMemory
    {
        public Dictionary<int, byte[]> Characters { get; } = [];
        public Dictionary<int, int> Followers { get; } = [];
        public HashSet<int> SetFlags { get; } = [];
        public HashSet<int> UnreadableFlags { get; } = [];
        public Dictionary<TypedName, byte[]> Typed { get; } = [];

        public byte[]? CharacterEntry(int character) => Characters.GetValueOrDefault(character);

        public int? FollowerCharacter(int follower) => Followers.TryGetValue(follower, out var c) ? c : null;

        public bool? StoryFlag(int flag) => UnreadableFlags.Contains(flag) ? null : SetFlags.Contains(flag);

        public byte[]? TypedName(TypedName name) => Typed.GetValueOrDefault(name);
    }

    // MD5 is the game's key for a label, not a security measure.
#pragma warning disable CA5351
    private static byte[] Key(string label) => MD5.HashData(Encoding.UTF8.GetBytes(label));
#pragma warning restore CA5351

    /// <summary>An entry with a default label and up to five slots of a label and a flag.</summary>
    private static byte[] Entry(string defaultLabel, params (string Label, int Flag)[] slots)
    {
        var entry = new byte[CharacterEntry.Size];
        Key(defaultLabel).CopyTo(entry, 0);
        for (var i = 0; i < 5; i++)
        {
            var at = 0x10 + i * 0x12;
            var (label, flag) = i < slots.Length ? slots[i] : ("", 0xffff);
            Key(label).CopyTo(entry, at);
            BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(at + 0x10), (ushort)flag);
        }
        return entry;
    }

    private static byte[] Utf32(string text)
    {
        var field = new byte[TypedNameText.Size];
        Encoding.UTF32.GetBytes(text).CopyTo(field, 0);
        return field;
    }

    private static (GameNames Names, Memory Memory, MessageLibrary Library) Setup()
    {
        var library = new MessageLibrary();
        library.Compiled(Names);
        var memory = new Memory();
        memory.Characters[51] = Entry("Name_Ada", ("Name_Ada_00", 0x978), ("Name_Ada_01", 0x979));
        return (new GameNames(memory, library), memory, library);
    }

    [Fact]
    public void TheFirstSlotWhoseFlagIsSetWins()
    {
        var (names, memory, _) = Setup();
        memory.SetFlags.Add(0x978);
        memory.SetFlags.Add(0x979);

        Assert.Equal("Masked Woman", names.Character(51));
    }

    [Fact]
    public void ALaterSlotWinsWhenOnlyItsFlagIsSet()
    {
        var (names, memory, _) = Setup();
        memory.SetFlags.Add(0x979);

        Assert.Equal("Old Friend", names.Character(51));
    }

    [Fact]
    public void WithNoSlotFlagSetTheDefaultWins()
    {
        var (names, _, _) = Setup();

        Assert.Equal("Ada", names.Character(51));
    }

    [Fact]
    public void ASlotWithoutAFlagIsSkippedEvenIfFlagFfffWereSet()
    {
        var entry = Entry("Name_Ada", ("Name_Ada_00", 0xffff));

        Assert.Equal(Key("Name_Ada"), CharacterEntry.ChooseKey(entry, _ => true));
    }

    [Fact]
    public void AnUnreadableFlagGivesNoKey()
    {
        var entry = Entry("Name_Ada", ("Name_Ada_00", 0x978));

        Assert.Null(CharacterEntry.ChooseKey(entry, _ => null));
    }

    [Fact]
    public void AnEntryOfTheWrongSizeGivesNoKey() =>
        Assert.Null(CharacterEntry.ChooseKey(new byte[CharacterEntry.Size - 1], _ => false));

    [Fact]
    public void TheSpeakerIsTheAliasTheFlagsPick()
    {
        var (names, memory, library) = Setup();
        memory.SetFlags.Add(0x978);

        Assert.Equal("Masked Woman", new ShownSpeakers(library, names).Find("MES_line")?.Speaker);
    }

    [Fact]
    public void ALineFoundByItsLabelsMd5GetsTheShownSpeaker()
    {
        var (names, memory, library) = Setup();
        memory.SetFlags.Add(0x978);

        var line = new ShownSpeakers(library, names).FindByLabelHash(MessageLibrary.LabelHash("MES_line"));

        Assert.Equal("Masked Woman", line?.Speaker);
    }

    [Fact]
    public void AHashNoLabelHasFindsNoShownLine()
    {
        var (names, _, library) = Setup();

        Assert.Null(new ShownSpeakers(library, names).FindByLabelHash(MessageLibrary.LabelHash("MES_none")));
    }

    [Fact]
    public void TheSpeakerFallsBackToTheFilesNameWhenTheTableCantBeRead()
    {
        var (names, memory, library) = Setup();
        memory.Characters.Clear();

        Assert.Equal("Ada", new ShownSpeakers(library, names).Find("MES_line")?.Speaker);
    }

    [Fact]
    public void TheSpeakerFallsBackToTheFilesNameWhenAFlagCantBeRead()
    {
        var (names, memory, library) = Setup();
        memory.UnreadableFlags.Add(0x978);

        Assert.Equal("Ada", names.Speaker(library.Find("MES_line")!));
    }

    [Fact]
    public void ASpeakerIdThatIsntACharacterNumberKeepsTheFilesName()
    {
        var (names, _, library) = Setup();

        Assert.Equal("Guard", names.Speaker(library.Find("MES_npc")!));
    }

    [Fact]
    public void AMessageWithoutANameGetsNoneFromTheTable()
    {
        var (names, memory, library) = Setup();
        memory.SetFlags.Add(0x978);

        Assert.Null(names.Speaker(library.Find("MES_aside")!));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(65535)]
    public void ACharacterOutsideTheTableHasNoName(int character)
    {
        var (names, memory, _) = Setup();
        memory.Characters[character] = memory.Characters[51];

        Assert.Null(names.Character(character));
    }

    [Fact]
    public void ANameIsReadAsUtf32UpToItsZero()
    {
        var field = Utf32("Kit");
        Encoding.UTF32.GetBytes("Zed").CopyTo(field, 16);

        Assert.Equal("Kit", TypedNameText.Decode(field));
    }

    [Fact]
    public void ANameOutsideTheBasicPlaneIsRead() =>
        Assert.Equal("Ki\U0001F600", TypedNameText.Decode(Utf32("Ki\U0001F600")));

    [Theory]
    [InlineData(0x0000D800u)]
    [InlineData(0x00110000u)]
    [InlineData(0xFFFFFFFFu)]
    public void AFieldWithAUnitThatIsntAScalarValueHasNoName(uint unit)
    {
        var field = Utf32("Kit");
        BinaryPrimitives.WriteUInt32LittleEndian(field.AsSpan(4), unit);

        Assert.Null(TypedNameText.Decode(field));
    }

    [Fact]
    public void AnEmptyFieldHasNoName() => Assert.Null(TypedNameText.Decode(new byte[TypedNameText.Size]));

    [Fact]
    public void NameTagsBecomeTheNamesTheScreenShows()
    {
        var (names, memory, _) = Setup();
        memory.SetFlags.Add(0x978);
        memory.Followers[4] = 51;
        memory.Typed[TypedName.Player] = Utf32("Kit");
        memory.Typed[TypedName.Hero] = Utf32("Rowan");
        var cleaner = new TextCleaner(GameNames.Rules, names.Placeholder);

        Assert.Equal(
            "Kit, Rowan, Masked Woman and Masked Woman.",
            cleaner.Clean("<PLAYER_NAME>, <HERO_NAME>, <CHAR_NAME 51> and <FOLLOWER_NAME 4>."));
    }

    [Fact]
    public void ANameTagWhoseReadFailsIsDroppedAsBefore()
    {
        var (names, _, _) = Setup();
        var cleaner = new TextCleaner(GameNames.Rules, names.Placeholder);

        Assert.Equal(
            "'s Strength rose. Thanks, .",
            cleaner.Clean("<CHAR_NAME 65535>'s Strength rose. Thanks, <PLAYER_NAME>.<FOLLOWER_NAME 9>"));
    }

    [Fact]
    public void WithStandInsANameTagWhoseReadFailsSaysNameAndIsReported()
    {
        var (names, _, _) = Setup();
        var missing = new List<string>();
        var cleaner = new TextCleaner(
            GameNames.Rules, (key, args) => names.PlaceholderOrStandIn(key, args, (tag, a) => missing.Add($"{tag} {a}".TrimEnd())));

        Assert.Equal(
            "name's Strength rose. Thanks, name.",
            cleaner.Clean("<CHAR_NAME 65535>'s Strength rose. Thanks, <HERO_NAME>."));
        Assert.Equal(["CHAR_NAME 65535", "HERO_NAME"], missing);
    }

    [Fact]
    public void WithStandInsANameThatIsReadIsSaidAndNotReported()
    {
        var (names, memory, _) = Setup();
        memory.Typed[TypedName.Hero] = Utf32("Rowan");
        var missing = new List<string>();

        Assert.Equal("Rowan", names.PlaceholderOrStandIn("hero-name", "", (tag, a) => missing.Add(tag)));
        Assert.Null(names.PlaceholderOrStandIn("item", "1 2", (tag, a) => missing.Add(tag)));
        Assert.Empty(missing);
    }
}
