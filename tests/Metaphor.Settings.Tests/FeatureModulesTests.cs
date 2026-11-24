using Metaphor.Core;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Cues;
using Metaphor.Core.Features;
using Metaphor.Core.Input;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;
using Metaphor.Features.Battle;
using Metaphor.Features.Dialogue;
using Metaphor.Features.Field;
using Metaphor.Features.Menus;
using Metaphor.Features.Updates;

namespace Metaphor.Settings.Tests;

/// <summary>The feature modules (#784, ADR 0022), registered in the hook layer's order over one context: each wires
/// its features and leaves the hooks' ports, and the controls take each read in the order they did before the move.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FeatureModulesTests
{
    // The hook layer's list (Metaphor.Hooks.FeatureModules), which these plain tests can't reference; the
    // architecture tests pin that list to this order.
    private static IFeatureModule[] InHookOrder() =>
        [new MenusModule(), new BattleModule(), new DialogueModule(), new NameEntryModule(), new FieldModule(),
            new UpdatesModule()];

    [Fact]
    public void EveryModuleRegistersOverOneContextAndLeavesTheHooksPorts()
    {
        var context = Register(withReport: true);

        Assert.NotNull(context.Find<TravelMapDestination>());
        Assert.NotNull(context.Find<FieldMapName>());
        Assert.NotNull(context.Find<TutorialPopups>());
        Assert.NotNull(context.Find<PopupOnScreen>());
        Assert.NotNull(context.Find<NameEntrySpeech>());
        Assert.NotNull(context.Find<IPadWatcher>());
        Assert.NotNull(context.Find<IBattleHitObserver>());
        Assert.NotNull(context.Find<BattleBanners>());
        // The target cursor's one observer passes each report on to the target line (#253).
        Assert.IsType<TargetLine>(Assert.IsType<TargetedEnemy>(context.Find<IBattleTargetObserver>()).Then);
        Assert.NotNull(context.Find<FieldPlace>());
        Assert.NotNull(context.Find<FieldTablesSource>());
        // The report-open hook's corner advice (#587), and the autosave notice's layout (#543).
        Assert.NotNull(context.Find<CornerAdvice>());
        Assert.True(context.Layouts.Wants(AutosaveNotice.Layout));
    }

    // Before #784 the input frame took the mark key, the menus, the battle status key, the enemy HP key, the dialogue
    // and the field, in that order (InputHooks.Frame). The mark key and the field controls aren't gated, so their
    // types show; the four between are gated by their settings.
    [Fact]
    public void ControlsTakeEachReadInTheOrderTheyDidBefore()
    {
        // Each module's own controls, in its own order, with no count of anyone else's (#892): a new control in one
        // module changes only that module's list.
        var registered = Registered(withReport: true);
        var controls = registered.Context.Controls;

        // The list is the modules' lists end to end, in the hook layer's order.
        Assert.Equal(
            InHookOrder().SelectMany(module => registered.Added[module.GetType()]),
            controls);
        // The menus' module starts with the mark key, which is not gated, so before every other control.
        Assert.IsType<MarkKey>(registered.Added[typeof(MenusModule)][0]);
        // The field's controls end the field module's, with the enemy awareness cues, which only watch the reads,
        // after them; the field takes its read after the mark key.
        var field = registered.Added[typeof(FieldModule)];
        Assert.IsType<FieldControls>(field[^2]);
        Assert.IsType<EnemyAwareness>(field[^1]);
        Assert.True(controls.ToList().FindIndex(control => control is MarkKey)
            < controls.ToList().FindIndex(control => control is FieldControls));
    }

    // Only the mark key needs the report: without one that module has one control fewer and no other module changes.
    [Fact]
    public void WithoutAReportOnlyTheMarkKeyIsMissing()
    {
        var with = Registered(withReport: true);
        var without = Registered(withReport: false);

        Assert.Single(with.Context.Controls.OfType<MarkKey>());
        Assert.Empty(without.Context.Controls.OfType<MarkKey>());
        foreach (var module in InHookOrder())
        {
            var type = module.GetType();
            var missing = type == typeof(MenusModule) ? 1 : 0;
            Assert.Equal(with.Added[type].Count - missing, without.Added[type].Count);
        }
    }

    // #754: the settings menu blocks the game's input through the frame, and holds the triggers for its chord only
    // while it, or the help, is on.
    [Fact]
    public void TheSettingsMenuIsTheInputBlockAndHoldsTheTriggersOnlyWhenOn()
    {
        var context = Register(withReport: false);

        Assert.IsType<SettingsMenu>(context.Find<IInputBlock>());
        var holds = () => context.ShoulderHolds.Aggregate(0, (held, hold) => held | hold());
        Assert.Equal(0, holds() & ShoulderTaps.Triggers);
        context.Settings.Load("""{ "speech": { "settingsMenu": true } }""");
        Assert.Equal(ShoulderTaps.Triggers, holds() & ShoulderTaps.Triggers);
    }

    // #868: in each place, the help says every listed key that works there, as bound now.
    [Theory]
    [InlineData(KeyPlaces.Dialogue)]
    [InlineData(KeyPlaces.Menu)]
    [InlineData(KeyPlaces.Field)]
    [InlineData(KeyPlaces.Battle)]
    [InlineData(KeyPlaces.SettingsMenu)]
    public void TheHelpSaysEveryKeyOfEachPlace(KeyPlaces place)
    {
        var context = Register(withReport: true);
        var testing = context.Settings.Current.WithTesting(true);
        var help = new KeyHelp(
            context.Keys, () => testing, new RecordingAnnouncer(), () => SettingsMenuBindings.Default, () => place);
        var where = place == KeyPlaces.SettingsMenu ? place : place | KeyPlaces.Anywhere;

        var line = help.Line(where);

        var expected = context.Keys.Where(entry => (entry.Places & where) != 0 && !entry.Key.Hidden).ToList();
        Assert.NotEmpty(expected);
        foreach (var entry in expected)
        {
            Assert.Contains(entry.Key.Said(testing)!, line, StringComparison.Ordinal);
        }
    }

    // #868: every module lists its keys, so the help says each project's.
    [Fact]
    public void EveryModuleListsItsKeysForTheHelp()
    {
        var keys = Register(withReport: false).Keys.SelectMany(entry => entry.Key.Bindings).Select(key => key.Group)
            .ToHashSet();

        Assert.Superset(new HashSet<string> { "menus", "report", "dialogue", "battle", "field" }, keys);
    }

    // #166: with the hook layer's purse, the battle's rewards take the reads as a clock, gated.
    [Fact]
    public void ThePurseAddsTheBattleRewardsControls()
    {
        var without = Registered(withReport: true);
        var with = Registered(withReport: true, purse: new NoPurse());

        Assert.Equal(without.Added[typeof(BattleModule)].Count + 1, with.Added[typeof(BattleModule)].Count);
        foreach (var module in InHookOrder().Where(module => module is not BattleModule))
        {
            Assert.Equal(without.Added[module.GetType()].Count, with.Added[module.GetType()].Count);
        }
    }

    // #809, by ear 2026-11-23 evening: the skip menu's animation 4768 is its close, so the question was said as the
    // player left. Nothing asks for that layout's animations; the question hears the component's show request instead.
    [Fact]
    public void NoFeatureHearsTheSkipMenusClose()
    {
        var context = Register(withReport: false);

        Assert.False(context.Layouts.Wants(SkipSceneQuestion.Layout));
    }

    [Fact]
    public void TheSkipQuestionHearsTheSkipMenusShowRequest()
    {
        var context = Register(withReport: false);

        // Testing and off by default: the hook reads nothing (#864); on, it is wanted.
        Assert.False(context.MoviePause.Any);
        context.Settings.Load("""{ "speech": { "skipSceneQuestion": true } }""");
        Assert.True(context.MoviePause.Any);
    }

    [Fact]
    public void OnePortOfEachTypeAndNoneUntilProvided()
    {
        var context = Register(withReport: false);

        Assert.Throws<InvalidOperationException>(() => context.Provide(new MissionRows(() => null)));
        Assert.Null(context.Find<StringWriter>());
    }

    [Fact]
    public void TheModulesInTheHookListAreEveryModule()
    {
        var listed = InHookOrder().Select(module => module.GetType()).Order(new ByName()).ToList();

        Assert.Equal(SettingsReference.AllFeatures.Modules().Select(module => module.GetType()), listed);
    }

    private static FeatureContext Register(bool withReport, IBattlePurse? purse = null) =>
        Registered(withReport, purse).Context;

    /// <summary>The context after every module registered, and the controls each module added to it.</summary>
    private sealed record Registration(FeatureContext Context, Dictionary<Type, List<IInputControls>> Added);

    private static Registration Registered(bool withReport, IBattlePurse? purse = null)
    {
        var announcer = new RecordingAnnouncer();
        var library = new MessageLibrary();
        var context = new FeatureContext
        {
            Settings = new SettingsReloader(
                announcer, SettingCatalogue.From(InHookOrder()), _ => { }),
            Announcer = announcer,
            Stoppable = new StoppableAnnouncer(announcer, () => { }),
            Reader = new DialogueReader(announcer),
            Cues = new SilentCuePlayer(),
            Report = withReport ? new ReportLog(new StringWriter(), () => 0, []) : null,
            Log = null,
            ModFolder = Path.Combine(Path.GetTempPath(), "metaphor-settings-tests-no-such-folder"),
            Events = new MessageEvents(),
            Cursors = new CursorEvents(),
            Screens = new MenuScreens(KnownScreens.All),
            Library = library,
            Serials = new MessageSerials([]),
            FilesById = new MessageFilesById(),
            FilePositions = new MessageFilePositions([]),
            History = new DialogueHistory(),
            Names = new GameNames(new NoNames(), library),
            Cleaner = new TextCleaner(TextCleanerRules.None),
            LastDevice = new LastInputDevice(),
            Tutorials = new TutorialTableSource(),
            BattleParty = new NoParty(),
            BattleScene = new NoBattle(),
            VoiceLanguage = () => VoiceLanguage.Unknown,
            StoryFlag = _ => null,
            OpenArchive = () => throw new FileNotFoundException("no archive in a test"),
        };
        context.Provide<IFieldReads>(new NoField());
        context.Provide(new MissionRows(() => null));
        if (purse is not null)
        {
            context.Provide(purse);
        }
        var added = new Dictionary<Type, List<IInputControls>>();
        foreach (var module in InHookOrder())
        {
            var before = context.Controls.Count;
            module.Register(context);
            added[module.GetType()] = [.. context.Controls.Skip(before)];
        }
        return new(context, added);
    }

    private sealed class ByName : IComparer<Type>
    {
        public int Compare(Type? x, Type? y) => string.CompareOrdinal(x?.FullName, y?.FullName);
    }

    private sealed class NoNames : IGameNameMemory
    {
        public byte[]? CharacterEntry(int character) => null;

        public int? FollowerCharacter(int follower) => null;

        public bool? StoryFlag(int flag) => null;

        public byte[]? TypedName(TypedName name) => null;
    }

    private sealed class NoParty : IBattleParty
    {
        public IReadOnlyList<PartyMember> Members() => [];
    }

    private sealed class NoPurse : IBattlePurse
    {
        public int? Money() => null;

        public int? Mag() => null;
    }

    private sealed class NoBattle : IBattleScene
    {
        public bool InBattle() => false;
    }

    private sealed class NoField : IFieldReads
    {
        public bool InFreeRoam() => false;

        public FieldView? Read() => null;

        public int? MapId() => null;

        public FieldPose? ReadPose() => null;
    }
}
