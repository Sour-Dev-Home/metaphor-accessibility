using Metaphor.Core.Features;
using Metaphor.Core.Input;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>The menus project's wiring and settings (#784, ADR 0022): the menu screens' focus (#94, #97, #211), their
/// controls (#214), the battle lists (#247), the tutorial pop-ups (#118), the travel map (#545), the place names (#586)
/// and the mark key (#597). It registers first, so its controls take each input read first (the mark key, then the
/// menus) and its observers hear each event before the dialogue's.</summary>
public sealed class MenusModule : IFeatureModule
{
    public IEnumerable<Enum> Settings => Enum.GetValues<MenusFeature>().Select(feature => (Enum)feature);

    public void Register(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = context.Settings;
        var reader = context.Reader;
        var library = context.Library;
        var cleaner = context.Cleaner;
        var screens = context.Screens;
        var events = context.Events;
        var serials = context.Serials;
        var log = context.Log;
        // The mark key (#597) takes each read first, on every screen: its toggle and binding read live; keyboard only
        // until the pad layer (#688). None unless the report log is open.
        if (context.Report is { } report)
        {
            context.AddControls(new MarkKey(() => settings.Current, context.Cues, report));
        }
        // Menus speak through the reader (#214), and their controls toggle it with the dialogue's binding.
        var menuSpeech = settings.Speech(MenusFeature.Menus, reader);
        screens.Observe(new TitleMenuFocus(library, menuSpeech, cleaner));
        screens.Observe(new MainMenuFocus(menuSpeech));
        // A yes/no dialog hears each scripted choice and each select window too, to say the text of one opened outside
        // the scripts (#569).
        // A settings page's dialog says its question, known from the press that opened it (#562).
        var settingsQuestion = new SettingsConfirmQuestion();
        // The quick menu's Return to title dialog says its question too, while tested (#810).
        var titleQuestion = new ReturnToTitleQuestion(() => settings.Current.IsOn(MenusFeature.ReturnToTitleQuestion));
        var yesNo = new YesNoDialogFocus(
            library, menuSpeech, cleaner, new YesNoQuestions([settingsQuestion, titleQuestion]));
        screens.Observe(yesNo);
        screens.Observe(settingsQuestion);
        screens.Observe(titleQuestion);
        events.ObserveChoices(yesNo);
        events.ObserveSelects(yesNo);
        screens.Observe(new DifficultyFocus(library, menuSpeech, cleaner));
        screens.Observe(new SettingsFocus(library, menuSpeech, cleaner)
        {
            LearnRowIds = () => settings.Current.IsOn(MenusFeature.LearnedSettingValues),
        });
        screens.Observe(new QuickMenuFocus(library, menuSpeech, cleaner));
        screens.Observe(new SystemMenuFocus(serials, settings.Speech(MenusFeature.SystemMenu, reader), cleaner));
        // The shop's top menu and its Buy and Sell lists, each row by the name the game fetched for it (#736, #197).
        var shopFocus = new ShopFocus(serials, settings.Speech(MenusFeature.ShopMenu, reader), cleaner);
        screens.Observe(shopFocus);
        events.ObserveSerials(shopFocus);
        // The protagonist's name is typed, not a message: the <HERO_NAME> field, read when said (#216).
        var names = context.Names;
        string? Protagonist() => names.Typed(TypedName.Hero);
        var itemFocus = new ItemFocus(serials, settings.Speech(MenusFeature.ItemMenu, reader), cleaner)
        {
            ProtagonistName = Protagonist,
        };
        screens.Observe(itemFocus);
        events.ObserveSerials(itemFocus);
        // The shop and the Item list hear each cursor input before the slot screens hand it to the menu screens, so a
        // list's view is taken before its screen event is told.
        context.Cursors.Observe(shopFocus);
        context.Cursors.Observe(itemFocus);
        screens.Observe(new CalendarFocus(settings.Speech(MenusFeature.Calendar, reader)));
        // The Equipment screens name their rows from the block of names the game fetches for the focused character (#217).
        // Each cursor record ends the block (#216).
        var equipmentFocus = new EquipmentFocus(serials, settings.Speech(MenusFeature.EquipmentMenu, reader), cleaner)
        {
            ProtagonistName = Protagonist,
        };
        screens.Observe(equipmentFocus);
        events.ObserveSerials(equipmentFocus);
        context.Cursors.Observe(equipmentFocus);
        // The save and load slots (#622): their lists come unnamed, so the slot screens name them before the screens,
        // from the prompts the serial hook hands MessageEvents (2026-11-22 review, finding 5).
        var slots = new SlotScreens(screens);
        events.ObserveSerials(slots);
        screens.Observe(new SaveSlotFocus(
            library, settings.Speech(MenusFeature.SaveSlots, reader), cleaner, log is null ? null : log.MenuNote));
        // Battle's skill and item lists say each row by the name the game fetched for it by serial (#247), through
        // their own setting, not the menus' (#661).
        var battleListSpeech = settings.Speech(MenusFeature.BattleLists, reader);
        // A whole view fetched again, as after a wrap, names its rows behind its own setting while tested (#737).
        bool ListViewsOn() => settings.Current.IsOn(MenusFeature.BattleListViews);
        BattleListFocus[] battleLists =
        [
            BattleListFocus.Skills(serials, battleListSpeech, cleaner, () => Environment.TickCount64, ListViewsOn),
            BattleListFocus.Items(serials, battleListSpeech, cleaner, () => Environment.TickCount64, ListViewsOn),
        ];
        foreach (var list in battleLists)
        {
            screens.Observe(list);
            events.ObserveSerials(list);
        }
        screens.Observe(new UnknownScreenFocus(menuSpeech));
        var menuBindings = settings.Live(now =>
        {
            var toggle = ReaderBinding.From(now);
            return MenuBindings.From(now, toggle.Buttons, toggle.Key);
        });
        // The details key reads a battle list's description (#247); on a screen shown outside the menus it answers only
        // while that screen's cursor is still called.
        var menuControls = new MenuControls(menuBindings, screens, reader, battleLists, context.Cursors.CalledRecently);
        screens.Observe(menuControls);
        context.Cursors.Observe(slots);
        context.AddControls(settings.Input(MenusFeature.Menus, menuControls));
        // Tutorial pop-ups speak through the reader too, once their table has loaded (#118). They aren't the dialogue
        // window's speech, so closing that window doesn't stop them. While one is up, the dialogue's repeat binding says
        // its page again (by-ear finding of 2026-11-21), unless the tutorials' setting is off.
        var tutorials = context.Tutorials;
        var tutorialPopups = new TutorialPopups(
            library, settings.Speech(MenusFeature.Tutorials, reader), cleaner, () => tutorials.Table);
        context.Provide(tutorialPopups);
        context.Provide(new PopupOnScreen(
            () => settings.Current.IsOn(MenusFeature.Tutorials) ? tutorialPopups.OnScreen : null));
        context.Provide<IPadWatcher>(settingsQuestion);
        context.Provide(new TravelMapDestination(
            context.FilesById, settings.Speech(MenusFeature.TravelMap, reader), cleaner, () => Environment.TickCount64));
        context.Provide(new FieldMapName(settings.Speech(MenusFeature.PlaceNames, reader), cleaner));
    }
}

/// <summary>The name entry screen (#585): the title and refusals through the reader, typed characters as private
/// announcements. Its own module, registered after the dialogue's, so a refused name's line is heard by the dialogue's
/// lines first, as before #784.</summary>
public sealed class NameEntryModule : IFeatureModule
{
    /// <summary>None of its own: <see cref="MenusFeature.NameEntry"/> is the menus module's.</summary>
    public IEnumerable<Enum> Settings => [];

    public void Register(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var nameEntry = new NameEntrySpeech(
            context.Library, context.Settings.Speech(MenusFeature.NameEntry, context.Reader), context.Cleaner,
            () => Environment.TickCount64);
        context.Events.ObserveShownLines(nameEntry);
        context.Provide(nameEntry);
    }
}
