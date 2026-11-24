using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Battle.Tests;

/// <summary>The Party screen's reserve list (#922), as the 2026-11-24 log ran: each landing's own name fetched about
/// 75 ms after it, and a move fetching the row it leaves in its own millisecond, before the landing. Made-up names.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ReserveScreenFocusTests
{
    private const int NameFirst = 500;
    private const string ReserveClass = "BTL_OrderPartyReserve@ope@ui@app";
    private static readonly string[] Names = ["Ally One", "Ally Two", "Ally Three"];

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageSerials serials = new([new SerialRange(MessageSerials.CharacterNames, NameFirst, 3)]);
    private readonly ReserveScreenFocus focus;
    private long now = 10_000;

    public ReserveScreenFocusTests()
    {
        serials.Observe(new MessageFileCompiled("EN/message/CharaName.msg", 1, string.Join("\n\n",
            Names.Select((text, at) => $"//--------\n@CharaName_{at:000}\n{{\n{text}\n<WAIT>\n}}"))));
        focus = new ReserveScreenFocus(serials, new TextCleaner(TextCleanerRules.None), announcer, () => now);
    }

    private List<string> Said() => [.. announcer.Said.Select(said => said.Text)];

    private void Cursor(ScreenChange change, int index, Screen? screen = null) =>
        focus.Observe(new ScreenEvent(change, screen ?? BattleScreens.Reserve, ReserveClass, index, Count: 3));

    private void Fetched(int position, int flags = ReserveScreenFocus.NameFlags) =>
        focus.Observe(new SerialResolved(NameFirst + position, flags));

    [Fact]
    public void EachLandingIsSaidThenNamedByTheNameFetchedAfterIt()
    {
        Cursor(ScreenChange.Entered, 0);
        now += 75;
        Fetched(0);
        now += 900;
        Fetched(0);
        now += 8;
        Cursor(ScreenChange.FocusMoved, 1);
        now += 76;
        Fetched(1);

        Assert.Equal(["Reserve, 1 of 3", "Reserve, Ally One, 1 of 3", "2 of 3", "Ally Two, 2 of 3"], Said());
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }

    [Fact]
    public void ARowNamedOnceKeepsItsNameUntilTheListOpensAgain()
    {
        Cursor(ScreenChange.Entered, 1);
        now += 70;
        Fetched(1);
        now += 1_000;
        Cursor(ScreenChange.FocusMoved, 0);
        now += 1_000;
        Cursor(ScreenChange.FocusMoved, 1);
        Cursor(ScreenChange.Left, 1);
        now += 5_000;
        Cursor(ScreenChange.Entered, 1);

        Assert.Equal(
            ["Reserve, 2 of 3", "Reserve, Ally Two, 2 of 3", "1 of 3", "Ally Two, 2 of 3", "Reserve, 2 of 3"],
            Said());
    }

    [Fact]
    public void AnotherScreenOrAnotherFileNamesNothing()
    {
        Cursor(ScreenChange.Entered, 0, BattleScreens.Party);
        Fetched(0);
        Cursor(ScreenChange.Entered, 0);
        Fetched(1, flags: 7);

        Assert.Equal(["Reserve, 1 of 3"], Said());
    }
}
