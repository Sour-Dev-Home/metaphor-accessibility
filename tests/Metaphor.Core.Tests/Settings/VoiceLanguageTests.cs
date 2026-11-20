using Metaphor.Core.Settings;

namespace Metaphor.Core.Tests.Settings;

/// <summary>The game's voice-language value (#538): 1 is Japanese and 2 English (docs/findings/voice-language-values.md,
/// game build 548ddc95…, Steam build 18330018); anything else, or a failed read, is unknown, so Auto speaks the full line.
/// </summary>
[Trait("Category", "Unit")]
public sealed class VoiceLanguageTests
{
    [Theory]
    [InlineData(1, VoiceLanguage.Japanese)]
    [InlineData(2, VoiceLanguage.English)]
    [InlineData(0, VoiceLanguage.Unknown)]
    [InlineData(3, VoiceLanguage.Unknown)]
    [InlineData(-1, VoiceLanguage.Unknown)]
    [InlineData(null, VoiceLanguage.Unknown)]
    public void TheGameValueNamesTheLanguage(int? value, VoiceLanguage expected) =>
        Assert.Equal(expected, GameVoiceLanguage.FromGameValue(value));
}
