using System.Text.RegularExpressions;

namespace Metaphor.Features.Dialogue;

/// <summary>Whether a message's voice clip reads the whole line (#551). The rule comes from the clip family survey in
/// docs/findings/voice-clip-families.md (PR #552), approved by the maintainer on #551 on 2026-11-20: only the families
/// below are full readings; every other clip (<c>v_co_</c>, <c>v_en_</c>, <c>v_fm</c>, <c>v_tr_travel_voice</c>, or
/// one not seen) is a reused generic sound, so the line counts as unvoiced and its text is read.</summary>
internal static partial class VoiceClips
{
    [GeneratedRegex("""<VOICE\s+"(?<clip>[^"]*)"\s*>""", RegexOptions.CultureInvariant)]
    private static partial Regex Tag();

    [GeneratedRegex("^(v_e[0-9]|v_f[0-9]|v_BE_|v_du_|v_br_|v_tr_nar)", RegexOptions.CultureInvariant)]
    private static partial Regex FullReading();

    /// <summary>True when the message carries a VOICE tag whose clip is a full reading of the line.</summary>
    public static bool ReadsTheWholeLine(string message)
    {
        foreach (Match tag in Tag().Matches(message))
        {
            if (FullReading().IsMatch(tag.Groups["clip"].Value))
            {
                return true;
            }
        }
        return false;
    }
}
