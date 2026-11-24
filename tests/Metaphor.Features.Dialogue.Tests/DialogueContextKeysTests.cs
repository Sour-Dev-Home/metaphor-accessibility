using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>The dialogue's context keys (#879): stop the speech, say who is speaking, say the line on screen, seen
/// through what the player hears and what the game is handed back.</summary>
[Trait("Category", "Unit")]
public sealed class DialogueContextKeysTests
{
    private const int P = 0x50;
    private const int O = 0x4F;
    private const int U = 0x55;
    private const int Enter = 0x0D;
    private const int Y = 0x1000;
    private const long Message = 0x1315afe20;

    private readonly RecordingAnnouncer said = new();
    private readonly ShownLine line = new();
    private int stops;
    private readonly DialogueContextKeys keys;

    public DialogueContextKeysTests()
    {
        keys = new DialogueContextKeys(() => DialogueKeyBindings.Default, line, said, () => stops++);
    }

    private static Announcement Said(string text) => new(text, Interrupt: true);

    private void Press(int key)
    {
        keys.Keys([]);
        keys.Keys([key]);
        keys.Keys([]);
    }

    [Fact]
    public void TheSpeakerKeySaysWhoIsSpeaking()
    {
        keys.WindowRead(Message);
        line.Record("Gallica", "Hey, I've got a bad feeling about this.");

        Press(O);

        Assert.Equal([Said("Gallica")], said.Said);
    }

    [Fact]
    public void TheLineKeySaysTheLineWithoutItsSpeaker()
    {
        keys.WindowRead(Message);
        line.Record("Gallica", "Hey, I've got a bad feeling about this.");

        Press(U);

        Assert.Equal([Said("Hey, I've got a bad feeling about this.")], said.Said);
    }

    [Fact]
    public void ALineWithNoSpeakerSaysSo()
    {
        keys.WindowRead(Message);
        line.Record("", "The wind howls.");

        Press(O);

        Assert.Equal([Said("No speaker")], said.Said);
    }

    [Fact]
    public void TheStopKeyStopsTheSpeech()
    {
        keys.WindowRead(Message);

        Press(P);

        Assert.Equal(1, stops);
        Assert.Empty(said.Said);
    }

    [Fact]
    public void BeforeAnyLineTheKeysSayThereIsNone()
    {
        keys.WindowRead(Message);

        Press(O);
        Press(U);

        Assert.Equal([Said("No line on screen"), Said("No line on screen")], said.Said);
    }

    // The previous window's line isn't on screen any more once it has closed.
    [Fact]
    public void AClosedWindowsLineIsNotSaid()
    {
        keys.WindowRead(Message);
        line.Record("Gallica", "Old line.");
        keys.WindowRead(0);
        keys.WindowRead(Message);

        Press(U);

        Assert.Equal([Said("No line on screen")], said.Said);
    }

    [Fact]
    public void TheKeysAreHiddenFromTheGameWhileTheWindowIsOpen()
    {
        keys.WindowRead(Message);

        var handed = keys.Keys([P, Enter, O, U]);

        Assert.Equal([Enter], handed);
    }

    [Fact]
    public void WithTheWindowClosedTheKeysDoNothingAndReachTheGame()
    {
        keys.WindowRead(0);
        line.Record("Gallica", "A line.");

        var handed = keys.Keys([O]);

        Assert.Equal([O], handed);
        Assert.Empty(said.Said);
        Assert.Equal(0, stops);
    }

    [Fact]
    public void HoldingAKeySaysItOnce()
    {
        keys.WindowRead(Message);
        line.Record("Gallica", "A line.");

        keys.Keys([O]);
        keys.Keys([O]);
        keys.Keys([O]);

        Assert.Single(said.Said);
    }

    [Fact]
    public void AKeyAlreadyHeldWhenTheWindowOpensDoesNothing()
    {
        keys.Keys([O]);
        keys.WindowRead(Message);
        line.Record("Gallica", "A line.");

        keys.Keys([O]);

        Assert.Empty(said.Said);
    }

    [Fact]
    public void NoPadButtonIsBoundByDefault()
    {
        keys.WindowRead(Message);
        line.Record("Gallica", "A line.");

        var handed = keys.Pad(0, 0xFFFF, made: true);

        Assert.Equal(0xFFFF, handed);
        Assert.Empty(said.Said);
    }

    [Fact]
    public void ABoundPadButtonWorksAndIsHidden()
    {
        var bound = DialogueKeyBindings.Default with { SpeakerButtons = Y };
        var padKeys = new DialogueContextKeys(() => bound, line, said, () => stops++);
        padKeys.WindowRead(Message);
        line.Record("Gallica", "A line.");

        padKeys.Pad(0, 0, made: true);
        var handed = padKeys.Pad(0, Y, made: true);

        Assert.Equal(0, handed);
        Assert.Equal([Said("Gallica")], said.Said);
    }

    [Fact]
    public void TheSettingsFileRebindsTheKeys()
    {
        var settings = ModSettings.Parse(
            """{ "keys": { "dialogue": { "speakerKey": "F2", "lineKey": "none" } } }""",
            SettingCatalogue.Empty, _ => { });

        var bindings = DialogueKeyBindings.From(settings);

        Assert.Equal(0x71, bindings.SpeakerKey);
        Assert.Equal(0, bindings.LineKey);
        Assert.Equal(P, bindings.StopKey);
    }

    // #879: who and the line are asked for, so they're said whatever the reader switch says; the announcer handed in is
    // the one outside the switch (DialogueModule), and the line is recorded spoken or not.
    [Fact]
    public void TheLineIsRecordedWhenTheReaderIsOff()
    {
        var reader = new DialogueReader(new RecordingAnnouncer());
        reader.Toggle();
        var announcer = new DialogueAnnouncer(
            reader, new TextCleaner(TextCleanerRules.None), new DialogueHistory(), shown: line);
        keys.WindowRead(Message);

        announcer.Show("Gallica", "A line.");
        Press(U);

        Assert.Equal([Said("A line.")], said.Said);
    }
}
