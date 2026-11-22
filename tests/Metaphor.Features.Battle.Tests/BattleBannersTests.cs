using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Battle.Tests;

/// <summary>The in-turn situation banner (#692): a banner of the situation kind is said from the situation file at its
/// id's position, queued, once. A fake banner source in, announcements out; labels follow the game's, the text is made
/// up.</summary>
[Trait("Category", "Unit")]
public sealed class BattleBannersTests
{
    private const int Situation = BattleBanners.SituationKind;

    private readonly RecordingAnnouncer announcer = new();
    // The test file holds three messages, so the shape names three in place of the build's 259.
    private readonly MessageFilePositions files = new([new(BattleBanners.SituationFile, 3)]);
    private long now = 10_000;
    private readonly BattleBanners banners;

    public BattleBannersTests()
    {
        files.Observe(new MessageFileCompiled($"EN/{BattleBanners.SituationFile}", 10, """
            @jyokyo_helpText_A
            {
            Test attack rose!
            <WAIT>
            }

            @jyokyo_helpText_B
            {
            <V 0> is testing up!
            <WAIT>
            }

            @jyokyo_helpText_C
            {
            Not enough test!
            <WAIT>
            }
            """));
        banners = new BattleBanners(files, announcer, new TextCleaner(TextCleanerRules.None), () => now);
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void ASituationBannerIsSaidFromItsPositionInTheSituationFileQueued()
    {
        Assert.Equal("Test attack rose!", banners.Shown(Situation, 0));
        banners.Shown(Situation, 2);

        Assert.Equal(["Test attack rose!", "Not enough test!"], Said());
        Assert.All(announcer.Said, said => Assert.False(said.Interrupt));
    }

    [Fact]
    public void ANameTheModCannotFillIsLeftOut()
    {
        banners.Shown(Situation, 1);

        Assert.Equal(["is testing up!"], Said());
    }

    [Fact]
    public void TheSameBannerShownAgainSoonIsSaidOnce()
    {
        banners.Shown(Situation, 0);
        now += BattleBanners.RepeatMilliseconds - 1;
        Assert.Null(banners.Shown(Situation, 0));
        now += BattleBanners.RepeatMilliseconds;
        banners.Shown(Situation, 0);

        Assert.Equal(["Test attack rose!", "Test attack rose!"], Said());
    }

    [Fact]
    public void AnotherKindOrAnUnknownPositionSaysNothing()
    {
        Assert.Null(banners.Shown(3, 0));
        Assert.Null(banners.Shown(0, 0));
        Assert.Null(banners.Shown(Situation, 3));
        Assert.Null(banners.Shown(Situation, -1));

        Assert.Empty(Said());
    }

    [Fact]
    public void ASituationFileOfAnotherCountOrAnotherFolderSaysNothing()
    {
        // Another count is another build's file, whose positions can't be trusted (finding 4 of the 2026-11-22
        // review); a file of the same name in another folder isn't the situation file.
        const string ThreeMessages = "@A\n{\nOne\n}\n@B\n{\nTwo\n}\n@C\n{\nThree\n}";
        var otherCount = new MessageFilePositions([new(BattleBanners.SituationFile, 2)]);
        otherCount.Observe(new MessageFileCompiled($"EN/{BattleBanners.SituationFile}", 10, ThreeMessages));
        var otherFolder = new MessageFilePositions([new(BattleBanners.SituationFile, 3)]);
        otherFolder.Observe(new MessageFileCompiled("EN/message/event/jyokyo_help_Message.msg", 10, ThreeMessages));
        var clean = new TextCleaner(TextCleanerRules.None);

        Assert.Null(new BattleBanners(otherCount, announcer, clean, () => now).Shown(Situation, 0));
        Assert.Null(new BattleBanners(otherFolder, announcer, clean, () => now).Shown(Situation, 0));
        Assert.Empty(Said());
    }

    [Fact]
    public void TheSituationFileIsPinnedToItsPathAndCountOnTheBuild()
    {
        Assert.Equal(new MessageFileShape("message/system/jyokyo_help_Message.msg", 259), BattleBanners.Steam18330018);
    }

    [Fact]
    public void BannersOffSaysNothing()
    {
        var off = new BattleBanners(files, announcer, new TextCleaner(TextCleanerRules.None), () => now, () => false);

        Assert.Null(off.Shown(Situation, 0));
        Assert.Empty(Said());
    }
}
