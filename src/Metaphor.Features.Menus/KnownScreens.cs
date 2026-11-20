using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus;

/// <summary>The screens the mod knows (spec #211), for the menu-screens module: each named by the feature that speaks
/// it, or left to another feature's speech.</summary>
public static class KnownScreens
{
    /// <summary>The title menu, the main menu, a yes/no dialog and a dialogue's reply choice.</summary>
    public static IReadOnlyList<Screen> All { get; } =
        [TitleMenuFocus.Screen, MainMenuFocus.Screen, YesNoDialogFocus.Screen, UnknownScreenFocus.DialogueChoice];
}
