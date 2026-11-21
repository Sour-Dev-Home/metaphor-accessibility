using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>A name entry screen (#585) speaks its title on arrival, then each character typed or deleted, privately; and
/// the game's refusal of a name when its window line shows (docs/findings/name-entry-static.md). Updates and compiled
/// files in, announcements out; made-up message text and names only.</summary>
[Trait("Category", "Unit")]
public sealed class NameEntrySpeechTests
{
    private const string SystemFile = "EN/message/system/System_Message.msg";
    private const string SystemSource =
        "@System_NameEntry_Error\n{\nNot that one.\n<WAIT>\n}\n\n" +
        "@System_NameEntry_01\n{\nName the <COLOR 0>hero<COLOR -1>.\n<WAIT>\n}\n\n" +
        "@System_NameEntry_07\n{\nName yourself.\n<WAIT>\n}";
    private const long Entry = 0x5000;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly NameEntrySpeech speech;
    private long now = 1000;

    public NameEntrySpeechTests()
    {
        var library = new MessageLibrary();
        messages.ObserveFiles(library);
        speech = new NameEntrySpeech(library, announcer, new TextCleaner(TextCleanerRules.None), () => now);
        messages.ObserveShownLines(speech);
    }

    private void SystemFileCompiled() => messages.Compiled(new MessageFileCompiled(SystemFile, 6, SystemSource));

    private void Frame(string text, bool player = false, long entry = Entry, long after = 16)
    {
        now += after;
        speech.Observe(new NameEntryFrame(entry, player, text));
    }

    [Fact]
    public void TheTitleIsSpokenOnceOnArrival()
    {
        SystemFileCompiled();

        Frame("");
        Frame("");
        Frame("");

        Assert.Equal([new Announcement("Name the hero.", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void ThePlayersOwnNameEntryHasItsOwnTitle()
    {
        SystemFileCompiled();

        Frame("", player: true);

        Assert.Equal([new Announcement("Name yourself.", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void ANameTheGameFilledInIsSpokenAfterTheTitlePrivately()
    {
        SystemFileCompiled();

        Frame("Ada");

        Assert.Equal(
            [new Announcement("Name the hero.", Interrupt: true), new Announcement("Ada", Interrupt: false, Private: true)],
            announcer.Said);
    }

    [Fact]
    public void EachTypedCharacterAndDeletionIsSpokenPrivately()
    {
        SystemFileCompiled();
        Frame("");

        Frame("A");
        Frame("Al");
        Frame("Al ");
        Frame("Al");
        Frame("A");

        Assert.Equal(
            [
                new Announcement("A", Interrupt: true, Private: true),
                new Announcement("l", Interrupt: true, Private: true),
                new Announcement("space", Interrupt: true, Private: true),
                new Announcement("deleted space", Interrupt: true, Private: true),
                new Announcement("deleted l", Interrupt: true, Private: true),
            ],
            announcer.Said.Skip(1));
    }

    // #593, by ear: Backspace was silent. The game's array always holds 10 characters, the name padded with zeros, so a
    // deletion turns the last letter into a zero (dev log: chars=10 throughout).
    [Fact]
    public void ADeletionInTheZeroPaddedArrayIsSpoken()
    {
        SystemFileCompiled();
        Frame(new string('\0', 10));

        Frame("K" + new string('\0', 9));
        Frame(new string('\0', 10));

        Assert.Equal(
            [
                new Announcement("K", Interrupt: true, Private: true),
                new Announcement("deleted K", Interrupt: true, Private: true),
            ],
            announcer.Said.Skip(1));
    }

    [Theory]
    [InlineData("Ann", "Ann", null)]
    [InlineData("An", "Ann", "n")]
    [InlineData("Ann", "An", "deleted n")]
    [InlineData("Bob", "Bxob", "x")]
    [InlineData("Bxob", "Bob", "deleted x")]
    [InlineData("Bob", "Bab", "a")]
    [InlineData("", "Eve", "Eve")]
    [InlineData("Eve", "", "deleted Eve")]
    // #593, by ear: a lone "/" was silent, since the screen reader drops lone punctuation; symbols are said by name.
    [InlineData("Al", "Al/", "slash")]
    [InlineData("Al/", "Al", "deleted slash")]
    [InlineData("O", "O'", "apostrophe")]
    [InlineData("Jo", "Jo-", "hyphen")]
    [InlineData("A", "A.", "period")]
    [InlineData("", "R2-D2", "R2 hyphen D2")]
    [InlineData("A", "A☃", NameEntrySpeech.UnknownSymbol)]
    [InlineData("A", "Aé", "é")]
    public void AChangeIsWhatWasAddedOrElseWhatWasDeleted(string before, string after, string? said) =>
        Assert.Equal(said, NameEntrySpeech.Change(before, after));

    [Fact]
    public void ANewScreenOrALongPauseSpeaksTheTitleAgain()
    {
        SystemFileCompiled();

        Frame("");
        Frame("", player: true, entry: 0x6000);
        Frame("", player: true, entry: 0x6000, after: NameEntrySpeech.VisitGapMilliseconds);

        Assert.Equal(
            ["Name the hero.", "Name yourself.", "Name yourself."], announcer.Said.Select(said => said.Text));
    }

    [Fact]
    public void WithoutTheMessageTheScreenIsStillNamed()
    {
        Frame("");

        Assert.Equal([new Announcement(NameEntrySpeech.FallbackTitle, Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void TheRefusalIsSpokenWhenItsWindowLineShows()
    {
        SystemFileCompiled();

        messages.Shown(new LineShown(1, MessageLibrary.LabelHash(NameEntrySpeech.ErrorLabel)));
        messages.Shown(new LineShown(0, MessageLibrary.LabelHash(NameEntrySpeech.ErrorLabel)));
        messages.Shown(new LineShown(1, MessageLibrary.LabelHash(NameEntrySpeech.ProtagonistTitleLabel)));

        Assert.Equal([new Announcement("Not that one.", Interrupt: true)], announcer.Said);
    }

    // #593, by ear: Enter doesn't confirm the name; Tab or the pad's Menu button does.
    [Fact]
    public void EnterOnTheScreenSaysHowToConfirmOncePerPress()
    {
        SystemFileCompiled();
        Frame("Ada");
        var before = announcer.Said.Count;

        speech.Keys([0x0D]);
        speech.Keys([0x0D]);
        speech.Keys([]);
        now += 16;
        speech.Keys([0x41, 0x0D]);

        Assert.Equal(
            [new Announcement(NameEntrySpeech.ConfirmHint, Interrupt: true), new Announcement(NameEntrySpeech.ConfirmHint, Interrupt: true)],
            announcer.Said.Skip(before));
        Assert.Equal("Press Tab or the Menu button to confirm.", NameEntrySpeech.ConfirmHint);
    }

    // The keyboard's dev log listed the typed name's key codes (#593's review); it hides them while the screen is up.
    [Fact]
    public void TheScreenIsUpOnlyWhileItUpdates()
    {
        Assert.False(speech.OnScreen());
        Frame("");
        Assert.True(speech.OnScreen());
        now += NameEntrySpeech.OnScreenMilliseconds + 1;
        Assert.False(speech.OnScreen());
    }

    [Fact]
    public void EnterAwayFromTheScreenSaysNothing()
    {
        speech.Keys([0x0D]);
        Frame("");
        speech.Keys([]);
        now += NameEntrySpeech.OnScreenMilliseconds + 1;
        speech.Keys([0x0D]);

        Assert.DoesNotContain(announcer.Said, said => said.Text == NameEntrySpeech.ConfirmHint);
    }

    [Fact]
    public void AFramesTextNeverShowsInItsDescription() =>
        Assert.Equal("player=1 chars=3", new NameEntryFrame(Entry, playerName: true, "Ada").ToString());
}
