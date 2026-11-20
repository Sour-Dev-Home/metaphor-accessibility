using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests.Messages;

/// <summary>Finding a message by its label in the message files the game compiled (#56). The source shape is the one
/// logged on build 548ddc95 (docs/raw-sources/message-listening-run-2026-11-16.md); the text here is made up.</summary>
[Trait("Category", "Unit")]
public sealed class MessageLibraryTests
{
    private const string Scene = """
        #SPEAKER_ID "51"
        @MES_test_000_00
        {
        <NS>Ada<NE>
        <VOICE "v_test_000_00">
        The bridge is out.
        <WAIT>
        }

        @MES_test_000_01
        {
        The wind picks up.
        }
        """;

    [Fact]
    public void AMessageIsFoundByItsLabelWithItsSpeakerApart()
    {
        var library = new MessageLibrary();
        library.Compiled(Scene);

        var message = library.Find("MES_test_000_00");

        Assert.NotNull(message);
        Assert.Equal("Ada", message.Speaker);
        Assert.Equal("<VOICE \"v_test_000_00\">\nThe bridge is out.\n<WAIT>", message.Text);
    }

    [Fact]
    public void ObservingACompiledFileAddsItsMessages()
    {
        var library = new MessageLibrary();

        library.Observe(new MessageFileCompiled("event/test.msg", 3, Scene));

        Assert.Equal(new GameMessage("MES_test_000_01", null, "The wind picks up."), library.Find("MES_test_000_01"));
    }

    [Fact]
    public void AMessageWithoutANameHasNoSpeaker()
    {
        var library = new MessageLibrary();
        library.Compiled(Scene);

        Assert.Equal(new GameMessage("MES_test_000_01", null, "The wind picks up."), library.Find("MES_test_000_01"));
    }

    [Fact]
    public void ALabelNoFileHasIsNotFound()
    {
        var library = new MessageLibrary();
        library.Compiled(Scene);

        Assert.Null(library.Find("MES_test_000"));
    }

    [Fact]
    public void ALaterCompileReplacesTheMessage()
    {
        var library = new MessageLibrary();
        library.Compiled(Scene);
        library.Compiled("@MES_test_000_01\n{\nThe wind dies down.\n}");

        Assert.Equal("The wind dies down.", library.Find("MES_test_000_01")?.Text);
        Assert.Equal("The bridge is out.", library.Find("MES_test_000_00")?.Text.Split('\n')[1]);
    }

    [Fact]
    public void WindowsLineEndingsAreRead()
    {
        var library = new MessageLibrary();
        library.Compiled(Scene.ReplaceLineEndings("\r\n"));

        Assert.Equal(new GameMessage("MES_test_000_01", null, "The wind picks up."), library.Find("MES_test_000_01"));
        Assert.Equal("Ada", library.Find("MES_test_000_00")?.Speaker);
    }

    // The message window finds a line by its label's MD5 (#526; docs/raw-sources/msg-window-show-line-2026-11-20.md).
    // 900150983cd24fb0d6963f7d28e17f72 is MD5("abc"), RFC 1321's test suite.
    [Theory]
    [InlineData("900150983CD24FB0D6963F7D28E17F72")]
    [InlineData("900150983cd24fb0d6963f7d28e17f72")]
    public void AMessageIsFoundByItsLabelsMd5(string hash)
    {
        var library = new MessageLibrary();
        library.Compiled("@abc\n{\nThe wind picks up.\n}");

        Assert.Equal(new GameMessage("abc", null, "The wind picks up."), library.FindByLabelHash(hash));
    }

    [Fact]
    public void AHashNoLabelHasIsNotFound()
    {
        var library = new MessageLibrary();
        library.Compiled(Scene);

        Assert.Null(library.FindByLabelHash("900150983CD24FB0D6963F7D28E17F72"));
    }
}
