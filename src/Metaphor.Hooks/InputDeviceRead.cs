using Metaphor.Core;

namespace Metaphor.Hooks;

/// <summary>Reads the game's glyph device global (#778), the u32 that picks key or pad glyphs, through
/// <c>InputDeviceGet</c> (<c>0x7adec0</c>), whose only instruction before <c>ret</c> loads it with
/// <c>mov eax,[rip+disp32]</c> (6 bytes) at <c>+0x4</c> of its signature. Game build 548ddc95… (Steam build 18330018):
/// docs/findings/game-over-component-and-input-device-static.md, read live on 2026-11-23. Only read, through
/// <see cref="ModuleMemoryReader"/>; nothing is hooked, called or written.</summary>
internal sealed class InputDeviceRead : IReadsGlobal
{
    private static readonly GlobalSpec Spec = new(
        "input device", [GameBuilds.Steam18330018], "InputDeviceGet",
        LoadOffset: 0x4, DisplacementOffset: 2, InstructionLength: 6,
        TurnsOff: "naming the buttons the game draws; the mod's own last device is used instead");

    // Set once by the loader before any hook goes live; a pointer-sized field is read and written whole.
    private nint global;

    public GlobalSpec Global => Spec;

    public void Locate(nint address) => global = address;

    /// <summary>The global's value, or null when it wasn't located or the read failed.</summary>
    public int? Value() => global == 0 ? null : ModuleMemoryReader.ReadInt32(global);
}
