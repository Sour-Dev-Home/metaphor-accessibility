namespace Metaphor.Core.Settings;

/// <summary>The reader binding (#58): the dialogue's <c>keys.dialogue.readerButtons</c> and <c>readerKey</c>, which the
/// menus toggle the reader with too (#214). Here, not in the dialogue project, so both features read it (#784).
/// </summary>
/// <param name="Buttons">Bits of the game's pad mask, all held together.</param>
/// <param name="Key">A Windows virtual-key code.</param>
public readonly record struct ReaderBinding(int Buttons, int Key)
{
    /// <summary>LB + RB, and T (#58's design comment). The game's mask puts LB at <c>0x400</c> and RB at <c>0x800</c> on
    /// game build 548ddc95… (Steam build 18330018): docs/raw-sources/pad-and-keyboard-state-2026-11-16.md. T is
    /// <c>VK_T</c> (0x54).</summary>
    public static ReaderBinding Default { get; } = new(0x400 | 0x800, 0x54);

    /// <summary>The binding the settings file names, each missing or unusable part the default.</summary>
    public static ReaderBinding From(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(
            settings.Binding("dialogue", "readerButtons", Default.Buttons),
            settings.Binding("dialogue", "readerKey", Default.Key));
    }
}
