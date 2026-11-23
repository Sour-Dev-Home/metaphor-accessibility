using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The Equipment slot list's names (#217): rows 1 to 4 are System_General_30 to _33, serials 1511 to 1514
/// (docs/findings/item-equipment-menu-names-from-resolves.md); row 0 (the archetype) and the other lists stay by
/// position. Hook events in, announcements out; made-up file text only.</summary>
[Trait("Category", "Unit")]
public sealed class EquipmentSlotNamesTests
{
    private const string SlotsClass = "EquipPartsChoice@ope@ui@app";
    private const string CharactersClass = "EquipCharaSelChoice@ope@ui@app";
    private const int Settle = 0x1;
    private const int First = 1500;

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();

    public EquipmentSlotNamesTests()
    {
        var serials = new MessageSerials([new SerialRange("message/system/System_Message.msg", First, 20)]);
        var messages = new MessageEvents();
        messages.ObserveFiles(serials);
        var source = string.Join("\n\n", Enumerable.Range(0, 20).Select(at =>
            $"//--------\n@System_General_{at:00}\n{{\nText {at + First}\n<WAIT>\n}}"));
        messages.Compiled(new MessageFileCompiled("EN/message/system/System_Message.msg", 21, source));
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        cursors.Observe(screens);
        screens.Observe(new EquipmentFocus(serials, announcer, new TextCleaner(TextCleanerRules.None)));
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private void Cursor(string screenClass, int index, int? count) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Settle, Result: 1, Top: index, Row: 0, Flags: 0x66,
            screenClass, count));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void SlotRowsOneToFourAreNamedFromSystemGeneral30To33()
    {
        Cursor(SlotsClass, 1, count: 5);
        Cursor(SlotsClass, 4, count: 5);

        Assert.Equal(["Text 1511, 2 of 5", "Text 1514, 5 of 5"], Said());
    }

    [Fact]
    public void TheFirstSlotRowIsSaidByPosition()
    {
        Cursor(SlotsClass, 0, count: 5);

        Assert.Equal(["1 of 5"], Said());
    }

    [Fact]
    public void CharactersStayByPosition()
    {
        Cursor(CharactersClass, 1, count: 8);

        Assert.Equal(["Equipment, 2 of 8"], Said());
    }
}
