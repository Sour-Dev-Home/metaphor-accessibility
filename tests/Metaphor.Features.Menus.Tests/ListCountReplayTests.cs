using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Replay;

namespace Metaphor.Features.Menus.Tests;

/// <summary>Every list screen says "n of total" (#289), the total being the item count the game keeps for the list,
/// read with each cursor input. Replays a fixture under <c>tests/fixtures/sessions/</c> through the cursor seam, with the
/// menu features wired as the mod wires them, and checks what was announced.</summary>
[Trait("Category", "Unit")]
public sealed class ListCountReplayTests
{
    private readonly RecordingAnnouncer announcer = new();

    [Fact]
    public void KnownAndUnknownScreensSayTheTotalAndAnUnreadableCountSaysThePositionAlone()
    {
        var screens = new MenuScreens(KnownScreens.All);
        var cursors = new CursorEvents();
        screens.Observe(new MainMenuFocus(announcer));
        screens.Observe(new SystemMenuFocus(new MessageSerials([]), announcer, new TextCleaner(TextCleanerRules.None)));
        screens.Observe(new UnknownScreenFocus(announcer));
        cursors.Observe(screens);
        SessionFixture.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sessions", "list-count-session6.txt")))
            .Replay(new MessageEvents(), cursors, menus: screens.MenusRead);

        // Session 6's counts (build 548ddc95): the main menu (9), Skill's character list (8) and skill list (9), the main
        // menu's System entry, the System menu (6), then a System input whose count wasn't read.
        Assert.Equal(
            ["Menu, Skill, 1 of 9", "1 of 8", "2 of 8", "1 of 9", "Menu, System, 9 of 9", "System, 1 of 6", "3"],
            announcer.Said.Select(said => said.Text));
    }
}
