using Metaphor.Core.Announcements;
using Metaphor.Core.Settings;

namespace Metaphor.Core.Tests.Settings;

/// <summary>The settings menu's writes (#754): one value set in the player's own file, keeping everything else they
/// wrote, and applied at once without the watcher's reload saying it again.</summary>
[Trait("Category", "Unit")]
public sealed class SettingsTextTests
{
    [Fact]
    public void WithNoFileAFileWithOnlyTheSettingIsWritten()
    {
        var text = SettingsText.Set(null, ["speech", "menus"], "false");

        Assert.NotNull(text);
        Assert.False(Parse(text).IsOn(SampleFeature.Menus));
    }

    [Fact]
    public void AValueThereIsReplacedAndEverythingElseKept()
    {
        const string json = """
            {
              // the player's note
              "speech": { "menus": true, "other": 1 },
              "unknownKey": [1, 2]
            }
            """;

        var text = SettingsText.Set(json, ["speech", "menus"], "false");

        Assert.Equal(json.Replace("\"menus\": true", "\"menus\": false", StringComparison.Ordinal), text);
    }

    [Fact]
    public void AMissingSettingIsAddedToItsGroup()
    {
        var text = SettingsText.Set("""{ "speech": { "other": 1 } }""", ["speech", "menus"], "false");

        Assert.Equal("""{ "speech": { "menus": false, "other": 1 } }""", text);
    }

    [Fact]
    public void AMissingGroupIsAddedWithTheSetting()
    {
        var text = SettingsText.Set("""{ "voicedLines": "auto" }""", ["limits", "field", "wallRange"], "350");

        Assert.Equal("""{ "limits": { "field": { "wallRange": 350 } }, "voicedLines": "auto" }""", text);
        Assert.Equal(350, Parse(text).Limit("field", "wallRange", 0));
    }

    [Fact]
    public void AnEmptyObjectTakesTheSetting() =>
        Assert.Equal("""{ "soundCues": false }""", SettingsText.Set("{}", ["soundCues"], "false"));

    [Fact]
    public void AGroupThatIsNotAnObjectIsReplaced() =>
        Assert.Equal("""{ "speech": { "menus": false } }""", SettingsText.Set("""{ "speech": 3 }""", ["speech", "menus"], "false"));

    [Fact]
    public void AnObjectValueIsReplacedWhole() =>
        Assert.Equal(
            """{ "sounds": { "mark": { "on": false } } }""",
            SettingsText.Set("""{ "sounds": { "mark": { "on": true, "volume": 5 } } }""", ["sounds", "mark"], """{ "on": false }"""));

    [Theory]
    [InlineData("[1, 2]")]
    [InlineData("not json")]
    [InlineData("\"text\"")]
    public void TextThatIsNotAnObjectIsLeftAlone(string json) => Assert.Null(SettingsText.Set(json, ["soundCues"], "false"));

    [Fact]
    public void ACommentBeforeTheObjectDoesNotMoveTheInsert()
    {
        var text = SettingsText.Set("/* { */ { }", ["soundCues"], "false");

        Assert.Equal("/* { */ { \"soundCues\": false  }", text);
    }

    [Fact]
    public void AnEditAppliesAtOnceAndTheWatchersReloadSaysNothing()
    {
        var store = new TextStore("""{ "speech": { "menus": true } }""");
        var said = new RecordingAnnouncer();
        var settings = new SettingsReloader(said, SampleSettings.Catalogue, _ => { });
        settings.Load(store.Text);

        Assert.True(new SettingsEditor(store, settings).Set(["speech", "menus"], "false"));

        Assert.False(settings.Current.IsOn(SampleFeature.Menus));
        Assert.Equal("""{ "speech": { "menus": false } }""", store.Text);
        settings.Reload(store.Text);
        Assert.Empty(said.Said);
    }

    [Fact]
    public void AFailedWriteSaysSoAndPutsTheOldSettingsBack()
    {
        var store = new TextStore("""{ "speech": { "menus": true } }""") { Fails = true };
        var settings = new SettingsReloader(new RecordingAnnouncer(), SampleSettings.Catalogue, _ => { });
        settings.Load(store.Text);

        Assert.False(new SettingsEditor(store, settings).Set(["speech", "menus"], "false"));

        Assert.True(settings.Current.IsOn(SampleFeature.Menus));
    }

    // A file that is there but can't be read is never written over with only the one setting.
    [Fact]
    public void AFileThatCantBeReadIsNotWrittenOver()
    {
        var store = new TextStore("""{ "keep": true }""") { Unreadable = true };
        var settings = new SettingsReloader(new RecordingAnnouncer(), SampleSettings.Catalogue, _ => { });

        Assert.False(new SettingsEditor(store, settings).Set(["soundCues"], "false"));

        Assert.Equal(0, store.Writes);
        Assert.True(settings.Current.SoundCues);
    }

    [Fact]
    public void AStoreWithNoFileNeverSaves()
    {
        var settings = new SettingsReloader(new RecordingAnnouncer(), SampleSettings.Catalogue, _ => { });

        Assert.False(new SettingsEditor(new NoSettingsStore(), settings).Set(["soundCues"], "false"));
        Assert.True(settings.Current.SoundCues);
    }

    private static ModSettings Parse(string? json) => ModSettings.Parse(json, SampleSettings.Catalogue, _ => { });

    private sealed class TextStore(string? text) : ISettingsStore
    {
        public string? Text { get; private set; } = text;

        public bool Fails { get; init; }

        public bool Unreadable { get; init; }

        public int Writes { get; private set; }

        public string? Read() => Unreadable ? throw new IOException("locked") : Text;

        public void Write(string written)
        {
            if (Fails)
            {
                throw new IOException("denied");
            }
            Writes++;
            Text = written;
        }
    }
}
