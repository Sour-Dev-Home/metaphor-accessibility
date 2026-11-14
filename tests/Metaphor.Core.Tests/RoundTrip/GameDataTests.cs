namespace Metaphor.Core.Tests.RoundTrip;

[Trait("Category", "Unit")]
public sealed class GameDataTests : IDisposable
{
    private const string Build = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("metaphor-gamedata-");

    public void Dispose() => _temp.Delete(recursive: true);

    [Fact]
    public void TheEnvironmentVariableWins()
    {
        var data = MakeData("from-env", Build);
        var repo = MakeRepo();
        MakeData(Path.Combine(repo, "extracted"), Build);

        var found = GameData.Find(data, Path.Combine(repo, "bin")).Data;

        Assert.Equal(data, found?.Root);
        Assert.Equal(Build, found?.Build);
    }

    [Fact]
    public void FallsBackToTheExtractedFolderAboveTheTests()
    {
        var repo = MakeRepo();
        var extracted = MakeData(Path.Combine(repo, "extracted"), Build);

        var found = GameData.Find(null, Path.Combine(repo, "tests", "bin")).Data;

        Assert.Equal(extracted, found?.Root);
    }

    [Fact]
    public void AnEmptyExtractedFolderIsNoData()
    {
        var repo = MakeRepo();
        Directory.CreateDirectory(Path.Combine(repo, "extracted"));

        var search = GameData.Find("", Path.Combine(repo, "bin"));

        Assert.Null(search.Data);
        Assert.Contains(GameData.EnvironmentVariable, search.MissingReason, StringComparison.Ordinal);
    }

    [Fact]
    public void AVariableNamingAMissingFolderSaysSo()
    {
        var missing = Path.Combine(_temp.FullName, "nowhere");

        var search = GameData.Find(missing, _temp.FullName);

        Assert.Null(search.Data);
        Assert.Contains(missing, search.MissingReason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a build")]
    public void DataWithoutAValidBuildFileHasAnUnknownBuild(string? buildFile)
    {
        var data = MakeData("no-build", buildFile);

        var found = GameData.Find(data, _temp.FullName).Data;

        Assert.Equal(GameData.UnknownBuild, found?.Build);
    }

    [Fact]
    public void TheBuildFileMayCarryLabelsAfterTheHash()
    {
        var data = MakeData("labelled", $"{Build.ToUpperInvariant()}\nSteam build 123\n");

        Assert.Equal(Build, GameData.Find(data, _temp.FullName).Data?.Build);
    }

    private string MakeRepo()
    {
        var repo = Directory.CreateDirectory(Path.Combine(_temp.FullName, "repo")).FullName;
        File.WriteAllText(Path.Combine(repo, "Metaphor.sln"), "");
        return repo;
    }

    private string MakeData(string folder, string? buildFile)
    {
        var root = Directory.CreateDirectory(Path.Combine(_temp.FullName, folder)).FullName;
        File.WriteAllText(Path.Combine(root, "some.bin"), "x");
        if (buildFile is not null)
        {
            File.WriteAllText(Path.Combine(root, GameData.BuildFileName), buildFile);
        }

        return root;
    }
}
