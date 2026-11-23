using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The Followers list (#199): a row is named by the n-th follower name its view fetched (flags 8) and the
/// archetype fetched right after it (flags 6) (docs/findings/follower-and-archetype-tree-screen-classes-static.md).
/// Hook events in, announcements out; made-up file text and serials only.</summary>
[Trait("Category", "Unit")]
public sealed class FollowerFocusTests
{
    private const string ListClass = "CooperationMenuChoice@ope@ui@app";
    private const int Settle = 0x1;
    private const int ArchetypeFirst = 40;

    private static readonly string[] Names = ["Ann", "Bo", "Cy"];
    private static readonly string[] Archetypes = ["Seeker", "Mage", "Knight"];

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors;
    private bool on = true;
    private long now = 50_000;

    public FollowerFocusTests()
    {
        cursors = new CursorEvents(() => now);
        var serials = new MessageSerials([
            new SerialRange(MessageSerials.CharacterNames, 0, Names.Length),
            new SerialRange(MessageSerials.ArchetypeNames, ArchetypeFirst, Archetypes.Length),
        ]);
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        var focus = new FollowerFocus(serials, announcer, new TextCleaner(TextCleanerRules.None), () => on);
        // The focus hears each cursor input before the screens turn it into a screen event, as MenusModule wires it.
        cursors.Observe(focus);
        cursors.Observe(screens);
        screens.Observe(focus);
        screens.Observe(new UnknownScreenFocus(announcer));
        messages.ObserveFiles(serials);
        messages.ObserveSerials(focus);
        messages.Compiled(new MessageFileCompiled("EN/message/CharaName.msg", 1,
            string.Join("\n\n", Names.Select((text, at) => Entry($"CharaName_{at:000}", text)))));
        messages.Compiled(new MessageFileCompiled("EN/message/ATCategoryName.msg", 30,
            string.Join("\n\n", Archetypes.Select((text, at) => Entry($"ATCategoryName_{at:00}", text)))));
    }

    private static string Entry(string label, string text) => $"//--------\n@{label}\n{{\n{text}\n<WAIT>\n}}";

    // The list's view as the 2026-11-21 log fetched it: each follower's name, then its archetype.
    private void ListView()
    {
        foreach (var (name, archetype) in new[] { (2, ArchetypeFirst + 1), (0, ArchetypeFirst + 2), (1, ArchetypeFirst) })
        {
            messages.Resolved(new SerialResolved(name, FollowerFocus.NameFlags));
            messages.Resolved(new SerialResolved(archetype, FollowerFocus.ArchetypeFlags));
        }
    }

    private void Cursor(int index, int command = Settle)
    {
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, command, Result: 1, Top: index, Row: 0, Flags: 0x66,
            ListClass, Count: 3));
        now += 10;
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void ARowSaysItsFollowerAndArchetype()
    {
        ListView();
        Cursor(0);
        Cursor(1, command: 0x14);
        Cursor(1);

        Assert.Equal(["Cy, Mage, 1 of 3", "Ann, Knight, 2 of 3"], Said());
    }

    [Fact]
    public void OffTheListSaysPositionsOnly()
    {
        on = false;
        ListView();
        Cursor(0);

        Assert.Equal(["1 of 3"], Said());
    }
}
