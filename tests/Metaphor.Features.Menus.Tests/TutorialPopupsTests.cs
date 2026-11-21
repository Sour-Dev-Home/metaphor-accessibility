using System.Buffers.Binary;
using Metaphor.Core;
using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>A tutorial pop-up (#118) is spoken once as it opens, "title, n pages. first page", from the messages the
/// tutorial table names for its number. Window updates in, announcements out; made-up labels and text only.</summary>
[Trait("Category", "Unit")]
public sealed class TutorialPopupsTests
{
    private const string TitlesFile = "EN/message/system/System_Tutorial_1.msg";
    private const string PagesFile = "EN/message/system/System_Tutorial_2.msg";
    private const string Titles = "@Made_Title_01\n{\nWalking About\n<WAIT>\n}\n\n@Made_Title_02\n{\nOne Thing\n<WAIT>\n}";
    private const string Pages =
        "@Made_Page_01_01\n{\nPress <PAD_ICON 1> to walk.\nMore here.\n<WAIT>\n}\n\n@Made_Page_01_02\n{\nSecond.\n<WAIT>\n}\n\n" +
        "@Made_Page_02_01\n{\nOnly page.\n<WAIT>\n}";
    private const long Window = 0x5000;
    private const long Model = 0x9000;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageLibrary library = new();
    private TutorialTable? table = TutorialTable.Parse(TableBytes(
        ("Made_Title_01", ["Made_Page_01_01", "Made_Page_01_02", "Missing_Page"]),
        ("Made_Title_02", ["Made_Page_02_01"]),
        ("Missing_Title", ["Made_Page_02_01", "Made_Page_01_02"])));

    private readonly TutorialPopups popups;

    public TutorialPopupsTests()
    {
        popups = new(library, announcer, new TextCleaner(TextCleanerRules.None), () => table);
    }

    // The table's layout (docs/findings/tutorial-table-maps-numbers-to-labels.md), unused slots as the empty label's
    // hash.
    private static byte[] TableBytes(params (string Title, string[] Pages)[] entries)
    {
        var bytes = new byte[0x10 + entries.Length * 0xe0];
        "TUTO"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), entries.Length);
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = bytes.AsSpan(0x10 + i * 0xe0);
            Convert.FromHexString(MessageLibrary.LabelHash(entries[i].Title)).CopyTo(entry[4..]);
            for (var slot = 0; slot < 4; slot++)
            {
                var label = slot < entries[i].Pages.Length ? entries[i].Pages[slot] : "";
                Convert.FromHexString(MessageLibrary.LabelHash(label)).CopyTo(entry[(0x14 + slot * 0x32)..]);
            }
        }
        return bytes;
    }

    private void Compiled()
    {
        library.Observe(new MessageFileCompiled(TitlesFile, 59, Titles));
        library.Observe(new MessageFileCompiled(PagesFile, 58, Pages));
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void APopUpIsSpokenAsItsTitlePageCountAndFirstPage()
    {
        Compiled();

        popups.WindowUpdated(Window, number: 0, Model, ended: false);

        Assert.Equal(["Walking About, 3 pages. Press to walk. More here."], Said());
        Assert.True(announcer.Said.Single().Interrupt);
    }

    [Fact]
    public void OnePageIsSaidAsOnePage()
    {
        Compiled();

        popups.WindowUpdated(Window, number: 1, Model, ended: false);

        Assert.Equal(["One Thing, 1 page. Only page."], Said());
    }

    [Fact]
    public void LaterUpdatesOfTheSamePopUpSayNothing()
    {
        Compiled();

        popups.WindowUpdated(Window, 0, Model, ended: false);
        popups.WindowUpdated(Window, 0, Model, ended: false);
        popups.WindowUpdated(Window, 0, Model, ended: false);

        Assert.Single(Said());
    }

    [Fact]
    public void ThePopUpIsSpokenAgainOnceItsWindowHasEnded()
    {
        Compiled();

        popups.WindowUpdated(Window, 1, Model, ended: false);
        popups.WindowUpdated(Window, 1, Model, ended: true);
        popups.WindowUpdated(Window, 1, Model, ended: false);

        Assert.Equal(2, Said().Count());
    }

    [Fact]
    public void AWindowWithoutItsObjectHasClosed()
    {
        Compiled();

        popups.WindowUpdated(Window, 1, Model, ended: false);
        popups.WindowUpdated(Window, 1, model: 0, ended: false);
        popups.WindowUpdated(Window, 1, Model, ended: false);

        Assert.Equal(2, Said().Count());
    }

    [Fact]
    public void ANewObjectOrNumberIsANewPopUp()
    {
        Compiled();

        popups.WindowUpdated(Window, 0, Model, ended: false);
        popups.WindowUpdated(Window, 1, Model, ended: false);
        popups.WindowUpdated(Window, 1, Model + 0x100, ended: false);

        Assert.Equal(
            ["Walking About, 3 pages. Press to walk. More here.", "One Thing, 1 page. Only page.", "One Thing, 1 page. Only page."],
            Said());
    }

    [Fact]
    public void AnEndedWindowIsNotSpoken()
    {
        Compiled();

        popups.WindowUpdated(Window, 0, Model, ended: true);
        popups.WindowUpdated(Window, 0, model: 0, ended: false);

        Assert.Empty(Said());
    }

    [Fact]
    public void ATitleNotCompiledIsSaidAsTutorialWithItsPages()
    {
        Compiled();

        popups.WindowUpdated(Window, 2, Model, ended: false);

        Assert.Equal(["Tutorial, 2 pages. Only page."], Said());
    }

    [Fact]
    public void WithoutItsFilesCompiledOnlyThePageCountIsKnown()
    {
        popups.WindowUpdated(Window, 0, Model, ended: false);

        Assert.Equal(["Tutorial, 3 pages"], Said());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    public void ANumberTheTableHasNotIsSaidAsTutorial(int number)
    {
        Compiled();

        popups.WindowUpdated(Window, number, Model, ended: false);

        Assert.Equal([TutorialPopups.Untitled], Said());
    }

    [Fact]
    public void WhileTheTableLoadsAPopUpIsSaidAsTutorial()
    {
        Compiled();
        table = null;

        popups.WindowUpdated(Window, 0, Model, ended: false);

        Assert.Equal([TutorialPopups.Untitled], Said());
    }
}
