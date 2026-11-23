using Metaphor.Core.Features;
using Metaphor.Core.Navigation;

namespace Metaphor.Features.Field;

/// <summary>The field project's wiring and settings (#784, ADR 0022): the nearby list, "where am I" and the guidance
/// beep (#181, #183), reach callouts (#644), the current mission (#658), the names table (#687) and the beacon's routes
/// (#690). It reads the <see cref="IFieldReads"/> and <see cref="MissionRows"/> the hook layer provides, and its
/// controls take each input read last.</summary>
public sealed class FieldModule : IFeatureModule
{
    public IEnumerable<Enum> Settings => Enum.GetValues<FieldFeature>().Select(feature => (Enum)feature);

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
        var place = new FieldPlace(cleaner, fieldTables.MapNameHash);
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
        var routes = new NavRoutePlanner(NavMapFiles.FromArchive(context.OpenArchive, navLog), navMeshes.Load);
        // Its settings, the nearby list, its categories, "where am I" and guidance, are read on each read (#661).
        // Debug builds only: the "where am I" record lists the main-mission flags (#658).
        context.AddControls(new FieldControls(
            settings.Live(FieldBindings.From), feature => settings.Current.IsOn(feature), field, place,
            context.Announcer, context.Cues, () => Environment.TickCount64, log is null ? null : log.FieldRead,
            nearbyNames,
            new ReachCallouts(field, settings.Speech(FieldFeature.Reach, context.Announcer), () => Environment.TickCount64),
            mission, log is null ? null : context.StoryFlag, routes));
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
