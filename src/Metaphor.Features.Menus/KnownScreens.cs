using Metaphor.Core.Battle;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus;

/// <summary>The screens the mod knows (spec #211), for the menu-screens module: each named by the feature that speaks
/// it, or left to another feature's speech.</summary>
public static class KnownScreens
{
    /// <summary>
    /// The title menu, the main menu, a yes/no dialog, New Game's difficulty list, a dialogue's reply choice, the
    /// Settings list and pages, the field's quick menu, battle's skill, item and Synthesis lists, its Order menu and
    /// Party screen, and the save and load screens' slot lists.
    /// </summary>
    public static IReadOnlyList<Screen> All { get; } =
    [
        TitleMenuFocus.Screen,
        MainMenuFocus.Screen,
        YesNoDialogFocus.Screen,
        DifficultyFocus.Screen,
        UnknownScreenFocus.DialogueChoice,
        .. QuestFocus.Screens,
        FollowerFocus.List,
        ArchetypeTreeFocus.CharacterPick,
        LevelUpFocus.Screen,
        SystemMenuFocus.Screen,
        TutorialListFocus.Screen,
        .. ShopFocus.Screens,
        .. SettingsFocus.Screens,
        QuickMenuFocus.Screen,
        .. ItemFocus.Screens,
        CalendarFocus.Screen,
        .. EquipmentFocus.Screens,
        BattleListFocus.SkillList,
        BattleListFocus.ItemList,
        BattleListFocus.SynthesisList,
        .. BattleOrderFocus.Screens,
        // Spoken by the battle project's party screen (#258).
        BattleScreens.Party,
        .. BattleConfirmFocus.Screens,
        SaveSlotFocus.Load,
        SaveSlotFocus.Save,
    ];
}
