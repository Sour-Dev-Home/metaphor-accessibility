using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Battle.Tests;

/// <summary>The battle Order menu's Party screen (#258): each row says the member's name, as the screen fetched it by
/// serial, and HP and MP from the party read, as the 2026-11-23 evening log ran (a name fetched as the screen opened,
/// one on the first landing on a row, none for the protagonist). Made-up names and numbers only.</summary>
[Trait("Category", "Unit")]
public sealed class PartyScreenFocusTests
{
    private const int NameFirst = 500;
    private const string PartyClass = "BTL_OrderPartyBattleMember@ope@ui@app";
    private static readonly string[] Names = ["Hero", "Ally Two", "Ally Three"];

    private readonly RecordingAnnouncer announcer = new();
    private readonly Party party = new();
    private readonly MessageSerials serials = new([new SerialRange(MessageSerials.CharacterNames, NameFirst, 3)]);
    private readonly PartyScreenFocus focus;
    private long now = 10_000;

    public PartyScreenFocusTests()
    {
        serials.Observe(new MessageFileCompiled("EN/message/CharaName.msg", 1, string.Join("\n\n",
            Names.Select((text, at) => $"//--------\n@CharaName_{at:000}\n{{\n{text}\n<WAIT>\n}}"))));
        party.Now =
        [
            new PartyMember(0, PartyScreenFocus.Protagonist, 200, 250, 30),
            new PartyMember(1, 5, 120, null, 40),
            new PartyMember(2, 8, 90, null, null),
        ];
        focus = new PartyScreenFocus(serials, new TextCleaner(TextCleanerRules.None), party, announcer, () => now)
        {
            ProtagonistName = () => "Typed",
        };
    }

    private List<string> Said() => [.. announcer.Said.Select(said => said.Text)];

    private void Cursor(ScreenChange change, int index, Screen? screen = null) =>
        focus.Observe(new ScreenEvent(change, screen ?? BattleScreens.Party, PartyClass, index, Count: 4));

    private void Fetched(int position, int flags = PartyScreenFocus.NameFlags) =>
        focus.Observe(new SerialResolved(NameFirst + position, flags));

    [Fact]
    public void ANameFetchedAsTheScreenOpensNamesTheRowItLandsOn()
    {
        Fetched(1);
        now += 20;
        Cursor(ScreenChange.Entered, 1);

        Assert.Equal(["Party, Ally Two, 120 HP, 40 MP, 2 of 4"], Said());
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }

    [Fact]
    public void ANameFetchedJustAfterALandingSaysTheRowAgainWithIt()
    {
        Cursor(ScreenChange.Entered, 1);
        now += 500;
        Cursor(ScreenChange.FocusMoved, 2);
        now += 20;
        Fetched(2);

        Assert.Equal(["Party, 120 HP, 40 MP, 2 of 4", "90 HP, 3 of 4", "Ally Three, 90 HP, 3 of 4"], Said());
    }

    [Fact]
    public void ARowKeepsItsNameOnTheNextVisitAndTheProtagonistIsSaidByHisTypedName()
    {
        Cursor(ScreenChange.Entered, 2);
        Fetched(2);
        now += 500;
        Cursor(ScreenChange.FocusMoved, 0);
        Cursor(ScreenChange.Left, 0);
        now += 5_000;
        Cursor(ScreenChange.Entered, 2);

        Assert.Equal(
            ["Party, 90 HP, 3 of 4", "Party, Ally Three, 90 HP, 3 of 4", "Typed, 200 of 250 HP, 30 MP, 1 of 4",
             "Party, Ally Three, 90 HP, 3 of 4"],
            Said());
    }

    [Fact]
    public void ALateFetchAnotherFileOrAnotherScreenNamesNothing()
    {
        Cursor(ScreenChange.Entered, 1);
        now += 1_000;
        Fetched(1);
        Fetched(2, flags: 7);
        Cursor(ScreenChange.Entered, 0, new Screen("Orders", "BTL_OrderTop@ope@ui@app", OutsideMenus: true));

        Assert.Equal(["Party, 120 HP, 40 MP, 2 of 4"], Said());
        Assert.Null(focus.NameOf(5));
    }

    [Fact]
    public void ARowWithNoMemberIsSaidByItsPosition()
    {
        Cursor(ScreenChange.Entered, 3);

        Assert.Equal(["Party, 4 of 4"], Said());
    }

    private sealed class Party : IBattleParty
    {
        public IReadOnlyList<PartyMember> Now { get; set; } = [];

        public IReadOnlyList<PartyMember> Members() => Now;
    }
}
