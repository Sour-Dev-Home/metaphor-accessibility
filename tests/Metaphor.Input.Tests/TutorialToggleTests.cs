using System.Buffers.Binary;
using Metaphor.Core;
using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;
using Metaphor.Features.Dialogue;
using Metaphor.Features.Menus;

namespace Metaphor.Input.Tests;

/// <summary>The tutorials' setting (#661, speech.tutorials) wired as the mod wires it: speech through
/// <c>settings.Speech(Feature.Tutorials, ...)</c> and the dialogue controls' pop-up read gated on
/// <c>settings.Current.IsOn(Feature.Tutorials)</c>. Off, a pop-up is not spoken and repeat says the dialogue line;
/// on, the pop-up is spoken and repeat says its page. Made-up labels and text only.</summary>
[Trait("Category", "Unit")]
public sealed class TutorialToggleTests
{
    private const int X = 0x8000;
    private const long Message = 0x1315afe20;
    private const long Window = 0x5000;
    private const long Model = 0x9000;
    private const string Popup = "Walking About, 1 page. Press to walk.";

    private readonly RecordingAnnouncer announcer = new();
    private readonly SettingsReloader settings;
    private readonly TutorialPopups popups;
    private readonly DialogueControls controls;

    public TutorialToggleTests()
    {
        settings = new SettingsReloader(new RecordingAnnouncer(), _ => { });
        settings.Load("{}");
        var library = new MessageLibrary();
        library.Observe(new MessageFileCompiled("EN/message/system/System_Tutorial_1.msg", 59,
            "@Made_Title_01\n{\nWalking About\n<WAIT>\n}"));
        library.Observe(new MessageFileCompiled("EN/message/system/System_Tutorial_2.msg", 58,
            "@Made_Page_01_01\n{\nPress to walk.\n<WAIT>\n}"));
        var table = TutorialTable.Parse(TableBytes("Made_Title_01", "Made_Page_01_01"));
        popups = new TutorialPopups(
            library, settings.Speech(Feature.Tutorials, announcer), new TextCleaner(TextCleanerRules.None),
            () => table, () => 1_000);
        var history = new DialogueHistory();
        history.Add("Ada: One.");
        controls = new DialogueControls(
            () => DialogueBindings.Default, history, new DialogueReader(announcer), announcer, null,
            () => settings.Current.IsOn(Feature.Tutorials) ? popups.OnScreen : null);
    }

    // One entry's layout, as in docs/findings/tutorial-table-maps-numbers-to-labels.md; unused slots are the empty
    // label's hash.
    private static byte[] TableBytes(string title, string page)
    {
        var bytes = new byte[0x10 + 0xe0];
        "TUTO"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1);
        var entry = bytes.AsSpan(0x10);
        Convert.FromHexString(MessageLibrary.LabelHash(title)).CopyTo(entry[4..]);
        for (var slot = 0; slot < 4; slot++)
        {
            var label = slot == 0 ? page : "";
            Convert.FromHexString(MessageLibrary.LabelHash(label)).CopyTo(entry[(0x14 + slot * 0x32)..]);
        }
        return bytes;
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void WithTheSettingOffAPopUpIsNotSpokenAndRepeatRepeatsTheDialogue()
    {
        settings.Reload("""{ "speech": { "tutorials": false } }""");

        popups.WindowUpdated(Window, 0, Model, page: 0, ended: false);
        controls.WindowRead(Message);
        controls.Pad(0, X);

        Assert.Equal(["Ada: One."], Said());
    }

    [Fact]
    public void WithTheSettingOnAPopUpIsSpokenAndRepeatSaysItsPage()
    {
        popups.WindowUpdated(Window, 0, Model, page: 0, ended: false);
        controls.WindowRead(Message);
        controls.Pad(0, X);

        Assert.Equal([Popup, Popup], Said());
    }
}
