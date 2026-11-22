using Metaphor.Core.Messages;

namespace Metaphor.Core.Menus;

/// <summary>Names the save and load screens' slot lists (#622), which reach the menu cursor hook with no screen class:
/// no choice hands out their manager. Each opening fetches its prompt by serial just before its new manager's first
/// cursor, the load screen's "Read the records so far?" (serial 1634) and the save screen's "Record your actions thus
/// far?" (1299), and nowhere else in two sessions on game build 548ddc95… (Steam build 18330018); the load list holds
/// 17 items and the save list 16 (docs/findings/save-load-slot-screens.md). So after a prompt, the next input with
/// that count, classed or not, names its manager, and that manager's inputs carry <see cref="LoadClass"/> or
/// <see cref="SaveClass"/> until another opening replaces it. Every input is handed on to <c>inner</c>, named or not.
/// Safe to call from several threads.</summary>
public sealed class SlotScreens : ICursorObserver, ISerialResolveObserver
{
    /// <summary>The load screen's slot list. The mod's own name: the list owner's class isn't read.</summary>
    public const string LoadClass = "slots:load";

    /// <summary>The save screen's slot list, as <see cref="LoadClass"/>.</summary>
    public const string SaveClass = "slots:save";

    // The prompt serials and list counts on build 548ddc95 (docs/findings/save-load-slot-screens.md).
    private const int LoadPrompt = 1634;
    private const int SavePrompt = 1299;
    private const int LoadCount = 17;
    private const int SaveCount = 16;

    private readonly ICursorObserver inner;
    private readonly Lock gate = new();
    private string? armed;
    private long manager;
    private string? named;

    public SlotScreens(ICursorObserver inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        this.inner = inner;
    }

    /// <summary>Takes a message the game fetched by serial, as <see cref="MessageEvents"/> hands it on (2026-11-22
    /// review, finding 5): a slot screen's prompt means its list opens next. Always listening, since a prompt can come
    /// at any time.</summary>
    public void Observe(SerialResolved resolve)
    {
        var screen = resolve.Serial switch
        {
            LoadPrompt => LoadClass,
            SavePrompt => SaveClass,
            _ => null,
        };
        if (screen is null)
        {
            return;
        }
        lock (gate)
        {
            armed = screen;
        }
    }

    /// <summary>Hands <paramref name="input"/> on, with the slot screen's name when it is the slot list's.</summary>
    public void Observe(CursorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        lock (gate)
        {
            // The count after a prompt decides, whatever class the input arrives with: late in the game the load
            // list's manager came already classed (SingleChoiceBase@ope@ui@app, count=17; #732).
            if (armed is { } screen && input.Count == (screen == LoadClass ? LoadCount : SaveCount))
            {
                named = screen;
                manager = input.Manager;
                armed = null;
            }
            if (named is { } slots && input.Manager == manager)
            {
                input = input with { ScreenClass = slots };
            }
        }
        inner.Observe(input);
    }
}
