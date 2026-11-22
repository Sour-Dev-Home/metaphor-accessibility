namespace Metaphor.Installer.Tests;

/// <summary>How the exe speaks its own lines: as install.ps1's Select-Speech decides for the installer's.</summary>
[Trait("Category", "Unit")]
public sealed class SpeechChoiceTests
{
    [Fact]
    public void WithAScreenReaderRunningItSpeaksThroughTheReader() =>
        Assert.Equal(SpeechMode.Reader, SpeechChoice.Select([], readerRunning: true, interactive: true));

    [Fact]
    public void WithoutOneItSpeaksWithWindowsVoice() =>
        Assert.Equal(SpeechMode.Voice, SpeechChoice.Select([], readerRunning: false, interactive: true));

    [Fact]
    public void WithoutAConsoleItPrintsOnly() =>
        Assert.Equal(SpeechMode.None, SpeechChoice.Select([], readerRunning: true, interactive: false));

    [Theory]
    [InlineData("-NoSpeech")]
    [InlineData("-nospeech")]
    public void NoSpeechPrintsOnly(string flag) =>
        Assert.Equal(SpeechMode.None, SpeechChoice.Select([flag, "-Speak"], readerRunning: true, interactive: true));

    [Fact]
    public void SpeakUsesWindowsVoiceEvenWithAReader() =>
        Assert.Equal(SpeechMode.Voice, SpeechChoice.Select(["-SPEAK"], readerRunning: true, interactive: true));

    [Fact]
    public void AnswersCountAsAConsole() =>
        Assert.Equal(SpeechMode.Voice, SpeechChoice.Select(["-Answers", "no"], readerRunning: false, interactive: false));
}
