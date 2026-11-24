using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>Battle's item list says how many of each row's item the party holds after its name (#937), from the source
/// the battle project adds (<see cref="BattleListAdditions.QuantityOf"/>). Made-up file text and serials only.</summary>
[Trait("Category", "Unit")]
public sealed class BattleItemQuantityTests
{
    private const string ItemClass = "BTL_ItemSelect@ope@ui@app";
    private const int ItemFirst = 300;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors;
    private readonly MenuScreens screens = new(KnownScreens.All);
    private readonly BattleListAdditions additions = new();
    private readonly long now = 50_000;

    public BattleItemQuantityTests()
    {
        cursors = new CursorEvents(() => now);
        var serials = new MessageSerials([new SerialRange(MessageSerials.ItemNames, ItemFirst, 3)]);
        messages.ObserveFiles(serials);
        screens.MenusRead(open: false);
        cursors.Observe(screens);
        var items = BattleListFocus.Items(serials, announcer, new TextCleaner(TextCleanerRules.None), () => now,
            viewsOn: null, tabsOn: null, additions.QuantityOf);
        screens.Observe(items);
        messages.ObserveSerials(items);
        messages.Compiled(new MessageFileCompiled("EN/message/system/Item_Message_1.msg", 15,
            "//--------\n@Item_000\n{\nTonic\n<WAIT>\n}\n\n//--------\n@Item_001\n{\nSalve\n<WAIT>\n}\n\n" +
            "//--------\n@Item_002\n{\nEther\n<WAIT>\n}"));
    }

    private void Open()
    {
        foreach (var serial in new[] { ItemFirst, ItemFirst + 1, ItemFirst + 2 })
        {
            messages.Resolved(new SerialResolved(serial, BattleListFocus.NameFlags));
        }
        Settle(0);
    }

    private void Settle(int index) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Command: 0x1, Result: 1, Top: index, Row: 0,
            Flags: 0x46, ItemClass, Count: 3));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void EachRowSaysHowManyOfItsItemThePartyHolds()
    {
        additions.AddQuantities(name => name switch
        {
            ItemFirst => 3,
            ItemFirst + 1 => 12,
            _ => null,
        });

        Open();
        Settle(1);
        Settle(2);

        Assert.Equal(["Items, Tonic, you have 3, 1 of 3", "Salve, you have 12, 2 of 3", "Ether, 3 of 3"], Said());
    }

    [Fact]
    public void WithNoSourceTheRowsSayNoQuantity()
    {
        Open();

        Assert.Equal(["Items, Tonic, 1 of 3"], Said());
    }
}
