using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>A sub-screen's prompt (#907, ADR 0023): the Item screen says "Which item?" and the Equipment character list
/// "Change whose gear?" once when they open, after the screen's own line, and not on a focus move. The labels are
/// the audit's (docs/findings/chapter1-silent-text-audit-logged.md, item 7). Hook events in, announcements out; made-up
/// file text only.</summary>
[Trait("Category", "Unit")]
public sealed class SubScreenPromptsTests
{
    private const string SystemFile = "EN/message/system/System_Message.msg";
    private const string ItemClass = "ItemMenuChoice@ope@ui@app";
    private const string TargetClass = "ItemCharaSelChoice@ope@ui@app";
    private const string CharactersClass = "EquipCharaSelChoice@ope@ui@app";
    private const string SlotsClass = "EquipPartsChoice@ope@ui@app";
    private const int Settle = 0x1;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors = new();

    public SubScreenPromptsTests()
    {
        var library = new MessageLibrary();
        messages.ObserveFiles(library);
        messages.Compiled(new MessageFileCompiled(SystemFile, 6, string.Join("\n\n",
            Entry("MainMenu_Item_01", "Which item?"), Entry("MainMenu_Equip_01", "Change whose gear?"))));
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        cursors.Observe(screens);
        // As MenusModule wires it: the screens' own focus first, then the prompt.
        screens.Observe(new ItemFocus(announcer));
        screens.Observe(new EquipmentFocus(announcer));
        screens.Observe(new SubScreenPrompts(library, announcer, new TextCleaner(TextCleanerRules.None)));
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private static string Entry(string label, string text) => $"//--------\n@{label}\n{{\n{text}\n<WAIT>\n}}";

    private void Cursor(string screenClass, int index, int? count) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Settle, Result: 1, Top: index, Row: 0, Flags: 0x66,
            screenClass, count));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void OpeningTheItemScreenSaysItsPromptAfterTheFocusedRow()
    {
        Cursor(ItemClass, 0, count: 12);

        Assert.Equal(["Item, 1 of 12", "Which item?"], Said());
        Assert.False(announcer.Said[1].Interrupt);
    }

    [Fact]
    public void AFocusMoveSaysNoPromptAgain()
    {
        Cursor(ItemClass, 0, count: 12);
        Cursor(ItemClass, 1, count: 12);

        Assert.Equal(["Item, 1 of 12", "Which item?", "2 of 12"], Said());
    }

    [Fact]
    public void OpeningTheScreenAgainSaysItsPromptAgain()
    {
        Cursor(ItemClass, 0, count: 12);
        Cursor(TargetClass, 0, count: 4);
        Cursor(ItemClass, 0, count: 12);

        Assert.Equal(["Item, 1 of 12", "Which item?", "1 of 4", "Item, 1 of 12", "Which item?"], Said());
    }

    [Fact]
    public void TheEquipmentCharacterListSaysItsPrompt()
    {
        Cursor(CharactersClass, 0, count: 5);

        Assert.Equal(["Equipment, 1 of 5", "Change whose gear?"], Said());
    }

    [Fact]
    public void AScreenWithNoPromptSaysNone()
    {
        Cursor(SlotsClass, 0, count: 5);

        Assert.Equal(["1 of 5"], Said());
    }

    [Fact]
    public void APromptTheFileLacksIsLeftOut()
    {
        var library = new MessageLibrary();
        var bare = new RecordingAnnouncer();
        var prompts = new SubScreenPrompts(library, bare, new TextCleaner(TextCleanerRules.None));

        prompts.Observe(new ScreenEvent(ScreenChange.Entered, ItemFocus.List, ItemClass, 0, 12));

        Assert.Empty(bare.Said);
    }
}
