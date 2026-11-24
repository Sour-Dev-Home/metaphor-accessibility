using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>The menus project's features, each with its own on/off key in the settings file (#661, #784). A new
/// feature gets a member here as <c>DialogueFeature</c> describes, and a gate in <see cref="MenusModule"/>.</summary>
public enum MenusFeature
{
    /// <summary>The menus' focus and the details key (#94, #97, #214).</summary>
    [SettingKey("speech", "menus", "Menus", Order = 1010, Description = "The title menu, the main menu, the quick " +
        "menu, Settings, the difficulty list, yes or no questions and any other menu. Off also leaves the details key " +
        "to the game.")]
    Menus,

    /// <summary>The Return to title dialog's question, before its Yes and No (#810). Separate from the menus while it
    /// is tested.</summary>
    [SettingKey("speech", "returnToTitleQuestion", "Return to title question", Order = 1013,
        Description = "The question the quick menu's Return to title asks before Yes and No. Needs `menus`.")]
    ReturnToTitleQuestion,

    /// <summary>The question and buttons of the "Skip?" menu in a 2D cutscene (#809). Separate from the menus while it
    /// is tested.</summary>
    [SettingKey("speech", "skipSceneQuestion", "Skip scene question", Testing = true, Order = 1015,
        Description = "The question and its two buttons when the Menu button opens the skip menu in a cutscene.")]
    SkipSceneQuestion,

    /// <summary>A sub-screen's prompt, said once when it opens (#907). Separate from the menus while it is tested.
    /// </summary>
    [SettingKey("speech", "menuPrompts", "Menu prompts", Testing = true, Order = 1016, Description =
        "The prompt a menu screen shows when it opens, such as \"Which item?\" on the Item screen, said after the " +
        "screen's name and the focused row. Needs `menus`.")]
    MenuPrompts,

    /// <summary>The quick menu row's help line on the details key (#907). Separate from the menus while it is tested.
    /// </summary>
    [SettingKey("speech", "quickMenuHelp", "Quick menu help", Testing = true, Order = 1017, Description =
        "The quick menu's help line for the focused row, such as \"Use an item\", on the details key (F, or R3 on the " +
        "pad). Needs `menus`.")]
    QuickMenuHelp,

    /// <summary>The quick menu's help line after the row, on each focus move (#907): the full way, where the details
    /// key is the brief way.</summary>
    [SettingKey("speech", "quickMenuHelpOnFocus", "Quick menu help on focus", Testing = true, Order = 1018,
        Description = "The quick menu's help line said after each row as the focus moves, without pressing the details " +
        "key. Off says the row alone. Needs `quickMenuHelp`.")]
    QuickMenuHelpOnFocus,

    /// <summary>An item's or worn gear's description on the details key (#907). Separate from the item and equipment
    /// menus while it is tested.</summary>
    [SettingKey("speech", "gearDescriptions", "Item and gear descriptions", Testing = true, Order = 1052,
        Description = "The description of the focused row on the Item screen, and of the gear worn in a slot on the " +
        "Equipment screen, on the details key (F, or R3 on the pad). Needs `menus`.")]
    GearDescriptions,

    /// <summary>Battle's skill and item lists, row by row (#247). Separate from the menus.</summary>
    [SettingKey("speech", "battleLists", "Battle lists", Order = 1040, Description = "Battle's skill and item lists, " +
        "row by row. Separate from `menus`.")]
    BattleLists,

    /// <summary>Battle lists' rows named from a whole view the game fetched again, as after a wrap (#737). Separate
    /// from the battle lists while it is tested.</summary>
    [SettingKey("speech", "battleListViews", "Battle list views", Order = 1045, Description =
        "Names the rows of battle's skill and item lists after a wrap to the bottom, not only their positions. Needs " +
        "`battleLists`.")]
    BattleListViews,

    /// <summary>The battle item list's tab names (#851). Separate from the battle lists while it is tested.</summary>
    [SettingKey("speech", "battleItemTabs", "Battle item tabs", Testing = true, Order = 1046, Description =
        "The tab's name when the battle item list opens or you switch tabs with LB or RB. Needs `battleLists`.")]
    BattleItemTabs,

    /// <summary>Battle's Order menu and its Tactics submenu (#771). Separate from the menus while it is tested.</summary>
    [SettingKey("speech", "battleOrderMenu", "Battle order menu", Order = 1047, Description =
        "Battle's Order menu, opened with LB, and its Tactics submenu.")]
    BattleOrderMenu,

    /// <summary>Battle's Retry and other confirm prompts (#262, #257), their question and Yes or No.</summary>
    [SettingKey("speech", "battleConfirms", "Battle confirm prompts", Testing = true, Order = 1048, Description =
        "Battle's Retry and confirm prompts: the question, then Yes or No.")]
    BattleConfirms,

    /// <summary>The Item screen's focus (#216). Separate from the menus while it is tested.</summary>
    [SettingKey("speech", "itemMenu", "Item menu", Order = 1050, Description = "The Item screen's " +
        "rows and its party-member list.")]
    ItemMenu,

    /// <summary>The Calendar's day grid (#193). Separate from the menus.</summary>
    [SettingKey("speech", "calendar", "Calendar", Order = 1060, Description = "The Calendar's day grid.")]
    Calendar,

    /// <summary>The quest list's names and an opened quest's description (#194). Separate from the menus while it is
    /// tested; off, the list says positions only.</summary>
    [SettingKey("speech", "questList", "Quest list", Order = 1065, Description = "Each quest's name " +
        "on the quest list, and an opened quest's name and description. Needs `menus`.")]
    QuestList,

    /// <summary>The Followers list's names (#199). Separate from the menus while it is tested; off, the list says
    /// positions only.</summary>
    [SettingKey("speech", "followerList", "Follower list", Order = 1067, Description = "Each " +
        "follower's name and archetype on the Followers list. Needs `menus`.")]
    FollowerList,

    /// <summary>The Archetype Tree's character pick and each archetype its cursor reaches (#220). Separate from the
    /// menus while it is tested; off, the pick says positions only.</summary>
    [SettingKey("speech", "archetypeTree", "Archetype Tree", Testing = true, Order = 1068, Description =
        "On the Archetype Tree, each character on the pick, then each archetype the cursor reaches, with its line " +
        "and rank. Needs `menus`.")]
    ArchetypeTree,

    /// <summary>The level-up point screen's stats (#624). Separate from the menus while it is tested; off, the screen
    /// says positions only.</summary>
    [SettingKey("speech", "levelUpPoints", "Level-up points", Testing = true, Order = 1069, Description =
        "The level-up screen where you spend points: its heading and each stat's name, and the stat's description on " +
        "the details key. Needs `menus`.")]
    LevelUpPoints,

    /// <summary>The save and load screens' slots (#622). Separate from the menus.</summary>
    [SettingKey("speech", "saveSlots", "Save slots", Order = 1080, Description = "The save and load screens' slots.")]
    SaveSlots,

    /// <summary>The System rows with no pinned setting ID saying their value, by the ID learned at run time (#770).
    /// Separate from the menus while it is tested.</summary>
    [SettingKey("speech", "learnedSettingValues", "Learned setting values", Order = 1090,
        Description = "The System rows whose setting the mod has not pinned saying their value on change, learned as " +
        "you change them. Needs `menus`.")]
    LearnedSettingValues,

    /// <summary>The game's own settings rows' help line on the details key (#560, #907). Separate from the menus while it
    /// is tested.</summary>
    [SettingKey("speech", "settingsHelp", "Settings help", Testing = true, Order = 1095, Description =
        "The game's help line for the focused row on a Settings page, on the details key (F, or R3 on the pad). The " +
        "line is the one the game fetched for the row, so a row you have not moved to yet says no more. Needs `menus`.")]
    SettingsHelp,

    /// <summary>The name entry screen (#585).</summary>
    [SettingKey("speech", "nameEntry", "Name entry", Order = 1100, Description = "The name entry screen: its title, " +
        "each letter you type, and when a name is refused.")]
    NameEntry,

    /// <summary>The name entry's read-back key (#585). Separate from the name entry while it is tested.</summary>
    [SettingKey("speech", "nameReadBack", "Name read-back", Order = 1105, Description = "On the " +
        "name entry screen, the details button (R3) saying the name typed so far. Needs `nameEntry`.")]
    NameReadBack,

    /// <summary>The Equipment screen's focus (#217). Separate from the menus while it is tested.</summary>
    [SettingKey("speech", "equipmentMenu", "Equipment menu", Testing = true, Order = 1110, Description = "The " +
        "Equipment screen's slots and gear.")]
    EquipmentMenu,

    /// <summary>The travel map's destinations (#545).</summary>
    [SettingKey("speech", "travelMap", "Travel map", Order = 1120, Description = "Each destination on the travel map.")]
    TravelMap,

    /// <summary>The System menu's rows (#733). Separate from the menus.</summary>
    [SettingKey("speech", "systemMenu", "System menu", Order = 1130, Description = "The System menu's rows.")]
    SystemMenu,

    /// <summary>The shop's top menu and its Buy and Sell lists (#736, #197). Separate from the menus while it is
    /// tested.</summary>
    [SettingKey("speech", "shopMenu", "Shop menu", Order = 1140, Description = "The shop's top menu " +
        "and its Buy and Sell lists.")]
    ShopMenu,

    /// <summary>The place name the field shows on arriving (#586).</summary>
    [SettingKey("speech", "placeNames", "Place names", Order = 1150, Description = "The place name the game shows as " +
        "you arrive somewhere.")]
    PlaceNames,

    /// <summary>The field's autosave notice (#543).</summary>
    [SettingKey("speech", "autosaveNotice", "Autosave notice", Testing = true, Order = 1155, Description =
        "\"Autosaving\" when the game autosaves in the field, after the dialogue or line before it.")]
    AutosaveNotice,

    /// <summary>Tutorial pop-ups and the repeat of their page (#118).</summary>
    [SettingKey("speech", "tutorials", "Tutorials", Order = 1170, Description = "Tutorial pop-ups: the title, the " +
        "page count and each page. Off also leaves the repeat key to repeat dialogue, not the pop-up's page.")]
    Tutorials,

    /// <summary>The first-launch welcome (#617). Whether it has been said is <c>welcomed</c>.</summary>
    [SettingKey("speech", "welcome", "Welcome", Order = 1220, Description = "The welcome on the title screen the " +
        "first time the game starts with the mod: the mod's version and the field keys.")]
    Welcome,

    /// <summary>The mod's own spoken settings menu (#754).</summary>
    [SettingKey("speech", "settingsMenu", "Settings menu", Testing = true, Order = 1230, Description = "The mod's " +
        "own settings menu, opened with F2, or LT and RT held then View on the pad, on any screen. Up and Down move, " +
        "Left and Right change the setting and save it at once, F or R3 says what it does, and Escape or B closes. " +
        "While it is open the game gets no keys or buttons.")]
    SettingsMenu,

    /// <summary>The help key, which says the mod's keys for where the player is (#868).</summary>
    [SettingKey("speech", "keyHelp", "Key help", Testing = true, Order = 1240, Description = "The help key, F1, or " +
        "LT and RT held then Y on the pad: says the mod's keys that work where you are now, with their current " +
        "bindings.")]
    KeyHelp,

    /// <summary>The mark key, which writes a marker to the report log (#597).</summary>
    [SettingKey("report", "markKey", "Mark key", Order = 3000, Description = "The mark key, F8: writes a numbered " +
        "marker with the time to the report log and plays the mark sound. Off leaves F8 to the game.")]
    MarkKey,

    /// <summary>The report log's list of screens and focus moves the mod said nothing for (#597).</summary>
    [SettingKey("report", "silentScreens", "Silent screen list", Order = 3010, Description = "Lists in the report " +
        "log each screen and each move in a menu that the mod said nothing for.")]
    SilentScreens,
}
