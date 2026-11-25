using Metaphor.Composition;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Cues;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;
using Metaphor.Features.Battle;
using Metaphor.Features.Dialogue;
using Metaphor.Features.Field;
using Metaphor.Features.Menus;
using Metaphor.Features.Updates;

namespace Metaphor.Settings.Tests;

/// <summary>The plain composition (ADR 0026, #726): the whole mod composed over fake game reads, with no game and no
/// hook layer. Made-up reads only.</summary>
[Trait("Category", "Unit")]
public sealed class ModCompositionTests
{
    [Fact]
    public void TheModuleListKeepsItsOrder() =>
        Assert.Equal(
            [typeof(MenusModule), typeof(BattleModule), typeof(DialogueModule), typeof(NameEntryModule),
                typeof(FieldModule), typeof(UpdatesModule)],
            FeatureModules.Create().Select(module => module.GetType()));

    [Fact]
    public void ComposedOverFakeReadsEveryPortTheHooksCallOnIsProvided()
    {
        var composed = Compose(new RecordingAnnouncer());
        var context = composed.Context;

        Assert.NotNull(context.Find<TravelMapDestination>());
        Assert.NotNull(context.Find<FieldMapName>());
        Assert.NotNull(context.Find<TutorialPopups>());
        Assert.NotNull(context.Find<IBattleHitObserver>());
        Assert.NotNull(context.Find<IBattleEnemyObserver>());
        Assert.NotNull(context.Find<IBattleTargetObserver>());
        Assert.NotNull(context.Find<BattleBanners>());
        Assert.NotNull(context.Find<NameEntrySpeech>());
        Assert.NotEmpty(context.Controls);
        Assert.NotEmpty(context.Keys);
        Assert.Same(context.Find<IFieldReads>(), composed.Field);
    }

    [Fact]
    public void ADialogueLineTheHooksReportIsSpokenAndKeptInTheHistory()
    {
        var announcer = new RecordingAnnouncer();
        var composed = Compose(announcer);

        composed.Events.Compiled(new MessageFileCompiled("event/e100.msg", 7, """
            @MES_a
            {
            <NS>Ada<NE>
            The gate is open.
            <WAIT>
            }
            """));
        composed.Events.Requested(new LineRequested(0, "MES_a"));

        Assert.Contains(announcer.Said, said => said.Text.Contains("The gate is open.", StringComparison.Ordinal));
    }

    internal static ComposedMod Compose(IAnnouncer announcer, ICuePlayer? cues = null, string? settingsFile = null)
    {
        var modules = FeatureModules.Create();
        var settings = new SettingsReloader(announcer, SettingCatalogue.From(modules), _ => { });
        settings.Load(settingsFile ?? "{}");
        return ModComposition.Compose(
            modules, FakeReads.Make(), new StoppableAnnouncer(announcer, () => { }), cues ?? new SilentCuePlayer(),
            settings, report: null, log: null,
            modFolder: Path.Combine(Path.GetTempPath(), "metaphor-composition-test-no-such-folder"));
    }
}

/// <summary>Game reads with nothing behind them: no battle, no field, no names.</summary>
internal static class FakeReads
{
    public static GameReads Make(IBattleScene? scene = null, IFieldReads? field = null, IBattleParty? party = null) =>
        new()
        {
            Names = new NoNames(),
            BattleParty = party ?? new NoParty(),
            BattleScene = scene ?? new NoBattle(),
            Field = _ => field ?? new NoField(),
            MissionRows = new MissionRows(() => null),
            GameDevice = () => null,
            VoiceLanguage = () => VoiceLanguage.Unknown,
            StoryFlag = _ => null,
            OpenArchive = () => throw new FileNotFoundException("no archive in a test"),
        };

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
