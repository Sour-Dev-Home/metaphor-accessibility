namespace Metaphor.Core.Tests;

[Trait("Category", "Unit")]
public sealed class ModStatusFileTests : IDisposable
{
    private const string Build = "548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504";

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("metaphor-status-");

    public void Dispose() => _folder.Delete(recursive: true);

    private string Written => File.ReadAllText(Path.Combine(_folder.FullName, ModStatusFile.FileName));

    // scripts/dev/smoke-test.ps1 parses these exact property names and values.
    [Fact]
    public void AReadyModWritesTheShapeTheSmokeTestReads()
    {
        ModStatusFile.Write(_folder.FullName, ModStatus.Ready("metaphor.test", Build, hooksLoaded: 2, hooksExpected: 3));

        Assert.Equal(
            $$"""{"modId":"metaphor.test","state":"ready","gameBuildSha256":"{{Build}}","hooksLoaded":2,"hooksExpected":3,"reason":null}""",
            Written);
    }

    [Fact]
    public void ARefusalCarriesItsReason()
    {
        ModStatusFile.Write(_folder.FullName, ModStatus.Refused("metaphor.test", Build, hooksExpected: 3, "unknown game build"));

        Assert.Equal(
            $$"""{"modId":"metaphor.test","state":"refused","gameBuildSha256":"{{Build}}","hooksLoaded":0,"hooksExpected":3,"reason":"unknown game build"}""",
            Written);
    }

    [Fact]
    public void ANewStatusReplacesTheOldOneWithoutLeavingTemporaryFiles()
    {
        ModStatusFile.Write(_folder.FullName, ModStatus.Refused("metaphor.test", Build, 1, "first"));
        ModStatusFile.Write(_folder.FullName, ModStatus.Ready("metaphor.test", Build, 1, 1));

        Assert.Contains("\"state\":\"ready\"", Written, StringComparison.Ordinal);
        Assert.Equal([ModStatusFile.FileName], _folder.GetFiles().Select(file => file.Name));
    }

    [Fact]
    public void ARefusalNeedsAReason() =>
        Assert.ThrowsAny<ArgumentException>(() => ModStatus.Refused("metaphor.test", Build, 1, " "));
}
