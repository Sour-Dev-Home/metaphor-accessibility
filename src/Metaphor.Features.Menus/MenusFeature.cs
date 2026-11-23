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
    [SettingKey("speech", "returnToTitleQuestion", "Return to title question", Testing = true, Order = 1013,
        Description = "The question the quick menu's Return to title asks before Yes and No. Needs `menus`.")]
    ReturnToTitleQuestion,

    /// <summary>Battle's skill and item lists, row by row (#247). Separate from the menus.</summary>
    [SettingKey("speech", "battleLists", "Battle lists", Order = 1040, Description = "Battle's skill and item lists, " +
        "row by row. Separate from `menus`.")]
    BattleLists,

    /// <summary>Battle lists' rows named from a whole view the game fetched again, as after a wrap (#737). Separate
    /// from the battle lists while it is tested.</summary>
    [SettingKey("speech", "battleListViews", "Battle list views", Testing = true, Order = 1045, Description =
        "Names the rows of battle's skill and item lists after a wrap to the bottom, not only their positions. Needs " +
        "`battleLists`.")]
    BattleListViews,

    /// <summary>Battle's Order menu and its Tactics submenu (#771). Separate from the menus while it is tested.</summary>
    [SettingKey("speech", "battleOrderMenu", "Battle order menu", Testing = true, Order = 1047, Description =
        "Battle's Order menu, opened with LB, and its Tactics submenu.")]
    BattleOrderMenu,

    /// <summary>The Item screen's focus (#216). Separate from the menus while it is tested.</summary>
    [SettingKey("speech", "itemMenu", "Item menu", Testing = true, Order = 1050, Description = "The Item screen's " +
        "rows and its party-member list.")]
    ItemMenu,

    /// <summary>The Calendar's day grid (#193). Separate from the menus.</summary>
    [SettingKey("speech", "calendar", "Calendar", Order = 1060, Description = "The Calendar's day grid.")]
    Calendar,

    /// <summary>The save and load screens' slots (#622). Separate from the menus.</summary>
    [SettingKey("speech", "saveSlots", "Save slots", Order = 1080, Description = "The save and load screens' slots.")]
    SaveSlots,

    /// <summary>The System rows with no pinned setting ID saying their value, by the ID learned at run time (#770).
    /// Separate from the menus while it is tested.</summary>
    [SettingKey("speech", "learnedSettingValues", "Learned setting values", Order = 1090,
        Description = "The System rows whose setting the mod has not pinned saying their value on change, learned as " +
        "you change them. Needs `menus`.")]
    LearnedSettingValues,

    /// <summary>The name entry screen (#585).</summary>
    [SettingKey("speech", "nameEntry", "Name entry", Order = 1100, Description = "The name entry screen: its title, " +
        "each letter you type, and when a name is refused.")]
    NameEntry,

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

    /// <summary>Tutorial pop-ups and the repeat of their page (#118).</summary>
    [SettingKey("speech", "tutorials", "Tutorials", Order = 1170, Description = "Tutorial pop-ups: the title, the " +
        "page count and each page. Off also leaves the repeat key to repeat dialogue, not the pop-up's page.")]
    Tutorials,

    /// <summary>The first-launch welcome (#617). Whether it has been said is <c>welcomed</c>.</summary>
    [SettingKey("speech", "welcome", "Welcome", Order = 1220, Description = "The welcome on the title screen the " +
        "first time the game starts with the mod: the mod's version and the field keys.")]
    Welcome,

    /// <summary>The mark key, which writes a marker to the report log (#597).</summary>
    [SettingKey("report", "markKey", "Mark key", Order = 3000, Description = "The mark key, F8: writes a numbered " +
        "marker with the time to the report log and plays the mark sound. Off leaves F8 to the game.")]
    MarkKey,

    /// <summary>The report log's list of screens and focus moves the mod said nothing for (#597).</summary>
    [SettingKey("report", "silentScreens", "Silent screen list", Order = 3010, Description = "Lists in the report " +
        "log each screen and each move in a menu that the mod said nothing for.")]
    SilentScreens,
}
