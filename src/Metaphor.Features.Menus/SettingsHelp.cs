using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>The game's own settings rows' help lines on the details key (#560, #907, ADR 0023): the line the game fetched
/// for the row as the cursor settled on it, kept by the page, the page's row count and the row, so a row says its line
/// once the player has moved to it. Never fetches a line itself. Safe to call from several threads.</summary>
/// <remarks>On build 548ddc95 (Steam build 18330018) each move on a settings page resolves the focused row's name and then
/// its help line, before the settled cursor record, alongside the names of rows being redrawn
/// (docs/raw-sources/settings-ok-and-rows-by-ear-2026-11-21.md). The help lines are <c>System_Option_29</c> to <c>_44</c>,
/// <c>_75</c> to <c>_81</c>, <c>_90</c>, <c>_92</c>, <c>_94</c>, <c>_104</c> and <c>_113</c>, and every
/// <c>System_tips_message_PC_*</c> (docs/findings/settings-screens-inventory-static.md). So the last help line fetched
/// since the last settled input is the focused row's. A row whose move fetched none says no more: an earlier fetch for
/// the same row stays, a row never reached says nothing.</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.SettingsHelp)]
public sealed class SettingsHelp : ISerialResolveObserver, ICursorObserver, IScreenDetails
{
    private const string TipsPrefix = "System_tips_message_PC_";
    private const string OptionPrefix = "System_Option_";

    private readonly MessageSerials serials;
    private readonly TextCleaner cleaner;
    private readonly Lock gate = new();
    private readonly Dictionary<(string ScreenClass, int Count, int Index), int> learned = [];
    private int? pending;

    public SettingsHelp(MessageSerials serials, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(serials);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.serials = serials;
        this.cleaner = cleaner;
    }

    /// <summary>Whether the details key says a line, from its setting; on when not given.</summary>
    public Func<bool> On { get; init; } = () => true;

    /// <summary>Keeps the last help line the game fetches since the last settled cursor input.</summary>
    public void Observe(SerialResolved resolve)
    {
        if (serials.FindBySerial(resolve.Serial) is { } message && IsHelp(message.Label))
        {
            lock (gate)
            {
                pending = resolve.Serial;
            }
        }
    }

    /// <summary>Ties the line fetched since the last settled input to the row the cursor settled on.</summary>
    public void Observe(CursorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.IsMove)
        {
            return;
        }
        lock (gate)
        {
            if (pending is { } serial && input.ScreenClass is { } screenClass
                && SettingsPages.Classes.Contains(screenClass) && !input.OffList)
            {
                learned[(screenClass, input.Count ?? -1, input.Index)] = serial;
            }
            pending = null;
        }
    }

    /// <summary>The focused row's help line for the details key, or null on another screen, for a row whose line wasn't
    /// fetched, or when it is off.</summary>
    public string? Details(ScreenEvent focus)
    {
        ArgumentNullException.ThrowIfNull(focus);
        if (!On() || focus.OffList || !SettingsPages.Classes.Contains(focus.ScreenClass))
        {
            return null;
        }
        int serial;
        lock (gate)
        {
            if (!learned.TryGetValue((focus.ScreenClass, focus.Count ?? -1, focus.Index), out serial))
            {
                return null;
            }
        }
        return serials.TextOf(serial, cleaner);
    }

    private static bool IsHelp(string label)
    {
        if (label.StartsWith(TipsPrefix, StringComparison.Ordinal))
        {
            return true;
        }
        return label.StartsWith(OptionPrefix, StringComparison.Ordinal)
            && int.TryParse(label.AsSpan(OptionPrefix.Length), out var number)
            && (number is >= 29 and <= 44 or >= 75 and <= 81 or 90 or 92 or 94 or 104 or 113);
    }
}
