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
    private readonly MessageFilesById files = new();
    private long now = 10_000;
    private readonly BattleBanners banners;

    public BattleBannersTests()
    {
        files.Observe(new MessageFileCompiled($"EN/message/system/{BattleBanners.SituationFile}", 10, """
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
    public void BannersOffSaysNothing()
    {
        var off = new BattleBanners(files, announcer, new TextCleaner(TextCleanerRules.None), () => now, () => false);

        Assert.Null(off.Shown(Situation, 0));
        Assert.Empty(Said());
    }
}
