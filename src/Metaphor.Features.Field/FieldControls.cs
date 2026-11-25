using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Cues;
using Metaphor.Core.Input;
using Metaphor.Core.Navigation;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>Which keys and buttons the field controls (#181) use. Buttons are bits of the game's own 16-bit pad mask;
/// keys are Windows virtual-key codes.</summary>
/// <param name="NearbyKey">The key that speaks the whole nearby list.</param>
/// <param name="PlaceKey">The key that says where the player is.</param>
/// <param name="HoldButtons">The button held for the pad's field chords.</param>
/// <param name="NextButton">With <paramref name="HoldButtons"/>: the next thing on the list.</param>
/// <param name="PreviousButton">With <paramref name="HoldButtons"/>: the thing before.</param>
/// <param name="PlaceButton">With <paramref name="HoldButtons"/>: where the player is.</param>
/// <param name="GuideKey">The key that starts the beacon toward the list's current thing (#183), or stops it.</param>
/// <param name="GuideButton">With <paramref name="HoldButtons"/>: as <paramref name="GuideKey"/>.</param>
/// <param name="NextKey">As <paramref name="NextButton"/>, on the keyboard.</param>
/// <param name="PreviousKey">As <paramref name="PreviousButton"/>, on the keyboard.</param>
/// <param name="CategoryNextKey">The key that moves the list to the next category (#697).</param>
/// <param name="CategoryPreviousKey">The key that moves it to the category before.</param>
/// <param name="CategoryHoldButtons">The button held for the pad's category chord, or 0 for none.</param>
/// <param name="CategoryButton">With <paramref name="CategoryHoldButtons"/>: the next category, or 0 for none.</param>
/// <param name="IdentifyKey">In Debug builds, the key that logs the nearest thing's map, id, kind fields and position
/// (#687).</param>
/// <param name="ObjectiveKey">The key that starts the beacon toward the current objective (#652).</param>
/// <param name="ObjectiveButton">With <paramref name="HoldButtons"/>: as <paramref name="ObjectiveKey"/>, or 0 for none.
/// </param>
public sealed record FieldBindings(
    int NearbyKey, int PlaceKey, int HoldButtons, int NextButton, int PreviousButton, int PlaceButton, int GuideKey,
    int GuideButton, int NextKey, int PreviousKey, int CategoryNextKey = 0xDD, int CategoryPreviousKey = 0xDB,
    int CategoryHoldButtons = 0, int CategoryButton = 0, int IdentifyKey = 0x49, int ObjectiveKey = 0x4F,
    int ObjectiveButton = 0)
{
    // O (0x4F) for the objective key (#652): free among the mod's keys, and within the game's keyboard read of 8 to
    // 0xEF (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md); whether the game's keyboard map uses O is
    // [NEEDS VERIFICATION], as for J and K. The LB chord's four D-pad buttons are taken, so the pad has none by default.
    // The category keys, the maintainer's decision of 2026-11-22 on #697: [ (VK_OEM_4, 0xDB) and ] (VK_OEM_6, 0xDD) on
    // a US layout, within the game's keyboard read of 8 to 0xEF (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md).
    // The pad's RB + Y (0x0800 and 0x1000 in the game's mask, same source) waits for the RB layer (#688) and the live
    // bindings (#668), neither merged yet, so the chord is unbound by default. I (0x49) for the Debug identify key:
    // whether the game's keyboard map uses I is [NEEDS VERIFICATION], as for J and K.
    /// <summary>N and L, and LB with the D-pad's Down, Up and Left: the maintainer's choices on #181 (2026-11-21). G and
    /// LB with the D-pad's Right for the beacon (#183): the maintainer's choice on PR #667 (2026-11-21). K and J step
    /// the list from the keyboard (by ear 2026-11-22 on PR #667: the keyboard had no step), proposed: J and K sit by L as
    /// the cycle keys of FF6ScreenReader's <c>J</c>/<c>L</c> (docs/raw-sources/field-navigation-accessibility-mods-2026-11-16.md),
    /// and keep off the number row. Whether the game's own keyboard map uses J or K is [NEEDS VERIFICATION]: no source
    /// for it is saved. The game's mask puts LB at <c>0x0400</c> and the D-pad's Up, Right, Down and Left at
    /// <c>0x0010</c>, <c>0x0020</c>, <c>0x0040</c> and <c>0x0080</c> on game build 548ddc95… (Steam build 18330018),
    /// and its keyboard read lists any held key from 8 to <c>0xEF</c>, N (<c>0x4E</c>), L (<c>0x4C</c>), G
    /// (<c>0x47</c>), K (<c>0x4B</c>) and J (<c>0x4A</c>) among them: docs/raw-sources/pad-and-keyboard-state-2026-11-16.md.
    /// </summary>
    public static FieldBindings Default { get; } =
        new(0x4E, 0x4C, 0x0400, 0x0040, 0x0010, 0x0080, 0x47, 0x0020, 0x4B, 0x4A);

    /// <summary>The bindings the settings file's <c>keys.field</c> object names (#661), each missing or unusable one
    /// the default.</summary>
    public static FieldBindings From(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(
            settings.Binding("field", "nearbyKey", Default.NearbyKey),
            settings.Binding("field", "placeKey", Default.PlaceKey),
            settings.Binding("field", "holdButtons", Default.HoldButtons),
            settings.Binding("field", "nextButton", Default.NextButton),
            settings.Binding("field", "previousButton", Default.PreviousButton),
            settings.Binding("field", "placeButton", Default.PlaceButton),
            settings.Binding("field", "guideKey", Default.GuideKey),
            settings.Binding("field", "guideButton", Default.GuideButton),
            settings.Binding("field", "nextKey", Default.NextKey),
            settings.Binding("field", "previousKey", Default.PreviousKey),
            settings.Binding("field", "categoryNextKey", Default.CategoryNextKey),
            settings.Binding("field", "categoryPreviousKey", Default.CategoryPreviousKey),
            settings.Binding("field", "categoryHoldButtons", Default.CategoryHoldButtons),
            settings.Binding("field", "categoryButton", Default.CategoryButton),
            settings.Binding("field", "identifyKey", Default.IdentifyKey),
            settings.Binding("field", "objectiveKey", Default.ObjectiveKey),
            settings.Binding("field", "objectiveButton", Default.ObjectiveButton));
    }
}

/// <summary>The field controls (#181), while the player roams the field: the nearby key speaks the list of things
/// nearby (<see cref="Nearby"/>), the pad's next and previous chords step through it one thing at a time, and the place
/// key and chord say where the player is (<see cref="FieldPlace"/>), then the current mission (<see cref="MissionLine"/>,
/// #658). Each read of the keys or a pad also lets the mission line look for a new mission. The game state is read on
/// the press, on the thread
/// that handed it the read, and copied into plain values. Only the exact press is hidden from the game, and only in free
/// roam: the key, or the chord's D-pad button (the held button stays, so the game never sees it let go and pressed
/// again). Elsewhere everything reaches the game. A press is an up-to-down edge of a binding held on any pad or the
/// keyboard, as for the menu controls. Speech goes to the announcer directly: each is an answer the player asked for.
/// The next and previous keys step as the chords do. The guide key and chord start the <see cref="Beacon"/> toward the
/// thing last stepped to, or the nearest not already reached once that step is forgotten, and stop it when it's on;
/// every read of the pad or keyboard moves it on. The nearby list, "where am I" and the guidance beep each have their
/// own setting (#661), read on each input read: one turned off leaves its keys and chords to the game.</summary>
[FeatureSetting<FieldFeature>(
    FieldFeature.Nearby, FieldFeature.NearbyCategories, FieldFeature.WhereAmI, FieldFeature.Guidance,
    FieldFeature.GuidancePause, FieldFeature.CameraTilt, FieldFeature.ObjectiveGuidance)]
public sealed class FieldControls : IInputControls
{
    /// <summary>How long the pad's place on the list lasts after a step; the next step after that starts again at the
    /// nearest.</summary>
    public const long StepMemoryMilliseconds = 10_000;

    private const int KeyboardSource = -1;

    private readonly Lock gate = new();
    private readonly Func<FieldBindings> liveBindings;
    private readonly Func<FieldFeature, bool> isOn;
    private readonly IFieldReads field;
    private readonly FieldPlace place;
    private readonly IAnnouncer announcer;
    private readonly Func<long> milliseconds;
    private readonly Func<double> nearbyRadius;
    private readonly Action<string>? log;
    private readonly MissionLine? mission;
    private readonly Func<int, bool?>? storyFlag;
    private readonly ReachCallouts? reach;
    private readonly StunCallouts? stuns;
    private readonly TiltWarning? tilt;
    private readonly Sonar? sonar;
    private readonly WallTones? walls;
    private readonly RoutePings? routePings;
    private readonly ObjectiveGuide? objective;
    private readonly FieldStrikes? strikes;
    private readonly Binding toObjective = new();
    private readonly Binding nearby = new();
    private readonly Binding next = new();
    private readonly Binding previous = new();
    private readonly Binding where = new();
    private readonly Binding guide = new();
    private readonly Binding categoryNext = new();
    private readonly Binding categoryPrevious = new();
    private readonly Binding identify = new();
    private readonly NamesTable names;
    private readonly Beacon beacon;
    private NearbyCategory category = NearbyCategory.Everything;
    private readonly FreeRoamGate roam;
    private int position = -1;
    private long steppedAt;
    private BeaconTarget? current;

    /// <summary>Field controls on <paramref name="bindings"/> that read <paramref name="field"/>, name the place through
    /// <paramref name="place"/>, speak through <paramref name="announcer"/>, guide on <paramref name="cues"/>, keep time
    /// by <paramref name="milliseconds"/> and write what each press read, as numbers, to <paramref name="log"/> when
    /// there is one (Debug builds, which alone have the identify key), name things by <paramref name="names"/>, and
    /// run <paramref name="reach"/>'s check on each keyboard read when there is one (#644). "Where am I" ends with
    /// <paramref name="mission"/>'s current mission when there is one (#658). With a log and
    /// <paramref name="storyFlag"/>, its record also lists the main-mission flags (<see cref="MainMissionFlags"/>).
    /// The beacon guides along routes from <paramref name="routes"/> when given (#690). <paramref name="stuns"/>' check
    /// runs on each keyboard read when there is one (#772). Every feature is on but the compass words (#700), which
    /// replace the screen's directions rather than add to them.</summary>
    public FieldControls(
        FieldBindings bindings, IFieldReads field, FieldPlace place, IAnnouncer announcer, ICuePlayer cues,
        Func<long> milliseconds, Action<string>? log = null, NamesTable? names = null, ReachCallouts? reach = null,
        MissionLine? mission = null, Func<int, bool?>? storyFlag = null, IRoutePlanner? routes = null,
        StunCallouts? stuns = null, TiltWarning? tilt = null, Sonar? sonar = null, WallTones? walls = null,
        RoutePings? routePings = null, ObjectiveGuide? objective = null, FieldStrikes? strikes = null)
        : this(() => bindings, feature => feature != FieldFeature.Compass, field, place, announcer, cues, milliseconds, log, names, reach, mission,
            storyFlag, routes, stuns, tilt, sonar, walls, routePings, objective, strikes)
    {
        ArgumentNullException.ThrowIfNull(bindings);
    }

    /// <summary>Field controls on the bindings and settings in force, read on each input read (#661):
    /// <paramref name="isOn"/> says whether <see cref="FieldFeature.Nearby"/>, <see cref="FieldFeature.WhereAmI"/> and
    /// <see cref="FieldFeature.Guidance"/> are on, and <see cref="FieldFeature.NearbyCategories"/> the category keys.
    /// <paramref name="tilt"/>'s check and <paramref name="sonar"/>'s tick run on each keyboard read when there are ones
    /// (#828, #827), and "where am I" ends with the tilt's line while <see cref="FieldFeature.CameraTilt"/> is on.
    /// <paramref name="walls"/> tick on each keyboard read too (#830), and <paramref name="routePings"/> ping each place
    /// the list steps to (#831). The objective key starts the beacon toward <paramref name="objective"/>'s answer
    /// (#652) while <see cref="FieldFeature.ObjectiveGuidance"/> is on, and with a log "where am I" also writes its
    /// record. <paramref name="strikes"/>' check runs on each keyboard read when there is one (#696). The beep is placed
    /// by <paramref name="placement"/> (#903, #650). <paramref name="nearbyRadius"/> gives the list's radius in game
    /// units on each use (#931); <see cref="Nearby.Radius"/> when null.</summary>
    public FieldControls(
        Func<FieldBindings> bindings, Func<FieldFeature, bool> isOn, IFieldReads field, FieldPlace place,
        IAnnouncer announcer, ICuePlayer cues, Func<long> milliseconds, Action<string>? log = null,
        NamesTable? names = null, ReachCallouts? reach = null, MissionLine? mission = null,
        Func<int, bool?>? storyFlag = null, IRoutePlanner? routes = null, StunCallouts? stuns = null,
        TiltWarning? tilt = null, Sonar? sonar = null, WallTones? walls = null, RoutePings? routePings = null,
        ObjectiveGuide? objective = null, FieldStrikes? strikes = null, CuePlacement? placement = null,
        Func<double>? nearbyRadius = null)
    {
        this.nearbyRadius = nearbyRadius ?? (() => Nearby.Radius);
        this.objective = objective;
        this.strikes = strikes;
        this.tilt = tilt;
        this.sonar = sonar;
        this.walls = walls;
        this.routePings = routePings;
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(isOn);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(milliseconds);
        liveBindings = bindings;
        this.isOn = isOn;
        this.field = field;
        this.place = place;
        this.announcer = announcer;
        this.milliseconds = milliseconds;
        this.log = log;
        this.mission = mission;
        this.storyFlag = storyFlag;
        this.names = names ?? NamesTable.Empty;
        beacon = new Beacon(
            field, cues, announcer, milliseconds, log, routes, pauseOn: () => this.isOn(FieldFeature.GuidancePause),
            placement: placement);
        this.reach = reach;
        this.stuns = stuns;
        roam = new FreeRoamGate(field);
    }

    // The list within the radius setting (#931), read on each use.
    private IReadOnlyList<NearbyThing> NearbyList(FieldView view, NearbyCategory category) =>
        Nearby.List(view, category, nearbyRadius());

    private enum Press
    {
        None,
        List,
        Next,
        Previous,
        Place,
        Guide,
        CategoryNext,
        CategoryPrevious,
        Identify,
        Objective,
    }

    /// <summary>Takes one read of <paramref name="pad"/>'s buttons, made or not (#225), acts on a press, moves the
    /// beacon on, and returns the buttons to hand on: without a chord's D-pad button while it is held in free roam.
    /// </summary>
    public int Pad(int pad, int buttons, bool made)
    {
        mission?.Poll();
        if (!made)
        {
            beacon.Tick();
            return buttons;
        }
        var bindings = liveBindings();
        var listOn = isOn(FieldFeature.Nearby);
        var holding = Holds(buttons, bindings.HoldButtons);
        var nextHeld = holding && listOn && Holds(buttons, bindings.NextButton);
        var previousHeld = holding && listOn && Holds(buttons, bindings.PreviousButton);
        var placeHeld = holding && isOn(FieldFeature.WhereAmI) && Holds(buttons, bindings.PlaceButton);
        var guideHeld = holding && isOn(FieldFeature.Guidance) && Holds(buttons, bindings.GuideButton);
        var categoryHeld = isOn(FieldFeature.NearbyCategories) && bindings.CategoryButton != 0 &&
            Holds(buttons, bindings.CategoryHoldButtons) && Holds(buttons, bindings.CategoryButton);
        var objectiveHeld = holding && ObjectiveOn() && Holds(buttons, bindings.ObjectiveButton);
        var roaming = (nextHeld || previousHeld || placeHeld || guideHeld || categoryHeld || objectiveHeld) &&
            roam.Open();
        Press press;
        lock (gate)
        {
            var nextPressed = next.Update(pad, nextHeld && roaming);
            var previousPressed = previous.Update(pad, previousHeld && roaming);
            var placePressed = where.Update(pad, placeHeld && roaming);
            var guidePressed = guide.Update(pad, guideHeld && roaming);
            var categoryPressed = categoryNext.Update(pad, categoryHeld && roaming);
            var objectivePressed = toObjective.Update(pad, objectiveHeld && roaming);
            press = nextPressed ? Press.Next : previousPressed ? Press.Previous : placePressed ? Press.Place :
                guidePressed ? Press.Guide : categoryPressed ? Press.CategoryNext :
                objectivePressed ? Press.Objective : Press.None;
        }
        Act(press);
        beacon.Tick();
        if (!roaming)
        {
            return buttons;
        }
        return buttons & ~((nextHeld ? bindings.NextButton : 0) | (previousHeld ? bindings.PreviousButton : 0) |
            (placeHeld ? bindings.PlaceButton : 0) | (guideHeld ? bindings.GuideButton : 0) |
            (categoryHeld ? bindings.CategoryButton : 0) | (objectiveHeld ? bindings.ObjectiveButton : 0));
    }

    /// <summary>Takes one read of the keys held down, acts on a press, moves the beacon on, and returns the keys to hand
    /// on: without the field's keys while held in free roam.</summary>
    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        mission?.Poll();
        reach?.Check();
        stuns?.Check();
        tilt?.Check();
        sonar?.Tick();
        walls?.Tick();
        routePings?.Tick();
        strikes?.Check();
        PollObjective();
        var bindings = liveBindings();
        var listOn = isOn(FieldFeature.Nearby);
        var categoriesOn = isOn(FieldFeature.NearbyCategories);
        var listHeld = listOn && keys.Contains(bindings.NearbyKey);
        var placeHeld = isOn(FieldFeature.WhereAmI) && keys.Contains(bindings.PlaceKey);
        var guideHeld = isOn(FieldFeature.Guidance) && keys.Contains(bindings.GuideKey);
        var nextHeld = listOn && keys.Contains(bindings.NextKey);
        var previousHeld = listOn && keys.Contains(bindings.PreviousKey);
        var categoryNextHeld = categoriesOn && keys.Contains(bindings.CategoryNextKey);
        var categoryPreviousHeld = categoriesOn && keys.Contains(bindings.CategoryPreviousKey);
        var identifyHeld = log is not null && keys.Contains(bindings.IdentifyKey);
        var objectiveHeld = ObjectiveOn() && keys.Contains(bindings.ObjectiveKey);
        var roaming = (listHeld || placeHeld || guideHeld || nextHeld || previousHeld || categoryNextHeld ||
            categoryPreviousHeld || identifyHeld || objectiveHeld) && roam.Open();
        Press press;
        lock (gate)
        {
            var listPressed = nearby.Update(KeyboardSource, listHeld && roaming);
            var placePressed = where.Update(KeyboardSource, placeHeld && roaming);
            var guidePressed = guide.Update(KeyboardSource, guideHeld && roaming);
            var nextPressed = next.Update(KeyboardSource, nextHeld && roaming);
            var previousPressed = previous.Update(KeyboardSource, previousHeld && roaming);
            var categoryNextPressed = categoryNext.Update(KeyboardSource, categoryNextHeld && roaming);
            var categoryPreviousPressed = categoryPrevious.Update(KeyboardSource, categoryPreviousHeld && roaming);
            var identifyPressed = identify.Update(KeyboardSource, identifyHeld && roaming);
            var objectivePressed = toObjective.Update(KeyboardSource, objectiveHeld && roaming);
            press = listPressed ? Press.List : placePressed ? Press.Place : guidePressed ? Press.Guide :
                nextPressed ? Press.Next : previousPressed ? Press.Previous :
                categoryNextPressed ? Press.CategoryNext : categoryPreviousPressed ? Press.CategoryPrevious :
                identifyPressed ? Press.Identify : objectivePressed ? Press.Objective : Press.None;
        }
        Act(press);
        beacon.Tick();
        // Only a held key of a feature that is on: one turned off reaches the game.
        return roaming
            ? [.. keys.Where(key => key == 0 || !(
                (listHeld && key == bindings.NearbyKey) || (placeHeld && key == bindings.PlaceKey) ||
                (guideHeld && key == bindings.GuideKey) || (nextHeld && key == bindings.NextKey) ||
                (previousHeld && key == bindings.PreviousKey) ||
                (categoryNextHeld && key == bindings.CategoryNextKey) ||
                (categoryPreviousHeld && key == bindings.CategoryPreviousKey) ||
                (identifyHeld && key == bindings.IdentifyKey) || (objectiveHeld && key == bindings.ObjectiveKey)))]
            : keys;
    }

    /// <summary>Whether the beacon is guiding.</summary>
    public bool Guiding => beacon.Active;

    /// <summary>Where the beacon points, as <see cref="Beacon.Bearing"/>: what auto-walk steers by (#184).</summary>
    public double? GuideBearing => beacon.Bearing;

    /// <summary>Starts the beacon toward <paramref name="target"/>, in place of any target before, as the guide key
    /// does: the map cursor's spot or a bookmark (#877). Does nothing, and returns false, with guidance off.</summary>
    public bool GuideTo(BeaconTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!isOn(FieldFeature.Guidance))
        {
            return false;
        }
        beacon.Start(target);
        return true;
    }

    /// <summary>The field as read, named by the names table.</summary>
    private FieldView? ReadNamed() => field.Read() is { } view ? names.Apply(view) : null;

    private NearbyCategory Category()
    {
        lock (gate)
        {
            return category;
        }
    }

    private static bool Holds(int buttons, int binding) => binding != 0 && (buttons & binding) == binding;

    /// <summary>Reads the game for <paramref name="press"/> and speaks the answer, outside the lock: the announcer may
    /// take its time. A read that throws says nothing, so it never reaches the game.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A failed read must never reach the game; it says nothing.")]
    private void Act(Press press)
    {
        if (press == Press.None)
        {
            return;
        }
        if (press == Press.Guide)
        {
            Guide();
            return;
        }
        if (press == Press.Objective)
        {
            GuideToObjective();
            return;
        }
        string text;
        string? tiltLine = null;
        try
        {
            text = press switch
            {
                Press.Place => Place(out tiltLine),
                Press.CategoryNext or Press.CategoryPrevious => Cycle(press == Press.CategoryNext ? 1 : -1),
                Press.Identify => Identify(),
                _ => List(press),
            };
        }
        catch (Exception)
        {
            return;
        }
        announcer.Announce(new Announcement(text, Interrupt: true));
        // The tilt is its own queued line, so the reader pauses between it and the mission line (#856).
        if (tiltLine is not null)
        {
            announcer.Announce(new Announcement(tiltLine, Interrupt: false));
        }
    }

    /// <summary>Stops the beacon when it's on; otherwise starts it toward the thing last stepped to, while the step is
    /// remembered, or else the nearest thing not already reached (<see cref="Beacon.Arrived(NearbyThing)"/>). A thing already
    /// reached is said as "Already next to" instead (#183, by ear 2026-11-22: the nearest, a person within reach, was
    /// "Reached" at once). A read that throws starts nothing and says nothing.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A failed read must never reach the game; it says nothing.")]
    private void Guide()
    {
        if (beacon.Active)
        {
            beacon.Stop();
            return;
        }
        BeaconTarget? stepped;
        lock (gate)
        {
            stepped = position >= 0 && milliseconds() - steppedAt <= StepMemoryMilliseconds ? current : null;
        }
        BeaconTarget? target;
        try
        {
            target = stepped is not null ? Unreached(stepped) : NearestUnreached();
        }
        catch (Exception)
        {
            return;
        }
        if (target is not null)
        {
            beacon.Start(target);
        }
    }

    /// <summary>The stepped thing, or null after saying it's already reached from where the player stands now. An
    /// unreadable pose leaves the test to the beacon's first poll.</summary>
    private BeaconTarget? Unreached(BeaconTarget stepped)
    {
        if (field.ReadPose() is { } pose)
        {
            var (distance, _, inReach) = Nearby.Measure(pose, stepped.Unit, stepped.DoorReachValue);
            if (Beacon.Arrived(distance, inReach))
            {
                AlreadyNextTo(stepped.Unit);
                return null;
            }
        }
        return stepped;
    }

    /// <summary>The nearest thing on a fresh list that isn't already reached, or null after saying why there's none.
    /// </summary>
    private BeaconTarget? NearestUnreached()
    {
        if (ReadNamed() is not { } view)
        {
            announcer.Announce(new Announcement("Can't read the field", Interrupt: true));
            return null;
        }
        var things = NearbyList(view, Category());
        if (things.Count == 0)
        {
            announcer.Announce(new Announcement("Nothing nearby", Interrupt: true));
            return null;
        }
        if (things.FirstOrDefault(thing => !Beacon.Arrived(thing)) is not { } unreached)
        {
            AlreadyNextTo(things[0].Unit);
            return null;
        }
        return new BeaconTarget(unreached.Unit, view.DoorReachValue);
    }

    private bool ObjectiveOn() => objective is not null && isOn(FieldFeature.ObjectiveGuidance);

    /// <summary>Starts reading the current map's markers ahead of a press (#652). A read that throws does nothing.
    /// </summary>
    [SuppressMessage("Design", "CA1031", Justification = "A failed read must never reach the game; it does nothing.")]
    private void PollObjective()
    {
        if (!ObjectiveOn())
        {
            return;
        }
        try
        {
            objective!.Poll(field.MapId, milliseconds());
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Starts the beacon toward the current objective's nearest marker or waypoint (#652), in place of any
    /// target before, or says why there is none; with a log, writes the marker walk's record first. A read that throws
    /// starts nothing and says nothing.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A failed read must never reach the game; it says nothing.")]
    private void GuideToObjective()
    {
        ObjectiveAnswer answer;
        try
        {
            var map = field.MapId();
            var pose = field.ReadPose();
            log?.Invoke(objective!.Describe("key", map, pose));
            if (pose is not { } at)
            {
                announcer.Announce(new Announcement("Can't read the field", Interrupt: true));
                return;
            }
            answer = objective!.Find(map, at);
        }
        catch (Exception)
        {
            return;
        }
        if (answer.Target is { } target)
        {
            beacon.Start(target);
        }
        else if (answer.Text is { } text)
        {
            announcer.Announce(new Announcement(text, Interrupt: true));
        }
    }

    private void AlreadyNextTo(FieldUnit unit) => announcer.Announce(new Announcement(
        $"Already next to {unit.Name ?? Nearby.KindWord(unit.Says)}", Interrupt: true));

    /// <summary>Moves the list <paramref name="by"/> one category on or back, wrapping, forgets the step, and says the
    /// category and how many things it holds.</summary>
    private string Cycle(int by)
    {
        var count = Enum.GetValues<NearbyCategory>().Length;
        NearbyCategory now;
        lock (gate)
        {
            category = (NearbyCategory)((((int)category + by) % count + count) % count);
            position = -1;
            now = category;
        }
        var word = Nearby.CategoryWord(now);
        if (ReadNamed() is not { } view)
        {
            return word;
        }
        var things = NearbyList(view, now);
        return things.Count == 0 ? $"{word}, none nearby" :
            string.Create(CultureInfo.InvariantCulture, $"{word}, {things.Count} nearby");
    }

    /// <summary>Logs the nearest thing in the category for a names-table row (#687): the map, its object id, its list
    /// and spoken kind, its position and the kind fields read for it, as numbers; says its id. Debug builds only.
    /// </summary>
    private string Identify()
    {
        if (ReadNamed() is not { } view)
        {
            log?.Invoke("identify unreadable");
            return "Can't read the field";
        }
        if (NearbyList(view, Category()) is not [var nearest, ..])
        {
            return "Nothing nearby";
        }
        var unit = nearest.Unit;
        log?.Invoke(string.Create(CultureInfo.InvariantCulture,
            $"identify map={Number(view.MapId)} id={unit.Id ?? "?"} list={unit.Kind} kind={unit.Says} at=({unit.X:0.#},{unit.Y:0.#},{unit.Z:0.#}) {unit.Raw}"));
        var text = nearest.Text();
        return unit.Id is { } id ? $"Logged {id}, {text}" : $"Logged, no id, {text}";
    }

    private string Place(out string? tiltLine)
    {
        var map = field.MapId();
        var name = place.Name(map);
        var current = mission?.Current;
        if (log is not null)
        {
            // #658: the flags the game's main-mission read scans, to pair with the box on screen next session.
            var flags = storyFlag is null
                ? ""
                : " mainFlags=" + MainMissionFlags.Describe(MainMissionFlags.Highest(storyFlag, 4));
            log(string.Create(CultureInfo.InvariantCulture,
                $"place map={Number(map)} known={name is not null} mission={current is not null}{flags}"));
            // #652: the marker walk's record, so "where am I" on a marker fits the map-to-world conversion.
            if (objective is not null)
            {
                log(objective.Describe("place", map, field.ReadPose()));
            }
        }
        var said = name ?? "Place not known yet";
        var line = current is null ? said : $"{said}. {MissionLine.Spoken(current)}";
        tiltLine = tilt is not null && isOn(FieldFeature.CameraTilt) ? tilt.Line() : null;
        return line;
    }

    private string List(Press press)
    {
        if (ReadNamed() is not { } view)
        {
            log?.Invoke("nearby unreadable");
            return "Can't read the field";
        }
        var shown = Category();
        var things = NearbyList(view, shown);
        Log(view, things);
        if (things.Count == 0)
        {
            return shown == NearbyCategory.Everything ? "Nothing nearby" : $"{Nearby.CategoryWord(shown)}, none nearby";
        }
        if (press == Press.List)
        {
            var heading = shown == NearbyCategory.Everything ? "" : Nearby.CategoryWord(shown) + ", ";
            var height = isOn(FieldFeature.NearbyHeight);
            var compass = isOn(FieldFeature.Compass);
            return string.Create(CultureInfo.InvariantCulture, $"{heading}{things.Count} nearby. ") +
                string.Join(". ", things.Select(thing => thing.Text(height, compass)));
        }
        int at;
        lock (gate)
        {
            var now = milliseconds();
            var fresh = position < 0 || now - steppedAt > StepMemoryMilliseconds;
            steppedAt = now;
            position = fresh
                ? (press == Press.Next ? 0 : things.Count - 1)
                : ((press == Press.Next ? position + 1 : position - 1) % things.Count + things.Count) % things.Count;
            at = position;
            current = new BeaconTarget(things[at].Unit, view.DoorReachValue);
        }
        routePings?.Focus(things[at].Unit);
        return string.Create(CultureInfo.InvariantCulture,
            $"{at + 1} of {things.Count}. {things[at].Text(isOn(FieldFeature.NearbyHeight), isOn(FieldFeature.Compass))}");
    }

    private void Log(FieldView view, IReadOnlyList<NearbyThing> things)
    {
        if (log is null)
        {
            return;
        }
        var counts = string.Join(',', Enum.GetValues<FieldKind>()
            .Select(kind => string.Create(CultureInfo.InvariantCulture, $"{kind}:{view.Units.Count(unit => unit.Kind == kind)}")));
        log(string.Create(CultureInfo.InvariantCulture,
            $"nearby player=({view.X:0.#},{view.Y:0.#},{view.Z:0.#}) heading={view.Heading:0.###} camera={view.CameraHeading?.ToString("0.###", CultureInfo.InvariantCulture) ?? "?"} pitch={view.CameraPitch?.ToString("0.###", CultureInfo.InvariantCulture) ?? "?"} map={Number(view.MapId)} doorValue={Number(view.DoorReachValue)} units={counts}"));
        foreach (var thing in things)
        {
            var unit = thing.Unit;
            log(string.Create(CultureInfo.InvariantCulture,
                $"nearby kind={unit.Kind} at=({unit.X:0.#},{unit.Y:0.#},{unit.Z:0.#}) distance={thing.Distance:0.#} bearing={thing.Bearing:0.###} reach={thing.InReach} named={unit.Name is not null} id={unit.Id ?? "?"} {unit.Raw}"));
        }
    }

    private static string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "?";

    /// <summary>One binding's state across sources: which hold it now.</summary>
    private sealed class Binding
    {
        private readonly HashSet<int> holding = [];

        /// <summary>Records whether <paramref name="source"/> holds the binding, and says whether that made it go from
        /// held nowhere to held somewhere.</summary>
        public bool Update(int source, bool held)
        {
            var before = holding.Count > 0;
            if (held)
            {
                holding.Add(source);
            }
            else
            {
                holding.Remove(source);
            }
            return !before && holding.Count > 0;
        }
    }
}
