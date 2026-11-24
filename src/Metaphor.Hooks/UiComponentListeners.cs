using Metaphor.Core;
using Metaphor.Core.Menus;

namespace Metaphor.Hooks;

// Issue #691: listeners that log UI components and layouts as they open, so a session can tell which one shows the
// item-get banner (#542), the autosave notice (#543) and the Memorandum notice (#544). The functions and their
// arguments are our reading of game build 548ddc95… (Steam build 18330018) in
// docs/findings/ui-component-open-points-static.md; the names are ours. Both only read: the loader's adapter guards
// Observe and calls the original unchanged (rule 14). Every argument is observed pointer-sized (rule 9). The layout
// animation hook ships to players too (maintainer's decision, 2026-11-23, #809), for the layouts features ask for, and
// so does the skip menu's request hook (the same day's decision on #809, option (a)).

/// <summary>The UI layouts' shared start-an-animation, <c>0xa70010</c>:
/// <c>void (Layout* layout, ss::Player* player, const u32* animation, ?, u8)</c>. The open functions (the 21st
/// virtual function) of 92 layouts reach it through a 3-argument wrapper (<c>0xa6ffd0</c>), among them
/// <c>AutoSavingLayout</c>, <c>SavingLayout</c> and <c>SystemSavingLayout</c>; other animations a layout starts land
/// here too. It hands <see cref="LayoutAnimations"/> the animations of the layout classes features asked for, such as
/// the cutscene skip menu's (#809). A Debug build also logs each call's layout class, address and animation number,
/// dropping a repeat of the same animation on the same layout within a second. In a Release build with no class asked
/// for a call reads nothing; otherwise it costs a pointer read and a lock-free lookup for the class, a set lookup, and
/// for an asked class an int read and one small allocation.</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class LayoutAnimationListener : IGameHook, IObserves5
{
    private const long QuietMilliseconds = 1000;

    private readonly MessageLog? log;
    private readonly LayoutAnimations layouts;
    private readonly ClassNames names = new();
    private readonly RepeatGate repeats = new(QuietMilliseconds, () => Environment.TickCount64);

    public LayoutAnimationListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
        layouts = services.Layouts;
    }

    public string SignatureName => "LayoutAnimationPlay";

    /// <summary>Observed before the animation starts; the fourth and fifth arguments are passed on unread.</summary>
    public void Observe(nint first, nint second, nint third, nint fourth, nint fifth)
    {
        if (log is null && !layouts.Any)
        {
            return;
        }
        var layout = names.Of(first);
        var wanted = layouts.Wants(layout);
        if (log is null && !wanted)
        {
            return;
        }
        var animation = ModuleMemoryReader.ReadInt32(third);
        if (log is not null && repeats.LetThrough(first, animation ?? -1))
        {
            log.UiAnimated(layout, first, animation);
        }
        if (wanted && layout is not null && animation is { } number)
        {
            layouts.Played(new LayoutAnimation(layout, number));
        }
    }
}

/// <summary>The cutscene skip menu's request handler, <c>UUIC_Common_MoviePause</c>'s fifth virtual function,
/// <c>0xf737c0</c> in <c>.ecode</c>: <c>void (UUIC_Common_MoviePause* this, request* request)</c>, where the request
/// holds one byte at <c>+0x0</c> and a has-value byte at <c>+0x40</c>. On a value of 1 it shows the menu's layout with
/// no animation; otherwise it closes a shown layout with animation 4768
/// (docs/findings/movie-pause-open-static.md). It hands the value to <see cref="MoviePauseRequests"/>, so the skip
/// menu's question is said as it opens (#809, the maintainer's decision of 2026-11-23: option (a), a read-only Release
/// hook). Only reads: the loader's adapter guards Observe and calls the original unchanged (rule 14). A Debug build
/// also logs each request. In a Release build with no feature hearing the menu it reads nothing; otherwise it reads two
/// bytes a request. How often the game sends one is <c>[NEEDS VERIFICATION]</c>: no analysed code calls the handler, which is
/// reached through the component's vtable from <c>.xcode</c>.</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class MoviePauseRequestListener : IGameHook, IObserves3
{
    private const string Component = "UUIC_Common_MoviePause@ui@app";

    // The request's has-value byte (docs/findings/movie-pause-open-static.md); its value is the byte at +0x0.
    private const int HasValueOffset = 0x40;

    private readonly MessageLog? log;
    private readonly MoviePauseRequests requests;

    public MoviePauseRequestListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
        requests = services.MoviePause;
    }

    public string SignatureName => "MoviePauseRequest";

    /// <summary>Observed before the handler runs; only <paramref name="component"/> and <paramref name="request"/> are
    /// arguments, the third is whatever the register held.</summary>
    public void Observe(nint component, nint request, nint third)
    {
        if (log is null && !requests.Any)
        {
            return;
        }
        var value = Value(request);
        log?.UiRequested(Component, component, value);
        requests.Requested(value);
    }

    // The request's byte, or null when it holds none or is unreadable. An array, not stackalloc: a stack buffer's loads
    // are raw-memory reads outside the reader (architecture rule 10).
    private static int? Value(nint request)
    {
        var bytes = new byte[HasValueOffset + 1];
        if (!ModuleMemoryReader.ReadBytes(request, bytes) || bytes[HasValueOffset] == 0)
        {
            return null;
        }
        return bytes[0];
    }
}

/// <summary>The item-get banner's open, <c>UUIC_Common_EventItemGet</c>'s <c>0xf560d0</c>:
/// <c>void (UUIC_Common_EventItemGet* this, request* items)</c>, called by the <c>ItemGetTreasure</c> and
/// <c>ItemGetWindowEventOpen</c> script functions. Its layout (<c>ItemGetNotice</c>) plays its animation without the
/// shared start-an-animation, so it gets its own record. Runs once a pickup.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class EventItemGetOpenListener : IGameHook, IObserves3
{
    private readonly MessageLog? log;
    private readonly ClassNames names = new();

    public EventItemGetOpenListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "EventItemGetOpen";

    /// <summary>Observed before the banner opens; only <paramref name="component"/> and <paramref name="items"/> are
    /// arguments, the third is whatever the register held.</summary>
    public void Observe(nint component, nint items, nint third) => log?.UiOpened(names.Of(component), component);
}
