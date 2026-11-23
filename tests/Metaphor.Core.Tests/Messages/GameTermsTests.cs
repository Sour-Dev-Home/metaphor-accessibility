using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Logging;
using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests.Messages;

/// <summary>Game-term and icon tags (#108, #109) under #83's rule: a found value replaces the tag, a missing one says a
/// stand-in for its kind and is reported. Which file and position a tag's number names is
/// docs/findings/placeholder-tags-name-file-positions.md. File names follow the game's; the text is made up.</summary>
[Trait("Category", "Unit")]
public sealed class GameTermsTests
{
    private const string SkillFile = "EN/message/system/Skill_Message_1.msg";
    private const string ConditionFile = "EN/message/system/BadStatus_Message_1.msg";

    private const string Skills = """
        @SkillName_blank
        {
        blank
        <WAIT>
        }

        @SkillName_Fire_01
        {
        Ember<COLOR -1>
        <WAIT>
        }
        """;

    private const string Conditions = """
        @BadStatus_Name_01
        {
        Soot
        <WAIT>
        }
        """;

    private static readonly IReadOnlyList<MessageFileShape> Shapes =
    [
        new("message/system/Skill_Message_1.msg", 2),
        new("message/system/BadStatus_Message_1.msg", 1),
    ];

    private static (TextCleaner Cleaner, List<string> Missing) Cleaner(bool compiled = true)
    {
        var files = new MessageFilePositions(Shapes);
        if (compiled)
        {
            files.Observe(new MessageFileCompiled(SkillFile, 17, Skills));
            files.Observe(new MessageFileCompiled(ConditionFile, 21, Conditions));
        }
        var missing = new List<string>();
        var terms = new GameTerms(files, (tag, args) => missing.Add($"{tag} {args}"));
        return (new TextCleaner(GameTerms.Rules, terms.Placeholder), missing);
    }

    [Fact]
    public void ASkillNameTagSaysTheSkillAtThatPositionOfTheSkillFile()
    {
        var (cleaner, missing) = Cleaner();

        Assert.Equal("Required Skill: Ember", cleaner.Clean("Required Skill: <COLOR 4><SKILL 1 2><COLOR -1>"));
        Assert.Empty(missing);
    }

    [Fact]
    public void AConditionTagSaysTheConditionAtThatPosition()
    {
        var (cleaner, _) = Cleaner();

        Assert.Equal("Soot: HP falls.", cleaner.Clean("<CONDITION 0 2>: HP falls."));
    }

    [Fact]
    public void AnIconFormSaysNothingSinceTheScreenWritesItsWordAfterIt()
    {
        var (cleaner, missing) = Cleaner();

        Assert.Equal("[ Strike]", cleaner.Clean("[<SKILL 1 1> Strike]"));
        Assert.Equal("Lip Rouge", cleaner.Clean("<ITEM 16497 1>Lip Rouge"));
        Assert.Empty(missing);
    }

    [Theory]
    [InlineData("Got <ITEM 20637 2> x1", "Got item x1", "ITEM 20637 2")]
    [InlineData("the <CLASS 27 2> Archetype", "the class Archetype", "CLASS 27 2")]
    [InlineData("<ATTRIBUTE 65535 1> damage", "attribute damage", "ATTRIBUTE 65535 1")]
    [InlineData("an action in <V 0>. Overwrite?", "an action in value. Overwrite?", "V 0")]
    [InlineData("Press <PAD_ICON 13> to attack", "Press button to attack", "PAD_ICON 13")]
    [InlineData("<SHOP_ICON 2>Weapons", "shop Weapons", "SHOP_ICON 2")]
    [InlineData("<SELECT_ICON 3>", "icon", "SELECT_ICON 3")]
    [InlineData("<FOLLOWER_ICON 1>", "follower", "FOLLOWER_ICON 1")]
    [InlineData("<MONEY_ICON>500", "money 500", "MONEY_ICON ")]
    [InlineData("<WEATHER_ICON 9>", "weather", "WEATHER_ICON 9")]
    public void AnUnknownValueSaysItsKindAndIsReported(string text, string spoken, string reported)
    {
        var (cleaner, missing) = Cleaner();

        Assert.Equal(spoken, cleaner.Clean(text));
        Assert.Equal([reported], missing);
    }

    [Fact]
    public void EachUnmappedIconOrTagGoesToTheUnknownListOnceASession()
    {
        var unknown = new List<string>();
        var files = new MessageFilePositions(Shapes);
        var terms = new GameTerms(files, unknowns: new UnknownValues(unknown.Add));
        var cleaner = new TextCleaner(GameTerms.Rules, terms.Placeholder);

        cleaner.Clean("Press <PAD_ICON 80> or <PAD_ICON 80>, then <PAD_ICON 81> for <ITEM 20637 2>");
        cleaner.Clean("<PAD_ICON 80> again");

        Assert.Equal(["icon PAD_ICON 80", "icon PAD_ICON 81", "tag ITEM 20637 2"], unknown);
    }

    [Theory]
    [InlineData("<SKILL 2 2>", "skill")]
    [InlineData("<SKILL 1 0>", "skill")]
    [InlineData("<SKILL x 2>", "skill")]
    [InlineData("<CONDITION 5 2>", "condition")]
    public void ASkillOrConditionOutsideItsFileOrInAnotherFormSaysItsKind(string text, string spoken)
    {
        var (cleaner, missing) = Cleaner();

        Assert.Equal(spoken, cleaner.Clean(text));
        Assert.Single(missing);
    }

    [Fact]
    public void BeforeItsFileCompilesASkillSaysItsKind()
    {
        var (cleaner, _) = Cleaner(compiled: false);

        Assert.Equal("skill", cleaner.Clean("<SKILL 1 2>"));
    }

    [Fact]
    public void AFileWithAnotherCountIsNotTrusted()
    {
        var files = new MessageFilePositions(Shapes);
        files.Observe(new MessageFileCompiled(ConditionFile, 21, Conditions + "\n" + Conditions));

        Assert.Null(files.Find("message/system/BadStatus_Message_1.msg", 0));
    }

    [Fact]
    public void WithNoReportAMissingValueStillSaysItsKind()
    {
        var terms = new GameTerms(new MessageFilePositions(Shapes));

        Assert.Equal("item", new TextCleaner(GameTerms.Rules, terms.Placeholder).Clean("<ITEM 1 2>"));
    }

    // docs/raw-sources/pad-icon-buttons-by-ear-2026-11-22.md: 6 LT, 7 RT, 12 B on an Xbox-glyph controller.
    [Theory]
    [InlineData("Hold <PAD_ICON 7> to dash", "Hold RT to dash")]
    [InlineData("Press <PAD_ICON 12> to dodge", "Press B to dodge")]
    [InlineData("<PAD_ICON 6>Fae Sight", "LT Fae Sight")]
    public void OnAControllerAPinnedButtonIconSaysTheButtonTheScreenDraws(string text, string spoken)
    {
        var missing = new List<string>();
        var terms = new GameTerms(
            new MessageFilePositions(Shapes), (tag, args) => missing.Add($"{tag} {args}"), buttons: () => InputDevice.Controller);

        Assert.Equal(spoken, new TextCleaner(GameTerms.Rules, terms.Placeholder).Clean(text));
        Assert.Empty(missing);
    }

    [Fact]
    public void OnAControllerAnUnpinnedButtonIconStillSaysButtonAndIsReported()
    {
        var missing = new List<string>();
        var terms = new GameTerms(
            new MessageFilePositions(Shapes), (tag, args) => missing.Add($"{tag} {args}"), buttons: () => InputDevice.Controller);

        Assert.Equal("Press button to attack", new TextCleaner(GameTerms.Rules, terms.Placeholder).Clean("Press <PAD_ICON 13> to attack"));
        Assert.Equal(["PAD_ICON 13"], missing);
    }

    [Fact]
    public void OnTheKeyboardAButtonIconSaysKeySinceTheKeyIsThePlayersBinding()
    {
        var missing = new List<string>();
        var terms = new GameTerms(
            new MessageFilePositions(Shapes), (tag, args) => missing.Add($"{tag} {args}"), buttons: () => InputDevice.Keyboard);

        Assert.Equal("Press key to dodge", new TextCleaner(GameTerms.Rules, terms.Placeholder).Clean("Press <PAD_ICON 12> to dodge"));
        Assert.Equal(["PAD_ICON 12"], missing);
    }

    [Fact]
    public void WithButtonNamesOffAButtonIconSaysButtonAsBefore()
    {
        var terms = new GameTerms(new MessageFilePositions(Shapes), buttons: () => null);

        Assert.Equal("Hold button to dash", new TextCleaner(GameTerms.Rules, terms.Placeholder).Clean("Hold <PAD_ICON 7> to dash"));
    }

    [Fact]
    public void TheRulesCombineWithTheNameTags()
    {
        var rules = GameNames.Rules.With(GameTerms.Rules);

        Assert.Equal("character-name", rules.Placeholders["CHAR_NAME"]);
        Assert.Equal(GameTerms.Rules.Placeholders["ITEM"], rules.Placeholders["ITEM"]);
    }
}
