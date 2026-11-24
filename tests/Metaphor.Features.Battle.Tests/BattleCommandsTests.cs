using System.Text;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Battle.Tests;

/// <summary>Escape (#256) and Pass (#261) as the recorded fights ran them, through the features that already speak
/// them: the situation banner at its position (Escape 149, Couldn't escape 30, Escape impossible 29, Pass 210 in the
/// situation file's 259 on Steam build 18330018), then the turn panel's escaped animation, or the turn icons' new count
/// after a pass. Positions and icon values from five Debug session logs of 2026-11-22 and 2026-11-23
/// (docs/findings/battle-escape-pass-retry-signals-logged.md); the banner text is made up.</summary>
[Trait("Category", "Unit")]
public sealed class BattleCommandsTests
{
    private const int Situation = BattleBanners.SituationKind;
    private const int Escape = 149;
    private const int EscapeFailed = 30;
    private const int EscapeImpossible = 29;
    private const int Pass = 210;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageFilePositions files = new([BattleBanners.Steam18330018]);
    private readonly ManualTime time = new();
    private readonly BattleBanners banners;

    public BattleCommandsTests()
    {
        var file = new StringBuilder();
        for (var i = 0; i < BattleBanners.Steam18330018.Count; i++)
        {
            file.Append(System.Globalization.CultureInfo.InvariantCulture, $"@test_{i}\n{{\nTest banner {i}\n<WAIT>\n}}\n\n");
        }
        files.Observe(new MessageFileCompiled($"EN/{BattleBanners.SituationFile}", 10, file.ToString()));
        banners = new BattleBanners(
            files, announcer, new TextCleaner(TextCleanerRules.None), () => time.GetUtcNow().ToUnixTimeMilliseconds());
    }

    private List<string> Said() => [.. announcer.Said.Select(said => said.Text)];

    [Fact]
    public void AnEscapeSaysTheCommandThenEscapedAsTheBattleEnds()
    {
        banners.Shown(Situation, Escape);
        time.Advance(700);
        new BattleEndLine(announcer).Played(new LayoutAnimation(BattleEndLine.Layout, BattleEndLine.EscapedAnimation));

        Assert.Equal(["Test banner 149", "Escaped"], Said());
    }

    [Fact]
    public void AFailedOrImpossibleEscapeSaysItsBanner()
    {
        banners.Shown(Situation, Escape);
        banners.Shown(Situation, EscapeFailed);
        time.Advance(5000);
        banners.Shown(Situation, EscapeImpossible);

        Assert.Equal(["Test banner 149", "Test banner 30", "Test banner 29"], Said());
    }

    [Fact]
    public void APassSaysItsBannerThenTheActionsLeft()
    {
        var icons = new Icons { Now = new TurnIcons(0, 4, 0) };
        var turn = new TurnLine(icons, new Scene(), announcer, time);
        Frames(turn, 300);

        // The log: the pass banner at icons 4,0, then a full icon turned half.
        banners.Shown(Situation, Pass);
        icons.Now = new TurnIcons(0, 3, 1);
        Frames(turn, TurnLine.SettleMilliseconds + 200);

        Assert.Equal(["Your turn, 4 actions", "Test banner 210", "3 actions, 1 half action"], Said());
    }

    private void Frames(TurnLine turn, long milliseconds)
    {
        for (var at = 0L; at < milliseconds; at += 50)
        {
            time.Advance(50);
            turn.Keys([]);
        }
    }

    private sealed class Icons : IBattleTurnIcons
    {
        public TurnIcons? Now { get; set; }

        public TurnIcons? Read() => Now;
    }

    private sealed class Scene : IBattleScene
    {
        public bool InBattle() => true;
    }
}
