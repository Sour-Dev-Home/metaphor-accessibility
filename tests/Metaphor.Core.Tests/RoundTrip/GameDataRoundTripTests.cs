namespace Metaphor.Core.Tests.RoundTrip;

public sealed class GameDataRoundTripTests
{
    /// <summary>Results on game data are pinned to a game build (AGENTS.md ground rule 2), so the data must say which.</summary>
    [RoundTripFact]
    public void TheGameDataNamesItsGameBuild() =>
        Assert.True(GameData.ThisRun.Data!.HasKnownBuild, $"no valid game build in {GameData.BuildFileName}");
}
