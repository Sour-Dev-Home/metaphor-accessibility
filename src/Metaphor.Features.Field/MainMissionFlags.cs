using System.Globalization;

namespace Metaphor.Features.Field;

/// <summary>One row of the main mission table (#658): the story flag that starts the mission, and the MD5 of its title's
/// label as 32 hex digits, the key <see cref="Core.Messages.IMessageLookup.FindByLabelHash"/> takes
/// (docs/findings/mission-title-from-the-mission-table.md).</summary>
public sealed record MainMissionRow(int Flag, string LabelHash);

/// <summary>The story flags the game's current-main-mission read scans (#658): on game build 548ddc95… (Steam build
/// 18330018) it tests flags <c>0x120a</c> down to <c>0x1165</c> and takes the highest set one that has a row in the
/// main mission table (docs/findings/mission-box-text-source-static.md). The top set flag followed a story step in the
/// 2026-11-22 session (PR #665).</summary>
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

    /// <summary>The row of the current main mission, as the game's read <c>0x724130</c> picks it: the highest flag in
    /// the range that is set and has a row in <paramref name="rows"/>; null when none does or a flag can't be read. The
    /// game's fallback to earlier flags is left out [NEEDS VERIFICATION]: when it applies.</summary>
    public static MainMissionRow? Current(Func<int, bool?> storyFlag, IReadOnlyList<MainMissionRow> rows)
    {
        ArgumentNullException.ThrowIfNull(storyFlag);
        ArgumentNullException.ThrowIfNull(rows);
        var byFlag = new Dictionary<int, MainMissionRow>();
        foreach (var row in rows)
        {
            byFlag.TryAdd(row.Flag, row);
        }
        for (var flag = Last; flag >= First; flag--)
        {
            if (!byFlag.TryGetValue(flag, out var row))
            {
                continue;
            }
            switch (storyFlag(flag))
            {
                case null:
                    return null;
                case true:
                    return row;
            }
        }
        return null;
    }

    /// <summary>How the log writes <paramref name="flags"/>: hex, comma-separated, <c>none</c> or <c>?</c>.</summary>
    public static string Describe(IReadOnlyList<int>? flags) => flags switch
    {
        null => "?",
        [] => "none",
        _ => string.Join(',', flags.Select(flag => "0x" + flag.ToString("x", CultureInfo.InvariantCulture))),
    };
}
