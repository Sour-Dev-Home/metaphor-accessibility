namespace Metaphor.Core.Announcements;

/// <summary>Picks how a line reaches the player through Prism (#115): its <c>output</c> call, which speaks and brailles
/// the same text, or <c>speak</c> alone once the backend has said it doesn't implement <c>output</c>. Status codes are
/// <c>PrismError</c> values from Prism v0.18.3's <c>include/prism.h</c>
/// (<c>docs/raw-sources/prism-output-braille-2026-11-16.md</c>). Not thread-safe: the announcer calls it under its lock.
/// </summary>
/// <remarks>The output-then-speak fallback is an idea from Persona-4-Golden-Access by AquaRose7 (GPL-3.0),
/// <c>M/Components/Voice/PrismNative.cs</c> at <c>dcff255</c>; no code was copied.</remarks>
public sealed class OutputRoute
{
    /// <summary><c>PRISM_ERROR_NOT_IMPLEMENTED</c>.</summary>
    public const int NotImplemented = 3;

    private bool outputUnsupported;

    /// <summary>True until the backend answers <c>output</c> with <see cref="NotImplemented"/>; after that, every line
    /// goes to <c>speak</c>.</summary>
    public bool TriesOutput => !outputUnsupported;

    /// <summary>Records what <c>output</c> returned and says whether the line must be spoken with <c>speak</c>
    /// instead. Only <see cref="NotImplemented"/> means that: any other failure may come after the line was already
    /// spoken (NVDA's and JAWS's <c>output</c> speak, then braille), so speaking again would say it twice.</summary>
    public bool SpeakInstead(int outputStatus)
    {
        if (outputStatus != NotImplemented)
        {
            return false;
        }
        outputUnsupported = true;
        return true;
    }
}
