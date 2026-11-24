namespace Metaphor.Features.Field.Tests;

/// <summary>The objective guidance's own data (#652): <c>data/objectives/map-to-world.json</c>, each field's fitted
/// conversion, and <c>waypoints.json</c>, the hand-written waypoints. Made-up fields and numbers only.</summary>
[Trait("Category", "Unit")]
public sealed class ObjectiveDataTests
{
    [Fact]
    public void AConversionIsAnOriginPlusAScalePerAxis()
    {
        var fitted = ObjectiveData.ParseConversions("""
            { "maps": [ { "field": "f_0001_001", "originX": -100, "originZ": 50.5, "scaleX": 2, "scaleZ": -0.5 } ] }
            """);

        Assert.Equal((1900f, 0.5f), fitted.ToWorld("f_0001_001", 1000, 100));
        Assert.Null(fitted.ToWorld("f_0001_002", 1000, 100));
    }

    [Fact]
    public void AWaypointIsAFieldARowAndAWorldPosition()
    {
        var waypoints = ObjectiveData.ParseWaypoints("""
            { "waypoints": [ { "field": "f_0001_001", "row": 1, "x": -2269.4, "z": 8609.8 } ] }
            """);

        Assert.Equal([new ObjectiveWaypoint("f_0001_001", 1, -2269.4f, 8609.8f)], waypoints);
    }

    [Theory]
    [InlineData("not json", "map-to-world.json: isn't JSON")]
    [InlineData("""{ "fields": [] }""", "map-to-world.json: must hold one object with maps, a list")]
    [InlineData("""{ "maps": [], "extra": 1 }""", "map-to-world.json: holds extra")]
    [InlineData("""{ "maps": [ 1 ] }""", "map-to-world.json: each of maps must be an object")]
    [InlineData("""{ "maps": [ { "field": "map1", "originX": 0, "originZ": 0, "scaleX": 1, "scaleZ": 1 } ] }""",
        "map-to-world.json: each row needs field")]
    [InlineData("""{ "maps": [ { "field": "f_0001_001", "originX": "0", "originZ": 0, "scaleX": 1, "scaleZ": 1 } ] }""",
        "map-to-world.json: f_0001_001: originX must be a number")]
    [InlineData("""{ "maps": [ { "field": "f_0001_001", "originX": 0, "originZ": 0, "scaleX": 0, "scaleZ": 1 } ] }""",
        "map-to-world.json: f_0001_001: scaleX must be a number other than 0")]
    [InlineData("""{ "maps": [ { "field": "f_0001_001", "originX": 0, "originZ": 0, "scaleX": 1 } ] }""",
        "map-to-world.json: f_0001_001: scaleZ must be a number")]
    [InlineData("""{ "maps": [ { "field": "f_0001_001", "originX": 0, "originZ": 0, "scaleX": 1, "scaleZ": 1, "turn": 1 } ] }""",
        "map-to-world.json: holds turn")]
    public void AConversionOutsideTheSchemaIsRefused(string json, string problem)
    {
        var refused = Assert.Throws<FormatException>(() => ObjectiveData.ParseConversions(json));

        Assert.StartsWith(problem, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFieldConvertedTwiceIsRefused()
    {
        var refused = Assert.Throws<FormatException>(() => ObjectiveData.ParseConversions("""
            { "maps": [
              { "field": "f_0001_001", "originX": 0, "originZ": 0, "scaleX": 1, "scaleZ": 1 },
              { "field": "f_0001_001", "originX": 0, "originZ": 0, "scaleX": 1, "scaleZ": 1 } ] }
            """));

        Assert.Equal("map-to-world.json: f_0001_001 is there twice", refused.Message);
    }

    [Theory]
    [InlineData("""{ "waypoints": [ { "field": "f_0001_001", "x": 0, "z": 0 } ] }""",
        "waypoints.json: a waypoint on f_0001_001 needs row")]
    [InlineData("""{ "waypoints": [ { "field": "f_0001_001", "row": -1, "x": 0, "z": 0 } ] }""",
        "waypoints.json: a waypoint on f_0001_001 needs row")]
    [InlineData("""{ "waypoints": [ { "field": "f_0001_001", "row": 1, "x": 0 } ] }""",
        "waypoints.json: f_0001_001: z must be a number")]
    [InlineData("""{ "waypoints": [ { "field": "f_0001_001", "row": 1, "x": 0, "z": 0, "y": 0 } ] }""",
        "waypoints.json: holds y")]
    public void AWaypointOutsideTheSchemaIsRefused(string json, string problem)
    {
        var refused = Assert.Throws<FormatException>(() => ObjectiveData.ParseWaypoints(json));

        Assert.StartsWith(problem, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadReadsBothFilesAndLeavesOutABadOne()
    {
        var folder = Directory.CreateTempSubdirectory("objectives ").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, ObjectiveData.ConversionsFile), "{");
            File.WriteAllText(Path.Combine(folder, ObjectiveData.WaypointsFile),
                """{ "waypoints": [ { "field": "f_0001_002", "row": 3, "x": 1, "z": 2 } ] }""");
            var problems = new List<string>();

            var data = ObjectiveData.Load(folder, problems.Add);

            Assert.Equal(0, data.Conversions.Count);
            Assert.Single(data.Waypoints);
            Assert.Equal(["map-to-world.json: isn't JSON"], problems);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AMissingFolderIsEmptyAndQuiet()
    {
        var problems = new List<string>();

        var data = ObjectiveData.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), problems.Add);

        Assert.Equal(0, data.Conversions.Count);
        Assert.Empty(data.Waypoints);
        Assert.Empty(problems);
    }

    /// <summary>The check on the shipped files: both keep the schema, and stay empty until a marker walk fits them.
    /// </summary>
    [Fact]
    public void TheRepositorysFilesKeepTheSchema()
    {
        var folder = Path.Combine(RepositoryRoot(), "data", "objectives");

        Assert.NotNull(ObjectiveData.ParseConversions(File.ReadAllText(Path.Combine(folder, ObjectiveData.ConversionsFile))));
        Assert.NotNull(ObjectiveData.ParseWaypoints(File.ReadAllText(Path.Combine(folder, ObjectiveData.WaypointsFile))));
    }

    private static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(folder.FullName, "Metaphor.sln")))
        {
            folder = folder.Parent ?? throw new InvalidOperationException("No Metaphor.sln above the test folder");
        }
        return folder.FullName;
    }
}
