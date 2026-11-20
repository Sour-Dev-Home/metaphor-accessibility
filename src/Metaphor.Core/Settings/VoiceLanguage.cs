namespace Metaphor.Core.Settings;

/// <summary>The language of the game's voice acting, which decides what <see cref="VoicedLinesMode.Auto"/> says. The hook
/// layer reads it from the game on each line (#538).</summary>
public enum VoiceLanguage
{
    Unknown,
    English,
    Japanese,
}

/// <summary>The game's own voice-language value as a <see cref="VoiceLanguage"/> (#538).</summary>
public static class GameVoiceLanguage
{
    /// <summary>The language the game's applied voice-language global names: 1 is Japanese and 2 English
    /// (docs/findings/voice-language-values.md, game build 548ddc95…, Steam build 18330018). Any other value, or null for
    /// a failed read, is <see cref="VoiceLanguage.Unknown"/>.</summary>
    public static VoiceLanguage FromGameValue(int? value) => value switch
    {
        1 => VoiceLanguage.Japanese,
        2 => VoiceLanguage.English,
        _ => VoiceLanguage.Unknown,
    };
}
