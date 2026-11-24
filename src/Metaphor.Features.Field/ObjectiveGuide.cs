using System.Globalization;
using System.Numerics;
using Metaphor.Core.Navigation;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>What the objective key found (#652): a target for the beacon, or what to say instead.</summary>
/// <param name="Target">Where to guide, or null.</param>
/// <param name="Text">What to say when there is no target.</param>
public sealed record ObjectiveAnswer(BeaconTarget? Target, string? Text);

/// <summary>Finds where the beacon guides for "guide me to the objective" (#652): the current field objective's row
/// (<see cref="MainMissionFlags.CurrentIndex"/>), the current map's field (<c>MapData.bin</c>'s file numbers), that
/// field's markers for the row (<see cref="MapIconPoints"/>) converted to world <c>x, z</c> by
/// <see cref="IMapToWorld"/>, and the hand-written waypoints for the row (our own data, ADR 0016); the nearest of
/// them. Its key is in <see cref="FieldControls"/>. On game build 548ddc95… (Steam build 18330018)
/// (docs/findings/objective-map-icon-points-static.md, docs/findings/minimap-data-converts-map-points-to-world.md); a
/// map with no minimap record and no override says <see cref="NotReadyText"/>. The row join is
/// [NEEDS VERIFICATION] until a marker stop confirms it. In a Debug build each press of the key and of "where am I"
/// logs what the check needs (<see cref="Describe"/>). Safe to call from several threads.</summary>
[FeatureSetting<FieldFeature>(FieldFeature.ObjectiveGuidance)]
public sealed class ObjectiveGuide
{
    /// <summary>What is said when the field has markers but no fitted conversion, or the map isn't known.</summary>
    public const string NotReadyText = "Objective guidance isn't ready for this area";

    /// <summary>What is said while the field's markers are read from the archive.</summary>
    public const string LoadingText = "Objective markers still loading, try again in a moment";

    /// <summary>What is said when no current objective is known.</summary>
    public const string NoObjectiveText = "No current objective known";

    /// <summary>What is said when the field has no marker and no waypoint for the objective.</summary>
    public const string NoMarkerText = "No objective marker in this area";

    /// <summary>What is said when the player already stands at the nearest one.</summary>
    public const string ArrivedText = "Already at the objective";

    /// <summary>What the beacon calls the target: "Guiding to the objective".</summary>
    public const string TargetName = "the objective";

    /// <summary>How often <see cref="Poll"/> starts the current field's read, at most.</summary>
    public const long PollMilliseconds = 1000;

    /// <summary>How far a converted marker may be from a walkable cell for the log to measure it: 32 cells, the walk
    /// graph's widest search.</summary>
    public const int ResidualCells = 32;

    private readonly Func<int, bool?> storyFlag;
    private readonly Func<IReadOnlyList<MainMissionRow>?> objectives;
    private readonly Func<int, (int A, int B)?> fieldOf;
    private readonly MapIconPointSource points;
    private readonly IMapToWorld toWorld;
    private readonly IReadOnlyList<ObjectiveWaypoint> waypoints;
    private readonly Func<int, int, WalkGraph?>? mesh;
    private readonly Lock gate = new();
    private long? polledAt;

    /// <summary>A guide that reads the story flags through <paramref name="storyFlag"/>, the field objective table
    /// through <paramref name="objectives"/> (null while it loads), each map's field file numbers through
    /// <paramref name="fieldOf"/>, the field's markers from <paramref name="points"/>, converts them with
    /// <paramref name="toWorld"/>, and adds <paramref name="waypoints"/>. The Debug log measures each converted marker
    /// against field <c>f_&lt;a&gt;_&lt;b&gt;</c>'s walk graph from <paramref name="mesh"/> (null while it loads).
    /// </summary>
    public ObjectiveGuide(
        Func<int, bool?> storyFlag, Func<IReadOnlyList<MainMissionRow>?> objectives, Func<int, (int A, int B)?> fieldOf,
        MapIconPointSource points, IMapToWorld toWorld, IReadOnlyList<ObjectiveWaypoint> waypoints,
        Func<int, int, WalkGraph?>? mesh = null)
    {
        this.mesh = mesh;
        ArgumentNullException.ThrowIfNull(storyFlag);
        ArgumentNullException.ThrowIfNull(objectives);
        ArgumentNullException.ThrowIfNull(fieldOf);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(toWorld);
        ArgumentNullException.ThrowIfNull(waypoints);
        this.storyFlag = storyFlag;
        this.objectives = objectives;
        this.fieldOf = fieldOf;
        this.points = points;
        this.toWorld = toWorld;
        this.waypoints = waypoints;
    }

    /// <summary>Starts reading the markers of the map <paramref name="map"/> reads ahead of a press, at most once every
    /// <see cref="PollMilliseconds"/> by <paramref name="now"/>, so the key seldom has to wait. The map is read only
    /// when a poll is due.</summary>
    public void Poll(Func<int?> map, long now)
    {
        ArgumentNullException.ThrowIfNull(map);
        lock (gate)
        {
            if (polledAt is { } last && now - last < PollMilliseconds)
            {
                return;
            }
            polledAt = now;
        }
        _ = toWorld.Loading;
        if (map() is { } id && fieldOf(id) is { } file)
        {
            _ = points.For(file.A, file.B);
        }
    }

    /// <summary>The nearest marker or waypoint for the current objective on map <paramref name="map"/>, from the player
    /// at <paramref name="pose"/>, or what to say when there is none.</summary>
    public ObjectiveAnswer Find(int? map, FieldPose pose)
    {
        if (objectives() is not { } table || MainMissionFlags.CurrentIndex(storyFlag, table) is not { } row)
        {
            return new(null, NoObjectiveText);
        }
        if (map is not { } id || fieldOf(id) is not { } file)
        {
            return new(null, NotReadyText);
        }
        var (a, b) = file;
        var field = MapIconPoints.FieldName(a, b);
        var places = waypoints.Where(waypoint => waypoint.Field == field && waypoint.Row == row)
            .Select(waypoint => (waypoint.X, waypoint.Z)).ToList();
        var loaded = points.For(a, b);
        if (loaded is null && points.Loading(a, b) && places.Count == 0)
        {
            return new(null, LoadingText);
        }
        var markers = Markers(loaded, row).ToList();
        var spot = new MapSpot(id, field, pose.Y);
        foreach (var marker in markers)
        {
            if (toWorld.ToWorld(spot, marker.X, marker.Y) is { } world)
            {
                places.Add(world);
            }
        }
        if (places.Count == 0)
        {
            return new(null, markers.Count == 0 ? NoMarkerText : toWorld.Loading ? LoadingText : NotReadyText);
        }
        var (x, z) = places.MinBy(place => Ground(pose, place.X, place.Z));
        if (Beacon.Arrived(Math.Sqrt(Ground(pose, x, z)), inReach: false))
        {
            return new(null, ArrivedText);
        }
        return new(new BeaconTarget(new FieldUnit(FieldKind.Person, x, pose.Y, z, TargetName), null), null);
    }

    /// <summary>The Debug log record for a marker walk (#652): what asked (<paramref name="why"/>), the map and its
    /// field, the player's world position, the objective's row and flag, whether the field has a conversion, its
    /// waypoints for the row, and every icon point of the field, raw. Numbers only.</summary>
    public string Describe(string why, int? map, FieldPose? pose)
    {
        ArgumentNullException.ThrowIfNull(why);
        var table = objectives();
        var row = table is null ? null : MainMissionFlags.CurrentIndex(storyFlag, table);
        var flag = row is { } at && table is not null ? "0x" + table[at].Flag.ToString("x", CultureInfo.InvariantCulture) : "?";
        var file = map is { } id ? fieldOf(id) : null;
        var field = file is { } named ? MapIconPoints.FieldName(named.A, named.B) : "?";
        var loaded = file is { } read ? points.For(read.A, read.B) : null;
        var state = file is not { } asked ? "no field" : loaded is not null ? MapIconPoints.Describe(loaded.Points) :
            points.Loading(asked.A, asked.B) ? "loading" : "unreadable";
        MapSpot? spot = map is { } mapId && file is not null ? new(mapId, field, pose?.Y) : null;
        var conversion = spot is { } where ? toWorld.Describe(where) : "none";
        var written = waypoints.Count(waypoint => waypoint.Field == field && waypoint.Row == row);
        var player = pose is { } p
            ? string.Create(CultureInfo.InvariantCulture, $"({p.X:0.#},{p.Y:0.#},{p.Z:0.#})")
            : "?";
        var world = spot is { } converted && file is { } walked && row is { } current
            ? World(converted, walked, Markers(loaded, current), pose?.Y ?? 0)
            : "none";
        return string.Create(CultureInfo.InvariantCulture,
            $"objective {why} map={map?.ToString(CultureInfo.InvariantCulture) ?? "?"} field={field} player={player} row={row?.ToString(CultureInfo.InvariantCulture) ?? "?"} flag={flag} conversion={conversion} waypoints={written} variant1={loaded?.HasVariant == true} points={state} world={world}");
    }

    // Each marker's world point and its distance to the nearest walkable cell's middle: (1045,295)->(-3,16589)r=12,
    // r=? while the walk graph loads, r=far past ResidualCells; "none" with no marker.
    private string World(MapSpot spot, (int A, int B) file, IEnumerable<MapIconPoint> markers, float height)
    {
        var graph = mesh?.Invoke(file.A, file.B);
        var entries = markers.Select(marker =>
        {
            var point = string.Create(CultureInfo.InvariantCulture, $"({marker.X},{marker.Y})->");
            if (toWorld.ToWorld(spot, marker.X, marker.Y) is not { } at)
            {
                return point + "none";
            }
            var residual = graph is null ? "?"
                : graph.NearestCell(new Vector3(at.X, height, at.Z), float.MaxValue, ResidualCells) is { } cell
                    ? Vector2.Distance(new(at.X, at.Z), new(graph.Centre(cell).X, graph.Centre(cell).Z))
                        .ToString("0", CultureInfo.InvariantCulture)
                    : "far";
            return point + string.Create(CultureInfo.InvariantCulture, $"({at.X:0.#},{at.Z:0.#})r={residual}");
        }).ToList();
        return entries.Count == 0 ? "none" : string.Join(' ', entries);
    }

    private static IEnumerable<MapIconPoint> Markers(FieldIconPoints? loaded, int row) =>
        loaded is null ? [] : loaded.Points.Where(point => point.Kind == MapIconPoints.MissionKind && point.Index == row);

    private static double Ground(FieldPose pose, float x, float z)
    {
        var dx = x - pose.X;
        var dz = z - pose.Z;
        return (dx * dx) + (dz * dz);
    }
}
