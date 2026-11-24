using Metaphor.Core.Battle;
using Metaphor.Core.Cues;
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
    // The positions setting (#664) is Core's, shared with the dialogue's choices; the menus list it, since they own the
    // settings pages it mostly acts on.
    public IEnumerable<Enum> Settings =>
        [.. Enum.GetValues<MenusFeature>().Select(feature => (Enum)feature), SpokenDetail.Positions];

    // Core's sliders that no one feature owns: the shoulder tap length (the field's and the battle's chords) and the
    // one-shot sounds' volume (hits and the mark sound).
    public IEnumerable<SliderLimit> Limits => [ShoulderTapLimits.TapLength, CueVolumes.Other];

    public void Register(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = context.Settings;
        // A prompt is not cut off by the row line after it (#923): every menu line goes through the hold.
        var reader = new PromptHold(context.Reader, () => Environment.TickCount64);
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
        RegisterSettingsMenu(context);
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
        // The name confirmation says the name typed where the game's text has <V 0> (#850); the name entry's module,
        // after this one, says where that name is.
        var typedName = new TypedNameSource();
        context.Provide(typedName);
        var yesNo = new YesNoDialogFocus(
            library, menuSpeech, cleaner, new YesNoQuestions([settingsQuestion, titleQuestion]))
        {
            TypedName = typedName.Name,
        };
        screens.Observe(yesNo);
        screens.Observe(settingsQuestion);
        screens.Observe(titleQuestion);
        events.ObserveChoices(yesNo);
        events.ObserveSelects(yesNo);
        // The cutscene's skip menu (#809) says its question on its component's show request, not on its layout's one
        // animation, which is the close, while tested.
        context.MoviePause.Observe(new SkipSceneQuestion(
            settings.Speech(MenusFeature.SkipSceneQuestion, reader), context.LastDevice),
            () => settings.Current.IsOn(MenusFeature.SkipSceneQuestion));
        // The field's autosave notice (#543) is its layout's show animation, while tested.
        context.Layouts.Observe(new AutosaveNotice(settings.Speech(MenusFeature.AutosaveNotice, reader)));
        screens.Observe(new DifficultyFocus(library, menuSpeech, cleaner));
        screens.Observe(new SettingsFocus(library, menuSpeech, cleaner)
        {
            LearnRowIds = () => settings.Current.IsOn(MenusFeature.LearnedSettingValues),
        });
        screens.Observe(new QuickMenuFocus(library, menuSpeech, cleaner));
        screens.Observe(new SystemMenuFocus(serials, settings.Speech(MenusFeature.SystemMenu, reader), cleaner));
        screens.Observe(new BattleOrderFocus(serials, settings.Speech(MenusFeature.BattleOrderMenu, reader), cleaner));
        screens.Observe(new BattleConfirmFocus(library, settings.Speech(MenusFeature.BattleConfirms, reader), cleaner));
        // The shop's top menu and its Buy and Sell lists, each row by the name the game fetched for it (#736, #197).
        bool SayPositions() => settings.Current.IsOn(SpokenDetail.Positions);
        var shopFocus = new ShopFocus(serials, settings.Speech(MenusFeature.ShopMenu, reader), cleaner)
        {
            SayPositions = SayPositions,
        };
        screens.Observe(shopFocus);
        events.ObserveSerials(shopFocus);
        // The protagonist's name is typed, not a message: the <HERO_NAME> field, read when said (#216).
        var names = context.Names;
        string? Protagonist() => names.Typed(TypedName.Hero);
        var itemFocus = new ItemFocus(serials, settings.Speech(MenusFeature.ItemMenu, reader), cleaner)
        {
            ProtagonistName = Protagonist,
            SayPositions = SayPositions,
            DescriptionsOn = () => settings.Current.IsOn(MenusFeature.GearDescriptions),
        };
        screens.Observe(itemFocus);
        events.ObserveSerials(itemFocus);
        // The quick menu's help lines, on the details key, or after each row with the on-focus setting (#907).
        var quickHelp = new QuickMenuHelp(serials, settings.Speech(MenusFeature.QuickMenuHelp, reader), cleaner)
        {
            On = () => settings.Current.IsOn(MenusFeature.QuickMenuHelp),
            SayOnFocus = () => settings.Current.IsOn(MenusFeature.QuickMenuHelpOnFocus),
        };
        screens.Observe(quickHelp);
        events.ObserveSerials(quickHelp);
        // The game's own settings rows' help lines, on the details key (#560, #907).
        var settingsHelp = new SettingsHelp(serials, cleaner)
        {
            On = () => settings.Current.IsOn(MenusFeature.SettingsHelp),
        };
        events.ObserveSerials(settingsHelp);
        // The shop and the Item list hear each cursor input before the slot screens hand it to the menu screens, so a
        // list's view is taken before its screen event is told.
        // The quest list's rows and an opened quest, by the names and text fetched from the quest files, while tested
        // (#194); off, the list says positions through the menus' speech as before.
        var questFocus = new QuestFocus(serials, menuSpeech, cleaner, () => settings.Current.IsOn(MenusFeature.QuestList));
        screens.Observe(questFocus);
        events.ObserveSerials(questFocus);
        // The Followers list's rows by the follower and archetype names it fetched, while tested (#199).
        var followerFocus = new FollowerFocus(
            serials, menuSpeech, cleaner, () => settings.Current.IsOn(MenusFeature.FollowerList));
        screens.Observe(followerFocus);
        events.ObserveSerials(followerFocus);
        // The Archetype Tree's pick and each archetype it focuses, by the names fetched, while tested (#220).
        var archetypeTree = new ArchetypeTreeFocus(
            serials, menuSpeech, cleaner, () => settings.Current.IsOn(MenusFeature.ArchetypeTree))
        {
            ProtagonistName = Protagonist,
        };
        screens.Observe(archetypeTree);
        events.ObserveSerials(archetypeTree);
        context.Cursors.Observe(archetypeTree);
        // The level-up point screen's stats, while tested (#624).
        var levelUp = new LevelUpFocus(serials, menuSpeech, cleaner, () => settings.Current.IsOn(MenusFeature.LevelUpPoints));
        screens.Observe(levelUp);
        context.Cursors.Observe(shopFocus);
        context.Cursors.Observe(itemFocus);
        context.Cursors.Observe(quickHelp);
        context.Cursors.Observe(settingsHelp);
        context.Cursors.Observe(questFocus);
        context.Cursors.Observe(followerFocus);
        screens.Observe(new CalendarFocus(settings.Speech(MenusFeature.Calendar, reader)));
        // The Equipment screens name their rows from the block of names the game fetches for the focused character (#217).
        // Each cursor record ends the block (#216).
        var equipmentFocus = new EquipmentFocus(serials, settings.Speech(MenusFeature.EquipmentMenu, reader), cleaner)
        {
            ProtagonistName = Protagonist,
            DescriptionsOn = () => settings.Current.IsOn(MenusFeature.GearDescriptions),
        };
        screens.Observe(equipmentFocus);
        events.ObserveSerials(equipmentFocus);
        context.Cursors.Observe(equipmentFocus);
        // A sub-screen's prompt queues after its screen's own line, so it is heard after the Item and Equipment
        // screens' focus (#907).
        screens.Observe(new SubScreenPrompts(library, settings.Speech(MenusFeature.MenuPrompts, reader.Prompts), cleaner));
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
        // The battle module, after this one, adds each skill's cost (#692 (b)) and the Synthesis list's setting (#260),
        // each its own battle setting.
        var additions = new BattleListAdditions();
        context.Provide(additions);
        BattleListFocus[] battleLists =
        [
            BattleListFocus.Skills(
                serials, battleListSpeech, cleaner, () => Environment.TickCount64, ListViewsOn,
                name => additions.CostOf(name, turnIcons: false)),
            BattleListFocus.Synthesis(
                serials, battleListSpeech, cleaner, () => Environment.TickCount64, ListViewsOn,
                name => additions.CostOf(name, turnIcons: true), additions.SynthesisOn),
            // The item list's tabs say their names behind their own setting while tested (#851).
            // Each row's quantity, once the battle project has a source for it (#937).
            BattleListFocus.Items(serials, battleListSpeech, cleaner, () => Environment.TickCount64, ListViewsOn,
                () => settings.Current.IsOn(MenusFeature.BattleItemTabs), additions.QuantityOf),
        ];
        foreach (var list in battleLists)
        {
            screens.Observe(list);
            events.ObserveSerials(list);
        }
        screens.Observe(new UnknownScreenFocus(menuSpeech, context.Unknowns));
        var menuBindings = settings.Live(now =>
        {
            var toggle = ReaderBinding.From(now);
            return MenuBindings.From(now, toggle.Buttons, toggle.Key);
        });
        // The details key reads a battle list's description (#247); on a screen shown outside the menus it answers only
        // while that screen's cursor is still called.
        // The level-up screen's details are its stat's description (#624).
        // The Item and Equipment screens', the quick menu's and the settings pages' are the row's description or help line
        // the game fetched (#907, #560).
        // The Archetype Tree's are the focused archetype's study state (#933).
        IScreenDetails[] details = [.. battleLists, levelUp, itemFocus, equipmentFocus, quickHelp, settingsHelp, archetypeTree];
        var menuControls = new MenuControls(menuBindings, screens, reader, details, context.Cursors.CalledRecently);
        screens.Observe(menuControls);
        context.Cursors.Observe(slots);
        context.AddControls(settings.Input(MenusFeature.Menus, menuControls));
        // Tutorial pop-ups speak through the reader too, once their table has loaded (#118). They aren't the dialogue
        // window's speech, so closing that window doesn't stop them. While one is up, the dialogue's repeat binding says
        // its page again (by-ear finding of 2026-11-21), unless the tutorials' setting is off.
        var tutorials = context.Tutorials;
        // A pop-up's last page names the help key, while tested and while that key is on (#806).
        var tutorialPopups = new TutorialPopups(
            library, settings.Speech(MenusFeature.Tutorials, reader), cleaner, () => tutorials.Table)
        {
            KeyHint = () => settings.Current is var now && now.IsOn(MenusFeature.TutorialKeyHint)
                && now.IsOn(MenusFeature.KeyHelp)
                    ? MenuKeys.Help.Keys(now)
                    : null,
        };
        // The System tutorial list's rows by the titles their pop-ups say, while tested (#806); off, positions.
        screens.Observe(new TutorialListFocus(
            library, menuSpeech, cleaner, () => tutorials.Table, () => settings.Current.IsOn(MenusFeature.TutorialList)));
        context.Provide(tutorialPopups);
        context.Provide(new PopupOnScreen(
            () => settings.Current.IsOn(MenusFeature.Tutorials) ? tutorialPopups.OnScreen : null));
        context.Provide<IPadWatcher>(settingsQuestion);
        context.Provide(new TravelMapDestination(
            context.FilesById, settings.Speech(MenusFeature.TravelMap, reader), cleaner, () => Environment.TickCount64));
        context.Provide(new FieldMapName(settings.Speech(MenusFeature.PlaceNames, reader), cleaner));
    }

    // The mod's settings menu (#754) and the help key (#868), while tested, after the mark key: the help first, so it
    // answers in the menu too, then the menu, which keeps each read from every later control and, through the input
    // block, from the game while it's open. Both speak straight to speech, not through the reader switch, so they
    // work with the reader off. Their chord's triggers are held back while either is on (#754).
    private static void RegisterSettingsMenu(FeatureContext context)
    {
        var settings = context.Settings;
        context.AddKeys(MenuKeys.All);
        var menuKeys = settings.Live(SettingsMenuBindings.From);
        var editor = new SettingsEditor(context.SettingsStore, settings);
        var settingsMenu = new SettingsMenu(
            () => PlayerSettings.Rows(
                settings.Catalogue, SettingsMenu.KeyRows(context.Keys),
                feature => feature is MenusFeature.SettingsMenu),
            () => settings.Current, editor.Set, settings.Speech(MenusFeature.SettingsMenu, context.Announcer),
            menuKeys, settings.Live(now => MenuBindings.From(now, 0, 0)),
            () => settings.Current.IsOn(MenusFeature.SettingsMenu),
            () => settings.Current.IsOn(SpokenDetail.Positions))
        {
            // The layout line on opening, while tested (#927).
            SayIntro = () => settings.Current.IsOn(MenusFeature.SettingsMenuIntro),
        };
        var menuOpen = ShoulderTaps.MenuOpen(context.Screens, context.Cursors);
        var battle = context.BattleScene;
        var help = new KeyHelp(
            context.Keys, () => settings.Current, settings.Speech(MenusFeature.KeyHelp, context.Announcer), menuKeys,
            () => settingsMenu.IsOpen
                ? KeyPlaces.SettingsMenu
                : (menuOpen() ? KeyPlaces.Menu : KeyPlaces.None) | (battle.InBattle() ? KeyPlaces.Battle : KeyPlaces.None));
        context.AddControls(settings.Input(MenusFeature.KeyHelp, help));
        context.AddControls(settings.Input(MenusFeature.SettingsMenu, settingsMenu));
        context.Provide<IInputBlock>(settingsMenu);
        context.AddShoulderHold(() =>
            settings.Current.IsOn(MenusFeature.SettingsMenu) || settings.Current.IsOn(MenusFeature.KeyHelp)
                ? menuKeys().ChordButtons & ShoulderTaps.Holdable
                : 0);
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
        var speech = context.Settings.Speech(MenusFeature.NameEntry, context.Reader);
        var nameEntry = new NameEntrySpeech(context.Library, speech, context.Cleaner, () => Environment.TickCount64);
        context.Events.ObserveShownLines(nameEntry);
        context.Provide(nameEntry);
        context.Find<TypedNameSource>()?.ReadFrom(nameEntry.LastName);
        // The read-back (#585) shares the menus' details binding and speaks as the name entry does.
        var readBack = new NameReadBack(
            nameEntry.ReadBack, speech, context.Settings.Live(now => MenuBindings.From(now, 0, 0)));
        context.AddControls(context.Settings.Input(MenusFeature.NameReadBack, readBack));
    }
}
