using Metaphor.Core.Cues;
using Metaphor.Core.Features;
using Metaphor.Core.Navigation;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>The field project's wiring and settings (#784, ADR 0022): the nearby list, "where am I" and the guidance
/// beep (#181, #183), reach callouts (#644), the current mission (#658), the names table (#687) and the beacon's routes
/// (#690). It reads the <see cref="IFieldReads"/> and <see cref="MissionRows"/> the hook layer provides, and its
/// controls take each input read last.</summary>
public sealed class FieldModule : IFeatureModule
{
    public IEnumerable<Enum> Settings => Enum.GetValues<FieldFeature>().Select(feature => (Enum)feature);

    // The field's sliders, and the volumes of the two kinds of sound only the field plays.
    public IEnumerable<SliderLimit> Limits =>
    [
        TiltLimits.Down, TiltLimits.Up, TiltLimits.Margin, WallLimits.Range, WallLimits.Loudness,
        RoutePingLimits.Loudness, CueVolumes.Sonar, Sonar.PingsPerSweep, CueVolumes.Guidance,
    ];

    public void Register(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = context.Settings;
        var log = context.Log;
        var cleaner = context.Cleaner;
        var field = context.Find<IFieldReads>() ?? throw new InvalidOperationException("no field reads provided");
        var missionRows = context.Find<MissionRows>() ?? throw new InvalidOperationException("no mission rows provided");
        // The map and objective tables load from the archive later; until then a place is its lookups alone and a
        // mission its title (#742, #743).
        var fieldTables = new FieldTablesSource();
        var place = new FieldPlace(cleaner, fieldTables.MapNameHash, context.Unknowns, () => fieldTables.MapsLoaded);
        context.Events.ObserveFiles(place);
        // The current main mission (#658): the top story flag's row in the mission table names its title's label, whose
        // text the library holds. Spoken through the reader like the other automatic field speech.
        var mission = new MissionLine(
            context.StoryFlag, missionRows.Read, context.Library, cleaner, context.Reader, () => Environment.TickCount64,
            new FreeRoamGate(field), log is null ? null : log.FieldRead, () => fieldTables.Objectives,
            () => settings.Current.IsOn(FieldFeature.WhereAmI));
        // The names table (#687) ships as data/names/<map>.json in the mod folder; the checks keep each file to the
        // schema, and a bad one is skipped (said in the dev log) so the rest still name things.
        var nearbyNames = NamesTable.Load(
            Path.Combine(context.ModFolder, "data", "names"), problem => log?.FieldRead("names table: " + problem));
        // The beacon's routes (#690) run over the map's .NAV cells, read off the game's thread from the archive, opened
        // read-only for each file (NavMeshSource). The map's file is named by the archive's map table (NavMapFiles,
        // #741), read once on the first route.
        var navLog = log is null ? (Action<string>)(_ => { }) : log.FieldRead;
        var navMeshes = new NavMeshSource(context.OpenArchive, navLog);
        var navFiles = NavMapFiles.FromArchive(context.OpenArchive, navLog);
        var routes = new NavRoutePlanner(navFiles, navMeshes.Load);
        // The navigation sounds take turns (#823): the sonar waits for the beep and a route ping, and the wall tones
        // duck under them. The controls, made below, say whether the beep guides.
        FieldControls? controls = null;
        var floor = new NavFloor(() => Environment.TickCount64, () => controls?.Guiding == true);
        // The generated navigation sounds (#841) ship as data/sounds/*.wav in the mod folder and are read once; a bad
        // file is left out (said in the dev log) and its feature plays the tone instead. Testing.
        var samples = new NavSamples(
            SoundBank.Load(Path.Combine(context.ModFolder, "data", "sounds"), problem => log?.FieldRead("sounds: " + problem)),
            () => settings.Current.IsOn(FieldFeature.SoundFiles));
        // The wall tones (#830) and route pings (#831), testing; their limits are read on each probe and ping.
        var edges = new MapEdges(navFiles, navMeshes.Load);
        var walls = new WallTones(
            field, context.Cues, settings.Speech(FieldFeature.WallTones, context.Announcer),
            () => Environment.TickCount64, () => settings.Current.IsOn(FieldFeature.WallTones),
            () => WallLimits.From(settings.Current), edges.For, floor, log is null ? null : log.FieldRead, samples);
        var routePings = new RoutePings(
            field, context.Cues, () => Environment.TickCount64, () => settings.Current.IsOn(FieldFeature.RoutePings),
            () => RoutePingLimits.From(settings.Current), routes, floor: floor,
            log: log is null ? null : log.FieldRead, samples: samples);
        // "Guide me to the objective" (#652), testing: the field's icon points read from the archive per field, and the
        // minimap frames that convert them to world once, both off the game's thread. A field in
        // data/objectives/map-to-world.json overrides its frame; the hand-written waypoints are waypoints.json (both
        // empty; a bad file is left out, said in the dev log).
        var objectiveData = ObjectiveData.Load(
            Path.Combine(context.ModFolder, "data", "objectives"), problem => log?.FieldRead("objectives: " + problem));
        var objective = new ObjectiveGuide(
            context.StoryFlag, () => fieldTables.Objectives, fieldTables.FileOf,
            MapIconPointSource.FromArchive(context.OpenArchive, navLog),
            new OverriddenMapToWorld(objectiveData.Conversions, MiniMapFrameSource.FromArchive(context.OpenArchive, navLog)),
            objectiveData.Waypoints, navMeshes.Get);
        // Its settings, the nearby list, its categories, "where am I" and guidance, are read on each read (#661).
        // Debug builds only: the "where am I" record lists the main-mission flags (#658).
        controls = new FieldControls(
            settings.Live(FieldBindings.From), feature => settings.Current.IsOn(feature), field, place,
            context.Announcer, context.Cues, () => Environment.TickCount64, log is null ? null : log.FieldRead,
            nearbyNames,
            new ReachCallouts(field, settings.Speech(FieldFeature.Reach, context.Announcer), () => Environment.TickCount64),
            mission, log is null ? null : context.StoryFlag, routes,
            // Stun callouts (#772), testing: said through their own setting's gate.
            new StunCallouts(
                field, settings.Speech(FieldFeature.StunCallouts, context.Announcer), () => Environment.TickCount64),
            // The camera-tilt warning (#828) and the sonar (#827), testing; the limits are read on each check.
            new TiltWarning(
                field, settings.Speech(FieldFeature.CameraTilt, context.Announcer), () => Environment.TickCount64,
                () => TiltLimits.From(settings.Current), () => context.LastDevice.Current),
            new Sonar(
                field, context.Cues, () => Environment.TickCount64, () => settings.Current.IsOn(FieldFeature.Sonar),
                log is null ? null : log.FieldRead, nearbyNames, floor,
                () => Sonar.PingsPerSweep.From(settings.Current), samples),
            walls, routePings, objective,
            // Field strike callouts (#696), testing: said through their own setting's gate.
            new FieldStrikes(
                field, settings.Speech(FieldFeature.FieldStrikes, context.Announcer), () => Environment.TickCount64,
                log is null ? null : log.FieldRead));
        // The corner advice (#587), testing: the report-open hook finds it by type and hands it each open's key; a
        // line opened outside free roam is said on a later input read, so it takes the read before the field's.
        var cornerAdvice = new CornerAdvice(
            context.Library, cleaner, settings.Speech(FieldFeature.CornerAdvice, context.Reader), new FreeRoamGate(field),
            () => Environment.TickCount64, log is null ? null : log.FieldRead)
        {
            Wanted = () => settings.Current.IsOn(FieldFeature.CornerAdvice),
        };
        context.AddControls(settings.Input(FieldFeature.CornerAdvice, cornerAdvice));
        context.Provide(cornerAdvice);
        context.AddControls(controls);
        // The enemy awareness cues (#649), testing: their sounds through their own keys, the line through the setting's
        // gate; each enemy's AI state changes go to the Debug log.
        context.AddControls(new EnemyAwareness(
            field, context.Cues, settings.Speech(FieldFeature.EnemyAwareness, context.Announcer),
            () => Environment.TickCount64, () => settings.Current.IsOn(FieldFeature.EnemyAwareness),
            log is null ? null : log.FieldRead));
        context.AddKeys(FieldKeys.All);
        // LB and RB held back in free roam until let go, so the reader's and the list's chords open no screen (#811),
        // testing.
        var shoulders = new FieldShoulderHold(
            settings.Live(FieldBindings.From), settings.Live(s => ReaderBinding.From(s).Buttons), new FreeRoamGate(field));
        context.AddShoulderHold(() => settings.Current.IsOn(FieldFeature.ShoulderHold) ? shoulders.Held() : 0);
        context.Provide(place);
        context.Provide(fieldTables);
    }
}

/// <summary>Reads the main mission table's rows (#658): what the hook layer provides the field module, from the table
/// the loader locates.</summary>
public sealed class MissionRows(Func<IReadOnlyList<MainMissionRow>?> read)
{
    /// <summary>The rows, or null when the table couldn't be read.</summary>
    public IReadOnlyList<MainMissionRow>? Read() => read();
}
