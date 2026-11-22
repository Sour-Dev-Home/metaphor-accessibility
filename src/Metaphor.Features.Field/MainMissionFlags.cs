using System.Globalization;

namespace Metaphor.Features.Field;

/// <summary>The story flags the game's current-main-mission read scans (#658): on game build 548ddc95… (Steam build
/// 18330018) it tests flags <c>0x120a</c> down to <c>0x1165</c> and takes the highest set one that has a row in the
/// main mission table (docs/findings/mission-box-text-source-static.md). The mod doesn't read that table, so this lists
/// the highest set flags in the range for the Debug log, to pair with the mission on screen. [NEEDS VERIFICATION] in
/// memory: that these flags follow the mission box.</summary>
public static class MainMissionFlags
{
    /// <summary>The lowest flag the scan tests.</summary>
    public const int First = 0x1165;

    /// <summary>The highest flag the scan tests, and its first.</summary>
    public const int Last = 0x120a;

    /// <summary>Up to <paramref name="count"/> set flags in the range, highest first, as <paramref name="storyFlag"/>
    /// reads them; null when one can't be read.</summary>
    public static IReadOnlyList<int>? Highest(Func<int, bool?> storyFlag, int count)
    {
        ArgumentNullException.ThrowIfNull(storyFlag);
        var set = new List<int>(count);
        for (var flag = Last; flag >= First && set.Count < count; flag--)
        {
            switch (storyFlag(flag))
            {
                case null:
                    return null;
                case true:
                    set.Add(flag);
                    break;
            }
        }
        return set;
    }

    /// <summary>How the log writes <paramref name="flags"/>: hex, comma-separated, <c>none</c> or <c>?</c>.</summary>
    public static string Describe(IReadOnlyList<int>? flags) => flags switch
    {
        null => "?",
        [] => "none",
        _ => string.Join(',', flags.Select(flag => "0x" + flag.ToString("x", CultureInfo.InvariantCulture))),
    };
}
