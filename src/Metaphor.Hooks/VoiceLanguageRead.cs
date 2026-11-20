using Metaphor.Core;
using Metaphor.Core.Settings;

namespace Metaphor.Hooks;

/// <summary>Reads the game's applied voice language (#538), which <see cref="VoicedLinesMode.Auto"/> follows: a 32-bit
/// global, 1 for Japanese and 2 for English, that changes at once with the setting
/// (docs/findings/voice-language-values.md, game build 548ddc95…, Steam build 18330018). Its address comes from the
/// game's one getter of it, <c>mov eax,[rip+disp32]</c> (6 bytes) at the start of <c>VoiceLanguageGetter</c>, in
/// <c>.ecode</c> (docs/raw-sources/voice-language-getter-2026-11-20.md), which the loader locates on its own (#300, after
/// the build check) and only reads, never hooks or calls. One read through <see cref="ModuleMemoryReader"/> per call;
/// nothing is written. Until then, when it wasn't located, and when a read fails, the language is unknown.</summary>
internal sealed class VoiceLanguageRead : IReadsGlobal
{
    private static readonly GlobalSpec Spec = new(
        "voice language", [GameBuilds.Steam18330018], "VoiceLanguageGetter",
        LoadOffset: 0, DisplacementOffset: 2, InstructionLength: 6, TurnsOff: "automatic name-only lines");

    // Set once by the loader before any hook goes live; a pointer-sized field is read and written whole.
    private nint global;

    public GlobalSpec Global => Spec;

    public void Locate(nint address) => global = address;

    /// <summary>The voice language now, or <see cref="VoiceLanguage.Unknown"/> when the global is unknown, unreadable or
    /// holds another value.</summary>
    public VoiceLanguage Read() =>
        GameVoiceLanguage.FromGameValue(global == 0 ? null : ModuleMemoryReader.ReadInt32(global));
}
