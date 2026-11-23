using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>The field's hold for the shoulder taps (#811, the maintainer's design of 2026-11-23): the game opens the
/// popularity ranking on LB and the map on RB as each goes down (by ear, #688 and #811), before the mod's chords on
/// them can form, the reader's LB with RB (#779) and the nearby list's LB with the D-pad (#181). So while the player
/// roams freely, the shoulders those chords use are held back, and a lone tap reaches the game on release
/// (<see cref="ShoulderTaps"/>). Elsewhere, menus and dialogue included, the shoulders reach the game at once.
/// </summary>
[FeatureSetting<FieldFeature>(FieldFeature.ShoulderHold)]
public sealed class FieldShoulderHold(Func<FieldBindings> bindings, Func<int> readerButtons, FreeRoamGate roam)
{
    /// <summary>Which of LB and RB the field's chords use now: the list's hold buttons, the category chord's when one is
    /// bound, and the reader's buttons in free roam; 0 elsewhere.</summary>
    public int Held()
    {
        if (!roam.Open())
        {
            return 0;
        }
        var b = bindings();
        var category = b.CategoryButton != 0 ? b.CategoryHoldButtons : 0;
        return (b.HoldButtons | category | readerButtons()) & ShoulderTaps.Shoulders;
    }
}
