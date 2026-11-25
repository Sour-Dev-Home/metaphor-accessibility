using System.Reflection;
using System.Text.Json.Nodes;
using Metaphor.Composition;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Cues;
using Metaphor.Core.Features;
using Metaphor.Core.Input;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;
using Metaphor.Features.Battle;
using Metaphor.Features.Field;
using Metaphor.Features.Menus;

namespace Metaphor.Settings.Tests;

/// <summary>Every feature can be turned off (#661, the maintainer's decision of 2026-11-24, part 2): each module,
/// registered alone with every one of its settings off, is fed a canned event of each kind its observers hear (message
/// files and lines, choices, menu cursors, layout animations, battle hits, enemies and targets, banners, popups and
/// every key, pad button, chord and stick push, in a battle and in free roam), and says nothing and plays no cue. A
/// feature that speaks without its setting's gate fails here. Made-up messages, labels and positions.</summary>
[Trait("Category", "Unit")]
public sealed class EveryFeatureOffTests
{
    private const string Messages = """
        @MES_q
        {
        <NS>Ada<NE>
        Which way?
        <WAIT>
        }

        @MES_c SEL 0
        {
        <SEL_ITEM>North

        South
        <WAIT>
        }
        """;

    public static TheoryData<string> Modules =>
        [.. FeatureModules.Create().Select(module => module.GetType().Name)];

    [Theory]
    [MemberData(nameof(Modules))]
    public void WithEveryOneOfItsSettingsOffAModuleSaysNothingAndPlaysNoCue(string name)
    {
        var module = FeatureModules.Create().Single(each => each.GetType().Name == name);
        var world = new World();
        var context = world.Register(module);

        Feed(context, world);

        Assert.Empty(world.Announcer.Said.Select(said => said.Text));
        Assert.Equal(0, world.Cues.Played);
    }

    // The same feed with every setting on, testing ones included, does reach the features, so the silence above is the
    // settings' doing and not a feed that misses them.
    [Theory]
    [InlineData("MenusModule")]
    [InlineData("BattleModule")]
    [InlineData("DialogueModule")]
    [InlineData("FieldModule")]
    public void WithItsSettingsOnTheSameFeedMakesTheModuleSpeakOrPlay(string name)
    {
        var module = FeatureModules.Create().Single(each => each.GetType().Name == name);
        var world = new World();
        var context = world.Register(module, allOff: false);

        Feed(context, world);

        Assert.True(world.Announcer.Said.Count > 0 || world.Cues.Played > 0);
    }

    // The settings file with every setting of the module off.
    private static string AllOff(IFeatureModule module)
    {
        var root = new JsonObject();
        foreach (var feature in module.Settings)
        {
            var key = SettingKey.Of(feature);
            if (root[key.Group] is not JsonObject group)
            {
                group = [];
                root[key.Group] = group;
            }
            group[key.Name] = false;
        }
        return root.ToJsonString();
    }

    private static void Feed(FeatureContext context, World world)
    {
        // In a battle, roaming the field, with a dialogue window open, and with a menu open.
        foreach (var phase in Enumerable.Range(0, 4))
        {
            world.Battle.On = phase == 0;
            world.Field.Roaming = phase == 1;
            world.Game.Window = phase == 2 ? 0x1000 : null;
            world.Game.Menus = phase == 3;
            context.Screens.MenusRead(phase == 3);
            Events(context);
            Ports(context);
            Layouts(context);
            Controls(context, world);
        }
    }

    private static void Events(FeatureContext context)
    {
        var events = context.Events;
        events.Compiled(new MessageFileCompiled("event/e100.msg", 7, Messages));
        events.Requested(new LineRequested(0, "MES_q"));
        events.Shown(new LineShown(0, Hash("MES_q")));
        events.ChoiceOpened(new ChoiceRequested(0, "MES_q", "MES_c"));
        events.SelectOpened(new SelectRequested(0, "MES_q", "MES_c"));
        foreach (var screen in KnownScreens.All)
        {
            foreach (var row in new[] { 0, 1, 0 })
            {
                var input = new CursorInput(1, null, 0, 0, 0, row, 0, screen.ScreenClass, Count: 3);
                context.Screens.Observe(input);
                context.Cursors.Handled(input);
            }
        }
    }

    private static void Ports(FeatureContext context)
    {
        context.Find<IBattleEnemyObserver>()?.Enemies(1, [0xa0, 0xb0]);
        context.Find<IBattleHitObserver>()?.Observe(new BattleHit(0xa0, -10, 50, 1, BattleSide.Enemy, HpMax: 60));
        context.Find<IBattleHitObserver>()?.Observe(new BattleHit(0xc0, -10, 50, 1, BattleSide.Ally, Slot: 0));
        context.Find<IBattleTargetObserver>()?.Focused(1, 0);
        for (var kind = 0; kind < 8; kind++)
        {
            context.Find<BattleBanners>()?.Shown(kind, 1);
        }
        context.Find<TutorialPopups>()?.WindowUpdated(1, 1, 1, 0, ended: false);
        context.Find<NameEntrySpeech>()?.Observe(new LineShown(0, Hash("MES_q")));
        context.Find<FieldMapName>()?.Composed(1, "Grand Trad");
        context.Find<CornerAdvice>()?.Opened("MES_q", Hash("MES_q"));
        context.Find<TravelMapDestination>()?.Shown(new MessageReference(7, 0), null);
        context.Find<IPadWatcher>()?.Pad(0, 0x4000, made: true);
    }

    // Every layout an observer asks for, with every animation number up to a few thousand.
    private static void Layouts(FeatureContext context)
    {
        var observers = (List<ILayoutAnimationObserver>)typeof(LayoutAnimations)
            .GetField("observers", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(context.Layouts)!;
        foreach (var layout in observers.SelectMany(observer => observer.Layouts).Distinct().ToList())
        {
            for (var animation = 0; animation < 6000; animation++)
            {
                context.Layouts.Played(new LayoutAnimation(layout, animation));
            }
        }
    }

    // Every key alone and with Ctrl; every pad button alone and under each hold; every stick push under each hold.
    private static void Controls(FeatureContext context, World world)
    {
        var now = 0L;
        var frame = new InputFrame(
            world.Game, context.Screens, [.. context.Controls], null, context.Find<IPadWatcher>(), null,
            taps: new ShoulderTaps(context.ShoulderHolds, () => now += 20));
        for (var key = 8; key <= 0xEF; key++)
        {
            frame.Keys([key]);
            frame.Keys([]);
            frame.Keys([0x11, key]);
            frame.Keys([]);
        }
        int[] holds = [0, 0x0400, 0x0800, 0x0400 | 0x0800, 0x0100 | 0x0200, 0x0002];
        foreach (var hold in holds)
        {
            for (var bit = 0; bit < 16; bit++)
            {
                frame.Pad(0, hold, made: true);
                frame.Pad(0, hold | (1 << bit), made: true);
                frame.Pad(0, hold, made: true);
                frame.Pad(0, 0, made: true);
            }
            foreach (var (x, y) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0) })
            {
                var pushed = PadSticks.Rest with
                {
                    RightX = PadSticks.Centre + (x * 127),
                    RightY = PadSticks.Centre - (y * 127),
                    LeftX = PadSticks.Centre + (x * 127),
                    LeftY = PadSticks.Centre - (y * 127),
                };
                frame.Sticks(0, hold, pushed, made: true);
                frame.Sticks(0, hold, PadSticks.Rest, made: true);
            }
        }
    }

    private static string Hash(string label) => MessageLibrary.LabelHash(label);

    /// <summary>A game in a battle or in free roam, with something to read on every port.</summary>
    private sealed class World
    {
        public RecordingAnnouncer Announcer { get; } = new();

        public CountingCues Cues { get; } = new();

        public Scene Battle { get; } = new();

        public LiveField Field { get; } = new();

        public Game Game { get; } = new();

        // The module alone, composed as the mod composes it (ADR 0026) over this world's reads, with every one of its
        // settings off before it registers, as a settings file read at start would be.
        public FeatureContext Register(IFeatureModule module, bool allOff = true)
        {
            var settings = new SettingsReloader(Announcer, SettingCatalogue.From([module]), _ => { }, testingOn: true);
            settings.Load(allOff ? AllOff(module) : "{}");
            var reads = new GameReads
            {
                Names = new NoNames(),
                BattleParty = new Party(),
                Purse = new Purse(),
                BattleScene = Battle,
                Field = _ => Field,
                MissionRows = new MissionRows(() => null),
                GameDevice = () => null,
                VoiceLanguage = () => VoiceLanguage.Unknown,
                StoryFlag = _ => true,
                OpenArchive = () => throw new FileNotFoundException("no archive in a test"),
            };
            return ModComposition.Compose(
                [module], reads, new StoppableAnnouncer(Announcer, () => { }), Cues, settings, report: null, log: null,
                modFolder: Path.Combine(Path.GetTempPath(), "metaphor-every-feature-off-no-such-folder")).Context;
        }
    }

    private sealed class CountingCues : ICuePlayer
    {
        public int Played { get; private set; }

        public void Play(Cue cue) => Played++;

        public void Play(Cue cue, double pan) => Played++;

        public void Hold(ToneChannel channel, HeldTone tone) => Played++;

        public void Silence(ToneChannel channel)
        {
        }
    }

    private sealed class Game : IGameInputState
    {
        public long? Window { get; set; }

        public bool Menus { get; set; }

        public long? DialogueWindow() => Window;

        public bool MenusOpen() => Menus;
    }

    private sealed class Scene : IBattleScene
    {
        public bool On { get; set; }

        public bool InBattle() => On;
    }

    private sealed class LiveField : IFieldReads
    {
        public bool Roaming { get; set; }

        public bool InFreeRoam() => Roaming;

        public FieldView? Read() => new(0, 0, 0, 0, [
            new FieldUnit(FieldKind.Door, 0, 0, 120), new FieldUnit(FieldKind.Chest, 300, 0, 0),
            new FieldUnit(FieldKind.Enemy, -400, 0, 0, Hp: 10), new FieldUnit(FieldKind.Person, 0, 0, -60),
        ], DoorReachValue: 1, MapId: 7, CameraHeading: 0, CameraPitch: -1.2);

        public int? MapId() => 7;

        public FieldPose? ReadPose() => Read()?.Pose;
    }

    private sealed class Party : IBattleParty
    {
        public IReadOnlyList<PartyMember> Members() => [new PartyMember(0, 3, 89, 120, 30)];
    }

    private sealed class Purse : IBattlePurse
    {
        public int? Money() => 100;

        public int? Mag() => 5;
    }

    private sealed class NoNames : IGameNameMemory
    {
        public byte[]? CharacterEntry(int character) => null;

        public int? FollowerCharacter(int follower) => null;

        public bool? StoryFlag(int flag) => null;

        public byte[]? TypedName(TypedName name) => null;
    }
}
