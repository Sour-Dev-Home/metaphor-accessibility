namespace Metaphor.Features.Field.Tests;

/// <summary>The names table (#687): one JSON file per map under <c>data/names/</c>, keyed by object id, each entry a
/// contributor's name and kind. Made-up maps, ids and names only.</summary>
[Trait("Category", "Unit")]
public sealed class NamesTableTests
{
    private const string Valid = """
        {
          "map": 7,
          "objects": {
            "npc-2-17": { "name": "Display case", "kind": "examine" },
            "door-4": { "name": "Gate to the square", "kind": "exit" }
          }
        }
        """;

    private static NamesTable Table(string json = Valid, string file = "7.json") =>
        NamesTable.From([NamesTable.Parse(json, file)]);

    [Fact]
    public void ARowRenamesTheThingAndChangesItsKind()
    {
        var view = new FieldView(0, 0, 0, 0,
        [
            new(FieldKind.Person, 0, 0, 200, Id: "npc-2-17"),
            new(FieldKind.Person, 0, 0, 400, Id: "npc-2-18"),
        ], MapId: 7);

        var texts = Nearby.List(Table().Apply(view)).Select(thing => thing.Text());

        Assert.Equal(["Display case, 2 steps ahead", "Person, 4 steps ahead"], texts);
    }

    [Fact]
    public void ARowMovesTheThingToItsKindsCategory()
    {
        var view = new FieldView(0, 0, 0, 0, [new(FieldKind.Person, 0, 0, 200, Id: "npc-2-17")], MapId: 7);

        var named = Table().Apply(view);

        Assert.Empty(Nearby.List(named, NearbyCategory.People));
        Assert.Single(Nearby.List(named, NearbyCategory.Examine));
    }

    [Fact]
    public void ARowForAnotherMapOrAThingWithNoIdChangesNothing()
    {
        var view = new FieldView(0, 0, 0, 0,
            [new(FieldKind.Person, 0, 0, 200, Id: "npc-2-17"), new(FieldKind.Person, 0, 0, 300)], MapId: 8);

        Assert.Equal(view.Units, Table().Apply(view).Units);
        Assert.Equal(view.Units, Table().Apply(view with { MapId = null }).Units);
    }

    [Fact]
    public void AnEmptyTableChangesNothing()
    {
        var view = new FieldView(0, 0, 0, 0, [new(FieldKind.Person, 0, 0, 200, Id: "npc-2-17")], MapId: 7);

        Assert.Same(view, NamesTable.Empty.Apply(view));
    }

    [Theory]
    [InlineData("""{ "map": 8, "objects": {} }""", "7.json", "map 8")]
    [InlineData("""{ "map": 7, "objects": { "npc-2-17": { "name": "Case", "kind": "box" } } }""", "7.json", "kind")]
    [InlineData("""{ "map": 7, "objects": { "npc-2-17": { "name": " ", "kind": "examine" } } }""", "7.json", "name")]
    [InlineData("""{ "map": 7, "objects": { "npc-2-17": { "name": "Case", "kind": "examine", "x": 1 } } }""", "7.json", "x")]
    [InlineData("""{ "map": 7, "objects": { "npc-17": { "name": "Case", "kind": "examine" } } }""", "7.json", "npc-17")]
    [InlineData("""{ "map": 7, "objects": { "door-1": { "name": "A", "kind": "door" }, "door-1": { "name": "B", "kind": "door" } } }""", "7.json", "twice")]
    [InlineData("""{ "map": 7 }""", "7.json", "objects")]
    [InlineData("""{ "map": 7, "objects": {}, "notes": "" }""", "7.json", "notes")]
    [InlineData("""{ "map": 7, "objects": {} }""", "seven.json", "seven.json")]
    [InlineData("""{ "map": 7, "objects": { "door-1": { "name": "A\nB", "kind": "door" } } }""", "7.json", "name")]
    [InlineData("not json", "7.json", "JSON")]
    public void AFileOutsideTheSchemaIsRefusedWithWhy(string json, string file, string why)
    {
        var refused = Assert.Throws<FormatException>(() => NamesTable.Parse(json, file));

        Assert.Contains(why, refused.Message, StringComparison.Ordinal);
        Assert.StartsWith(file, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANameLongerThanSixtyCharactersIsRefused()
    {
        var json = $$"""{ "map": 7, "objects": { "door-1": { "name": "{{new string('a', 61)}}", "kind": "door" } } }""";

        Assert.Throws<FormatException>(() => NamesTable.Parse(json, "7.json"));
    }

    [Fact]
    public void LoadReadsEveryFileAndReportsABadOneWithoutStopping()
    {
        var folder = Directory.CreateTempSubdirectory("names-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "7.json"), Valid);
            File.WriteAllText(Path.Combine(folder, "9.json"), "{");
            var problems = new List<string>();

            var table = NamesTable.Load(folder, problems.Add);

            var view = new FieldView(0, 0, 0, 0, [new(FieldKind.Door, 0, 0, 200, Id: "door-4")], MapId: 7);
            Assert.Equal("Gate to the square", table.Apply(view).Units.Single().Name);
            Assert.StartsWith("9.json", Assert.Single(problems), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void LoadSkipsAFileOverTheSizeCap()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "7.json"), new string(' ', (int)NamesTable.MaxFileBytes + 1));
            var problems = new List<string>();

            var table = NamesTable.Load(folder, problems.Add);

            Assert.Same(NamesTable.Empty, table);
            Assert.Contains("skipped", Assert.Single(problems), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void LoadOfAMissingFolderIsEmpty()
    {
        var problems = new List<string>();

        var table = NamesTable.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), problems.Add);

        Assert.Same(NamesTable.Empty, table);
        Assert.Empty(problems);
    }

    /// <summary>The check on the shipped files: every <c>data/names/*.json</c> in the repository keeps the schema.
    /// </summary>
    [Fact]
    public void EveryNamesFileInTheRepositoryKeepsTheSchema()
    {
        var folder = Path.Combine(RepositoryRoot(), "data", "names");
        var problems = new List<string>();

        if (Directory.Exists(folder))
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                try
                {
                    NamesTable.Parse(File.ReadAllText(file), Path.GetFileName(file));
                }
                catch (FormatException refused)
                {
                    problems.Add(refused.Message);
                }
            }
        }

        Assert.Empty(problems);
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
