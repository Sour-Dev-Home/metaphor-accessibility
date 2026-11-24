using Metaphor.Core.Announcements;
using Metaphor.Core.Cues;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field.Tests;

/// <summary>The field controls (#181): N and L on the keyboard, LB with D-pad Down, Up and Left on the pad, acting and
/// hidden from the game only on the exact press in free roam. A stand-in field and made-up names only.</summary>
[Trait("Category", "Unit")]
public sealed class FieldControlsTests
{
    private const int KeyN = 0x4E;
    private const int KeyL = 0x4C;
    private const int KeyW = 0x57;
    private const int Lb = 0x0400;
    private const int Up = 0x0010;
    private const int Down = 0x0040;
    private const int Left = 0x0080;
    private const int Right = 0x0020;
    private const int KeyG = 0x47;
    private const int KeyK = 0x4B;
    private const int KeyJ = 0x4A;
    private const int A = 0x4000;

    private readonly RecordingAnnouncer announcer = new();
    private readonly RecordingCues cues = new();
    private readonly StandInField field = new();
    private readonly FieldPlace place = new(new TextCleaner(TextCleanerRules.None));
    private readonly List<string> log = [];
    private readonly FieldControls controls;
    private long now = 1000;

    public FieldControlsTests()
    {
        controls = new FieldControls(FieldBindings.Default, field, place, announcer, cues, () => now, log.Add);
        field.View = new FieldView(0, 0, 0, 0,
        [
            new(FieldKind.Door, 0, 0, 900),
            new(FieldKind.Chest, 0, 0, 200),
            new(FieldKind.Person, 500, 0, 0, "Testa"),
        ]);
    }

    private sealed class StandInField : IFieldReads
    {
        public bool Roaming { get; set; } = true;

        public FieldView? View { get; set; }

        public int? Map { get; set; } = 3;

        public int FreeRoamReads { get; private set; }

        public bool InFreeRoam()
        {
            FreeRoamReads++;
            return Roaming;
        }

        public FieldView? Read() => View;

        public int? MapId() => Map;

        public double? Pitch { get; set; }

        public FieldPose? ReadPose() => View is { } view ? new FieldPose(view.X, view.Y, view.Z, view.Heading, CameraPitch: Pitch) : null;
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    private void Tap(int buttons)
    {
        controls.Pad(0, buttons, made: true);
        controls.Pad(0, Lb, made: true);
    }

    [Fact]
    public void EachKeyboardReadChecksReachWithNoKeyHeld()
    {
        var reach = new ReachCallouts(field, announcer, () => now);
        var withReach = new FieldControls(FieldBindings.Default, field, place, announcer, cues, () => now, reach: reach);

        withReach.Keys([]);

        Assert.Equal(new Announcement("Chest, in reach", Interrupt: false), announcer.Said.Single());
    }

    [Fact]
    public void EachKeyboardReadChecksStunsWithNoKeyHeld()
    {
        field.View = new FieldView(0, 0, 0, 0, [new(FieldKind.Enemy, 0, 0, 400, Stunned: true)]);
        var stuns = new StunCallouts(field, announcer, () => now);
        var withStuns = new FieldControls(FieldBindings.Default, field, place, announcer, cues, () => now, stuns: stuns);

        withStuns.Keys([]);

        Assert.Equal(new Announcement("Enemy stunned", Interrupt: false), announcer.Said.Single());
    }

    [Fact]
    public void NSpeaksTheWholeListAndIsHidden()
    {
        var handed = controls.Keys([KeyW, KeyN]);

        Assert.Equal([KeyW], handed);
        Assert.Equal(
            new Announcement("3 nearby. Chest, 2 steps ahead, in reach. Testa, 5 steps left. Door, 9 steps ahead", Interrupt: true),
            announcer.Said.Single());
    }

    [Fact]
    public void HoldingNSpeaksOnceUntilItIsLetGo()
    {
        controls.Keys([KeyN]);
        controls.Keys([KeyN]);
        controls.Keys([]);
        controls.Keys([KeyN]);

        Assert.Equal(2, announcer.Said.Count);
    }

    [Fact]
    public void LbWithDownStepsThroughTheListAndWraps()
    {
        Tap(Lb | Down);
        Tap(Lb | Down);
        Tap(Lb | Down);
        Tap(Lb | Down);
        Tap(Lb | Up);

        Assert.Equal(
        [
            "1 of 3. Chest, 2 steps ahead, in reach",
            "2 of 3. Testa, 5 steps left",
            "3 of 3. Door, 9 steps ahead",
            "1 of 3. Chest, 2 steps ahead, in reach",
            "3 of 3. Door, 9 steps ahead",
        ], Said());
    }

    [Fact]
    public void AStepAfterAPauseStartsAgainAtTheNearest()
    {
        Tap(Lb | Down);
        Tap(Lb | Down);
        now += FieldControls.StepMemoryMilliseconds + 1;
        Tap(Lb | Down);

        Assert.StartsWith("1 of 3.", Said().Last(), StringComparison.Ordinal);
    }

    [Fact]
    public void UpWithNoListStartsAtTheFarthest()
    {
        Tap(Lb | Up);

        Assert.Equal("3 of 3. Door, 9 steps ahead", Said().Single());
    }

    [Fact]
    public void OnlyTheChordsDpadButtonIsHidden()
    {
        Assert.Equal(Lb | A, controls.Pad(0, Lb | Down | A, made: true));
        Assert.Equal(Down, controls.Pad(0, Down, made: true));
        Assert.Equal(Lb, controls.Pad(0, Lb, made: true));
    }

    [Fact]
    public void OutsideFreeRoamEverythingReachesTheGameAndNothingIsSaid()
    {
        field.Roaming = false;

        Assert.Equal(Lb | Down, controls.Pad(0, Lb | Down, made: true));
        Assert.Equal([KeyN, KeyL], controls.Keys([KeyN, KeyL]));
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ThePressFiresWhenFreeRoamComesWhileHeldOnlyAfterALetGo()
    {
        field.Roaming = false;
        controls.Keys([KeyN]);
        field.Roaming = true;
        controls.Keys([KeyN]);

        Assert.Single(announcer.Said);
    }

    [Fact]
    public void FreeRoamIsReadOnlyWhileABindingIsHeld()
    {
        controls.Pad(0, Lb | A, made: true);
        controls.Keys([KeyW]);
        controls.Pad(0, Lb | Down, made: false);

        Assert.Equal(0, field.FreeRoamReads);
    }

    [Fact]
    public void LAndLbWithLeftSayThePlace()
    {
        place.Observe(new MessageFileCompiled("EN/message/MapName.msg", 4, "@Map_00\n{\nTestshire\n}\n"));
        place.LookedUp(FieldPlace.MapNameEntry, 0, mapId: 3);

        Assert.Equal([], controls.Keys([KeyL]));
        controls.Keys([]);
        Assert.Equal(Lb, controls.Pad(0, Lb | Left, made: true));

        Assert.Equal(["Testshire", "Testshire"], Said());
    }

    [Fact]
    public void WhereAmIAddsTheCurrentMission()
    {
        var started = false;
        var withMission = new FieldControls(
            FieldBindings.Default, field, place, announcer, cues, () => now, mission: Mission(() => started));
        place.Observe(new MessageFileCompiled("EN/message/MapName.msg", 4, "@Map_00\n{\nTestshire\n}\n"));

        withMission.Keys([KeyL]);
        withMission.Keys([]);
        place.LookedUp(FieldPlace.MapNameEntry, 0, mapId: 3);
        started = true;
        withMission.Keys([KeyL]);

        Assert.Equal(["Place not known yet. Mission: Rest", "Testshire. Mission: Reach mock harbour"], Said());
    }

    [Fact]
    public void EachKeyAndPadReadLooksForANewMission()
    {
        var started = false;
        var withMission = new FieldControls(
            FieldBindings.Default, field, place, announcer, cues, () => now, mission: Mission(() => started));

        withMission.Keys([]);
        started = true;
        now += 1000;
        withMission.Pad(0, 0, made: true);
        withMission.Pad(0, 0, made: true);

        Assert.Equal(["Mission: Reach mock harbour"], Said());
    }

    // A mission line whose one made-up mission starts when started says so.
    private MissionLine Mission(Func<bool> started)
    {
        var library = new MessageLibrary();
        library.Compiled("@MissionText_Test_01\n{\nReach mock harbour\n}\n@MissionText_Test_02\n{\nRest\n}\n");
        MainMissionRow[] rows =
        [
            new(0x1165, MessageLibrary.LabelHash("MissionText_Test_02")),
            new(0x1166, MessageLibrary.LabelHash("MissionText_Test_01")),
        ];
        return new MissionLine(
            flag => flag == 0x1165 || (flag == 0x1166 && started()), () => rows, library,
            new TextCleaner(TextCleanerRules.None), announcer, () => now, new FreeRoamGate(field));
    }

    [Fact]
    public void WhereAmILogsTheMainMissionFlagsWhenItCanReadThem()
    {
        HashSet<int> set = [0x1170, 0x1190];
        var withFlags = new FieldControls(FieldBindings.Default, field, place, announcer, cues, () => now, log.Add,
            storyFlag: flag => set.Contains(flag));

        withFlags.Keys([KeyL]);
        controls.Keys([]);
        controls.Keys([KeyL]);

        Assert.EndsWith(" mainFlags=0x1190,0x1170", log[0], StringComparison.Ordinal);
        Assert.DoesNotContain("mainFlags", log[1], StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownPlaceOrUnreadableFieldSaysSo()
    {
        field.View = null;

        controls.Keys([KeyL]);
        controls.Keys([KeyN]);

        Assert.Equal(["Place not known yet", "Can't read the field"], Said());
    }

    [Fact]
    public void NothingNearbySaysSo()
    {
        field.View = new FieldView(0, 0, 0, 0, [new(FieldKind.Enemy, 0, 0, (float)Nearby.Radius * 2)]);

        controls.Keys([KeyN]);

        Assert.Equal("Nothing nearby", Said().Single());
    }

    // Each half of the field controls has its own setting (#661): one off leaves its keys to the game, says nothing,
    // and leaves the other working.
    private static ModSettings Parse(string json) =>
        ModSettings.Parse(json, SettingCatalogue.Of<FieldFeature>(), _ => { });

    // Field controls whose settings are read from the file text, as the field module reads them (#784).
    private FieldControls Gated(string json)
    {
        var settings = Parse(json);
        return new FieldControls(
            () => FieldBindings.Default, feature => settings.IsOn(feature), field, place, announcer, cues, () => now);
    }

    // The camera-tilt line (#828) ends "where am I" only while its testing setting is on. Its warnings speak through
    // their own announcer here (the module gates them by the setting).
    private FieldControls Tilted(string json, IAnnouncer? warnings = null)
    {
        var settings = Parse(json);
        field.Pitch = -Math.PI / 4;
        var tilt = new TiltWarning(field, warnings ?? new RecordingAnnouncer(), () => now, () => TiltLimits.Default);
        return new FieldControls(
            () => FieldBindings.Default, feature => settings.IsOn(feature), field, place, announcer, cues, () => now,
            tilt: tilt);
    }

    [Fact]
    public void WhereAmIEndsWithTheCameraTiltWhenItIsOn()
    {
        Tilted("""{ "field": { "cameraTilt": true } }""").Keys([KeyL]);

        Assert.Equal(["Place not known yet. Camera looking down, 45 degrees"], Said());
    }

    [Fact]
    public void WhereAmILeavesOutTheCameraTiltWhileItIsOff()
    {
        Tilted("""{ "field": { "cameraTilt": false } }""").Keys([KeyL]);

        Assert.Equal(["Place not known yet"], Said());
    }

    [Fact]
    public void EachKeyboardReadChecksTheCameraTilt()
    {
        Tilted("""{ "field": { "cameraTilt": true } }""", announcer).Keys([]);

        Assert.Equal(new Announcement("Camera looking down", Interrupt: false), announcer.Said.Single());
    }

    [Fact]
    public void EachKeyboardReadMovesTheSonarOn()
    {
        var pings = new List<HeldTone>();
        var sonarCues = new PingCues(pings);
        var sonar = new Sonar(field, sonarCues, () => now, () => true);
        var withSonar = new FieldControls(
            FieldBindings.Default, field, place, announcer, cues, () => now, sonar: sonar);

        withSonar.Keys([]);

        Assert.Single(pings);
    }

    [Fact]
    public void AStepInTheListPingsTheRouteToThePlaceAndReadsMoveTheWallsOn()
    {
        var routeCues = new ChannelCues();
        var routePings = new RoutePings(field, routeCues, () => now, () => true, () => RoutePingLimits.Default);
        var walls = new WallTones(field, routeCues, announcer, () => now, () => true, () => WallLimits.Default,
            _ => Task.FromResult<EdgeDistance?>((_, _, _) => 10));
        var withPings = new FieldControls(
            FieldBindings.Default, field, place, announcer, cues, () => now, walls: walls, routePings: routePings);

        withPings.Keys([]);
        Assert.Empty(routeCues.Held(ToneChannel.Route));
        Assert.NotNull(routeCues.Now(ToneChannel.Wall));
        withPings.Keys([KeyK]);

        // The first step is the nearest thing, the chest straight ahead.
        var ping = Assert.Single(routeCues.Held(ToneChannel.Route));
        Assert.Equal(0, ping.Pan, 6);
    }

    private sealed class PingCues(List<HeldTone> pings) : ICuePlayer
    {
        public void Play(Cue cue)
        {
        }

        public void Play(Cue cue, double pan)
        {
        }

        public void Hold(ToneChannel channel, HeldTone tone) => pings.Add(tone);

        public void Silence(ToneChannel channel)
        {
        }
    }

    [Fact]
    public void TheNearbyListTurnedOffLeavesItsKeysToTheGameAndWhereAmIWorking()
    {
        var gated = Gated("""{ "field": { "nearby": false } }""");

        Assert.Equal([KeyN], gated.Keys([KeyN]));
        Assert.Equal(Lb | Down, gated.Pad(0, Lb | Down, made: true));
        Assert.Equal(Lb, gated.Pad(0, Lb | Left, made: true));

        Assert.Equal(["Place not known yet"], Said());
    }

    [Fact]
    public void TheCategoriesTurnedOffLeaveTheirKeysToTheGameAndTheListWorking()
    {
        var gated = Gated("""{ "field": { "nearbyCategories": false } }""");

        Assert.Equal([KeyCloseBracket], gated.Keys([KeyCloseBracket]));
        Assert.Empty(Said());
        gated.Keys([KeyN]);
        Assert.StartsWith("3 nearby.", Said().Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void GuidanceTurnedOffLeavesItsKeysToTheGameAndStartsNoBeacon()
    {
        var gated = Gated("""{ "field": { "guidance": false } }""");

        Assert.Equal([KeyG], gated.Keys([KeyG]));
        Assert.Equal(Lb | Right, gated.Pad(0, Lb | Right, made: true));

        Assert.False(gated.Guiding);
        Assert.Empty(Said());
    }

    [Fact]
    public void WhereAmITurnedOffLeavesItsKeysToTheGameAndTheListWorking()
    {
        var gated = Gated("""{ "field": { "whereAmI": false } }""");

        Assert.Equal([KeyL], gated.Keys([KeyN, KeyL]));
        Assert.Equal(Lb | Left, gated.Pad(0, Lb | Left, made: true));

        Assert.Single(Said());
        Assert.StartsWith("3 nearby.", Said().Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void KeysReboundInTheFileApplyOnTheNextRead()
    {
        var bindings = FieldBindings.Default;
        var rebound = new FieldControls(() => bindings, _ => true, field, place, announcer, cues, () => now);
        bindings = FieldBindings.From(Parse("""{ "keys": { "field": { "nearbyKey": "M" } } }"""));

        Assert.Equal([KeyN], rebound.Keys([KeyN]));
        Assert.Empty(rebound.Keys([0x4D]));
        Assert.StartsWith("3 nearby.", Said().Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheFileNamesEveryFieldBinding()
    {
        var settings = ModSettings.Parse(
            """
            { "keys": { "field": { "nearbyKey": "F1", "placeKey": "F2", "holdButtons": "RB",
              "nextButton": "A", "previousButton": "B", "placeButton": "Y",
              "guideKey": "F3", "guideButton": "X", "nextKey": "F4", "previousKey": "F5",
              "categoryNextKey": "F6", "categoryPreviousKey": "F7", "categoryHoldButtons": "LB",
              "categoryButton": "DpadRight", "identifyKey": "F8" } } }
            """, SettingCatalogue.Of<FieldFeature>(), _ => { });

        Assert.Equal(
            new FieldBindings(
                0x70, 0x71, 0x0800, 0x4000, 0x2000, 0x1000, 0x72, 0x8000, 0x73, 0x74, 0x75, 0x76, 0x0400, 0x0020, 0x77),
            FieldBindings.From(settings));
        Assert.Equal(FieldBindings.Default, FieldBindings.From(ModSettings.Defaults));
    }

    [Fact]
    public void EachPressLogsItsNumbersWithoutNames()
    {
        field.View = field.View! with { CameraHeading = 1.5 };

        controls.Keys([KeyN]);

        Assert.StartsWith("nearby player=(0,0,0) heading=0 camera=1.5 pitch=? map=? doorValue=? units=Person:1,Door:1,Chest:1,Enemy:0", log[0], StringComparison.Ordinal);
        Assert.Equal(4, log.Count);
        Assert.Contains("kind=Chest at=(0,0,200) distance=200 bearing=-1.5 reach=True named=False", log[1], StringComparison.Ordinal);
        Assert.DoesNotContain(log, line => line.Contains("Testa", StringComparison.Ordinal));
    }

    [Fact]
    public void GWithNothingSteppedGuidesToTheNearestNotInReachAndIsHidden()
    {
        var handed = controls.Keys([KeyW, KeyG]);

        Assert.Equal([KeyW], handed);
        // The chest is nearer but already in reach (#183, by ear 2026-11-22: "Reached" at once).
        Assert.Equal(["Guiding to Testa"], Said());
        Assert.True(controls.Guiding);
    }

    [Fact]
    public void GSkipsAPersonAlreadyWithinTheArrivalDistance()
    {
        // People have no reach test, so the arrival distance is what makes one already reached.
        field.View = new FieldView(0, 0, 0, 0,
        [
            new(FieldKind.Person, 0, 0, (float)(Beacon.ArrivalDistance - 30), "Testa"),
            new(FieldKind.Door, 0, 0, 900),
        ]);

        controls.Keys([KeyG]);

        Assert.Equal(["Guiding to Door"], Said());
    }

    [Fact]
    public void GWithEverythingNearAlreadyInReachSaysSo()
    {
        field.View = new FieldView(0, 0, 0, 0,
        [
            new(FieldKind.Person, 0, 0, 100, "Testa"),
            new(FieldKind.Chest, 0, 0, 200),
        ]);

        controls.Keys([KeyG]);

        Assert.Equal(["Already next to Testa"], Said());
        Assert.False(controls.Guiding);
        Assert.Empty(cues.Held);
    }

    [Fact]
    public void GuidingToASteppedThingAlreadyInReachSaysSo()
    {
        Tap(Lb | Down);
        Tap(Lb | Right);

        Assert.Equal("Already next to Chest", Said().Last());
        Assert.False(controls.Guiding);
    }

    [Fact]
    public void KAndJStepThroughTheListLikeTheChordsAndAreHidden()
    {
        Assert.Equal([KeyW], controls.Keys([KeyW, KeyK]));
        controls.Keys([]);
        controls.Keys([KeyK]);
        controls.Keys([]);
        controls.Keys([KeyJ]);
        controls.Keys([]);
        Tap(Lb | Down);

        Assert.Equal(
        [
            "1 of 3. Chest, 2 steps ahead, in reach",
            "2 of 3. Testa, 5 steps left",
            "1 of 3. Chest, 2 steps ahead, in reach",
            "2 of 3. Testa, 5 steps left",
        ], Said());
    }

    [Fact]
    public void KThenGGuidesToTheThingSteppedTo()
    {
        controls.Keys([KeyK]);
        controls.Keys([]);
        controls.Keys([KeyK]);
        controls.Keys([]);
        controls.Keys([KeyK]);
        controls.Keys([]);
        controls.Keys([KeyG]);

        Assert.Equal("Guiding to Door", Said().Last());
    }

    [Fact]
    public void OutsideFreeRoamTheStepKeysReachTheGame()
    {
        field.Roaming = false;

        Assert.Equal([KeyK, KeyJ], controls.Keys([KeyK, KeyJ]));
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void LbWithRightGuidesToTheThingSteppedTo()
    {
        Tap(Lb | Down);
        Tap(Lb | Down);
        var handed = controls.Pad(0, Lb | Right, made: true);

        Assert.Equal(Lb, handed);
        Assert.Equal("Guiding to Testa", Said().Last());
        Assert.Equal(-1, Assert.Single(cues.Held).Pan, 6);
    }

    [Fact]
    public void AStepTooLongAgoGuidesToTheNearestInstead()
    {
        Tap(Lb | Down);
        Tap(Lb | Down);
        now += FieldControls.StepMemoryMilliseconds + 1;
        Tap(Lb | Right);

        Assert.Equal("Guiding to Testa", Said().Last());
    }

    [Fact]
    public void ASecondGuidePressCancels()
    {
        Tap(Lb | Down);
        Tap(Lb | Down);
        Tap(Lb | Right);
        controls.Keys([KeyG]);

        Assert.Equal("Guidance off", Said().Last());
        Assert.Equal(1, cues.Silenced);
    }

    [Fact]
    public void GuidanceTicksOnEveryReadEvenUnmade()
    {
        Tap(Lb | Down);
        Tap(Lb | Down);
        Tap(Lb | Right);
        now += Beacon.IntervalMilliseconds(500);
        controls.Pad(1, 0, made: false);

        Assert.Equal(2, cues.Held.Count);
    }

    [Fact]
    public void OutsideFreeRoamTheGuideKeysReachTheGameAndGuideNothing()
    {
        field.Roaming = false;

        Assert.Equal([KeyG], controls.Keys([KeyG]));
        Assert.Equal(Lb | Right, controls.Pad(0, Lb | Right, made: true));
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void GuidingWithNothingNearbySaysSo()
    {
        field.View = new FieldView(0, 0, 0, 0, []);

        controls.Keys([KeyG]);

        Assert.Equal(["Nothing nearby"], Said());
    }

    private const int KeyOpenBracket = 0xDB;
    private const int KeyCloseBracket = 0xDD;
    private const int KeyI = 0x49;
    private const int Rb = 0x0800;
    private const int Y = 0x1000;

    private void Press(int key)
    {
        controls.Keys([key]);
        controls.Keys([]);
    }

    private static FieldView Mixed() => new(0, 0, 0, 0,
    [
        new(FieldKind.Door, 0, 0, 900),
        new(FieldKind.Chest, 0, 0, 200),
        new(FieldKind.Person, 500, 0, 0, "Testa"),
        new(FieldKind.Door, 0, 0, 300),
        new(FieldKind.Enemy, -700, 0, 0),
    ]);

    [Fact]
    public void CloseBracketCyclesThroughTheCategoriesAndIsHidden()
    {
        field.View = Mixed();

        Assert.Equal([KeyW], controls.Keys([KeyW, KeyCloseBracket]));
        controls.Keys([]);
        for (var i = 0; i < 5; i++)
        {
            Press(KeyCloseBracket);
        }

        Assert.Equal(
        [
            "People, 1 nearby",
            "Doors and exits, 2 nearby",
            "Chests and items, 1 nearby",
            "Enemies, 1 nearby",
            "Examine, none nearby",
            "Everything, 5 nearby",
        ], Said());
    }

    [Fact]
    public void OpenBracketCyclesBackAndWraps()
    {
        field.View = Mixed();

        Press(KeyOpenBracket);
        Press(KeyOpenBracket);

        Assert.Equal(["Examine, none nearby", "Enemies, 1 nearby"], Said());
    }

    [Fact]
    public void InDoorsNListsOnlyDoorsNearestFirst()
    {
        field.View = Mixed();
        Press(KeyCloseBracket);
        Press(KeyCloseBracket);

        Press(KeyN);

        Assert.Equal("Doors and exits, 2 nearby. Door, 3 steps ahead. Door, 9 steps ahead", Said().Last());
    }

    [Fact]
    public void TheStepKeysMoveWithinTheCategory()
    {
        field.View = Mixed();
        Press(KeyCloseBracket);
        Press(KeyCloseBracket);

        Press(KeyK);
        Press(KeyK);
        Press(KeyK);

        Assert.Equal(["1 of 2. Door, 3 steps ahead", "2 of 2. Door, 9 steps ahead", "1 of 2. Door, 3 steps ahead"],
            Said().Skip(2));
    }

    [Fact]
    public void ACategoryChangeStartsTheStepsAgain()
    {
        field.View = Mixed();
        Press(KeyK);
        Press(KeyK);
        Press(KeyCloseBracket);

        Press(KeyK);

        Assert.Equal("1 of 1. Testa, 5 steps left", Said().Last());
    }

    [Fact]
    public void GGuidesToTheNearestInTheCategory()
    {
        field.View = Mixed();
        Press(KeyOpenBracket);
        Press(KeyOpenBracket);

        Press(KeyG);

        // Everything's nearest not reached is the door 3 steps ahead.
        Assert.Equal("Guiding to Enemy", Said().Last());
    }

    [Fact]
    public void TheCategoryKeysReachTheGameOutsideFreeRoam()
    {
        field.Roaming = false;

        Assert.Equal([KeyOpenBracket, KeyCloseBracket], controls.Keys([KeyOpenBracket, KeyCloseBracket]));
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void TheControllerCategoryChordIsUnboundUntilTheRbLayer()
    {
        Assert.Equal(0, FieldBindings.Default.CategoryHoldButtons);
        Assert.Equal(0, FieldBindings.Default.CategoryButton);
        Assert.Equal(Rb | Y, controls.Pad(0, Rb | Y, made: true));
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ABoundCategoryChordCyclesForwardAndHidesItsButton()
    {
        var bound = new FieldControls(FieldBindings.Default with { CategoryHoldButtons = Rb, CategoryButton = Y },
            field, place, announcer, cues, () => now);
        field.View = Mixed();

        Assert.Equal(Rb, bound.Pad(0, Rb | Y, made: true));
        bound.Pad(0, Rb, made: true);

        Assert.Equal(["People, 1 nearby"], Said());
    }

    [Fact]
    public void ANamesTableRowNamesTheThingAndMovesItToItsCategory()
    {
        var table = NamesTable.From([NamesTable.Parse(
            """{ "map": 3, "objects": { "npc-2-17": { "name": "Display case", "kind": "examine" } } }""", "3.json")]);
        var named = new FieldControls(FieldBindings.Default, field, place, announcer, cues, () => now, names: table);
        field.View = new FieldView(0, 0, 0, 0, [new(FieldKind.Person, 0, 0, 200, Id: "npc-2-17")], MapId: 3);

        named.Keys([KeyOpenBracket]);
        named.Keys([]);
        named.Keys([KeyN]);

        Assert.Equal(["Examine, 1 nearby", "Examine, 1 nearby. Display case, 2 steps ahead"], Said());
    }

    [Fact]
    public void IInADebugBuildLogsTheNearestThingsMapIdKindAndPosition()
    {
        field.View = new FieldView(0, 0, 0, 0,
            [new(FieldKind.Person, 30, 5, 200, "Testa", "npcKind=2 model=401", "npc-2-17")], MapId: 3);

        Assert.Equal([], controls.Keys([KeyI]));

        var line = Assert.Single(log, line => line.StartsWith("identify ", StringComparison.Ordinal));
        Assert.Equal("identify map=3 id=npc-2-17 list=Person kind=Person at=(30,5,200) npcKind=2 model=401", line);
        Assert.Equal("Logged npc-2-17, Testa, 2 steps ahead", Said().Single());
    }

    [Fact]
    public void IWithNothingNearbySaysSo()
    {
        field.View = new FieldView(0, 0, 0, 0, [], MapId: 3);

        controls.Keys([KeyI]);

        Assert.Equal(["Nothing nearby"], Said());
    }

    [Fact]
    public void IOutsideADebugBuildReachesTheGame()
    {
        var release = new FieldControls(FieldBindings.Default, field, place, announcer, cues, () => now);

        Assert.Equal([KeyI], release.Keys([KeyI]));
        Assert.Empty(announcer.Said);
    }
}
