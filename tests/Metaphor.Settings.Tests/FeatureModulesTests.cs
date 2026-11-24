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
        Assert.NotNull(context.Find<FieldPlace>());
        Assert.NotNull(context.Find<FieldTablesSource>());
    }

    // Before #784 the input frame took the mark key, the menus, the battle status key, the enemy HP key, the dialogue
    // and the field, in that order (InputHooks.Frame). The mark key and the field controls aren't gated, so their
    // types show; the four between are gated by their settings.
    [Fact]
    public void ControlsTakeEachReadInTheOrderTheyDidBefore()
    {
        var controls = Register(withReport: true).Controls;

        // Seven since the battle's end check (#750), gated, before the battle keys; it only watches the reads. Eight
        // since the enemy HP keys split into the keyboard's and the pad's, each its own testing feature (#767). Nine
        // since the name entry's read-back (#585), gated, before the field.
        Assert.Equal(9, controls.Count);
        Assert.IsType<MarkKey>(controls[0]);
        Assert.IsType<FieldControls>(controls[^1]);
        Assert.Equal(8, Register(withReport: false).Controls.Count);
    }

    // #809, by ear 2026-11-23 evening: the skip menu's animation 4768 is its close, so the question was said as the
    // player left. Nothing asks for that layout's animations until an open signal is found.
    [Fact]
    public void NoFeatureHearsTheSkipMenusClose()
    {
        var context = Register(withReport: false);

        Assert.False(context.Layouts.Wants(SkipSceneQuestion.Layout));
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

    private static FeatureContext Register(bool withReport)
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
        foreach (var module in InHookOrder())
        {
            module.Register(context);
        }
        return context;
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
