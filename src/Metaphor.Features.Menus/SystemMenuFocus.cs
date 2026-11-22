using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the in-game System submenu (#733): on entering it "System", the focused row's name and its position,
/// then each move's name and position ("Save, 2 of 6"), each cutting off the last. The six rows are, in cursor order,
/// Load, Save, Tutorials, Story, Settings, Return to Title (heard 2026-11-22, session "c"); each is named by the text
/// of the System_Option label whose help line the game fetched for that row. A row whose text isn't known is said by its
/// position. Safe to call from several threads.</summary>
/// <remarks>On build 548ddc95 (Steam build 18330018) the screen is <c>SystemMenuChoice</c> with 6 rows. Opening it
/// fetched the six help lines <c>System_Option_58</c>, <c>_59</c>, <c>_56</c>, <c>_55</c>, <c>_57</c>, <c>_60</c>
/// (serials 1682, 1683, 1680, 1679, 1681, 1684) and the rows' own names were not fetched; the names are the labels
/// <c>System_Option_52</c>, <c>_53</c>, <c>_50</c>, <c>_49</c>, <c>_51</c>, <c>_54</c> (Load, Save, "Tutorial", Story,
/// "Config", Return to Title), each a help line's label minus 6. That the rows use those labels, and why the file says
/// "Tutorial" and "Config" where the maintainer heard "Tutorials" and "Settings", are <c>[NEEDS VERIFICATION]</c>
/// (docs/findings/system-submenu-and-shop-menu-names.md).</remarks>
[FeatureSetting(Feature.Menus)]
public sealed class SystemMenuFocus : IScreenObserver
{
    // System_Option_49 is serial 1673 (System_Option_55, "The story so far...", is 1679), so each row's label is below.
    private static readonly int[] RowSerials = [1676, 1677, 1674, 1673, 1675, 1678];

    /// <summary>The in-game System submenu, by its owner's class on build 548ddc95.</summary>
    public static Screen Screen { get; } = new("System", "SystemMenuChoice@ope@ui@app");

    private readonly MessageSerials serials;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;

    public SystemMenuFocus(MessageSerials serials, IAnnouncer announcer, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(serials);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.serials = serials;
        this.announcer = announcer;
        this.cleaner = cleaner;
    }

    /// <summary>Speaks the focus when the submenu is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen != Screen || change.Change == ScreenChange.Left || change.Index < 0)
        {
            return;
        }
        var line = change.Total is { } total ? $"{change.Index + 1} of {total}" : $"{change.Index + 1}";
        if (change.Index < RowSerials.Length && serials.TextOf(RowSerials[change.Index], cleaner) is { } name)
        {
            line = $"{name}, {line}";
        }
        var heading = change.Change == ScreenChange.Entered ? $"{Screen.Name}, " : "";
        announcer.Announce(new Announcement(heading + line, Interrupt: true));
    }
}
