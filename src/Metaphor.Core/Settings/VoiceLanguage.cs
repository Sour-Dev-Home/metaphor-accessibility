namespace Metaphor.Core.Settings;

/// <summary>The language of the game's voice acting, which decides what <see cref="VoicedLinesMode.Auto"/> says.
/// Nothing reads it yet: #103 hasn't found the game's voice-language setting, so it is always
/// <see cref="Unknown"/>.</summary>
public enum VoiceLanguage
{
    Unknown,
    English,
    Japanese,
}
