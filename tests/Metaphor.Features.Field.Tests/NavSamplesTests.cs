using Metaphor.Core.Cues;

namespace Metaphor.Features.Field.Tests;

/// <summary>The generated navigation sounds played in place of the tones (#841): every committed file loads, each kind
/// finds its own, and with the setting off or a file missing a feature gets none and plays its tone.</summary>
[Trait("Category", "Unit")]
public sealed class NavSamplesTests
{
    /// <summary>The bank of the repository's <c>data/sounds/</c>, read as the mod reads its folder.</summary>
    internal static SoundBank Shipped { get; } = SoundBank.Load(
        Path.Combine(RepositoryRoot(), "data", "sounds"), problem => throw new InvalidOperationException(problem));

    [Fact]
    public void EveryKindFindsItsOwnCommittedFile()
    {
        var samples = new NavSamples(Shipped, () => true);
        var found = new List<CueSample?>();
        found.AddRange(Enum.GetValues<SonarKind>().Select(samples.For));
        found.AddRange(Enum.GetValues<WallDirection>().Select(samples.For));
        found.AddRange(Enum.GetValues<RoutePingKind>().Select(samples.For));

        Assert.All(found, Assert.NotNull);
        Assert.Equal(found.Count, found.Distinct().Count());
        Assert.Equal(Sonar.PingMilliseconds, samples.For(SonarKind.People)!.Milliseconds);
        Assert.Equal(1000, samples.For(WallDirection.Left)!.Milliseconds);
        Assert.Equal(RoutePings.Steps(RoutePingKind.Straight).Sum(step => step.Milliseconds),
            samples.For(RoutePingKind.Straight)!.Milliseconds);
    }

    [Fact]
    public void OffOrMissingGivesNoSound()
    {
        var on = false;
        var samples = new NavSamples(Shipped, () => on);
        Assert.Null(samples.For(SonarKind.Exits));
        on = true;
        Assert.NotNull(samples.For(SonarKind.Exits));
        Assert.Null(new NavSamples(SoundBank.Empty, () => true).For(SonarKind.Exits));
        Assert.Null(NavSamples.None.For(WallDirection.Ahead));
    }

    [Fact]
    public void AWallFileFoldsBackToItsCentredLoopSoThePanIsTheMixers()
    {
        // The left wall's file was written panned left; folded, it is the same loop as the centred ahead one would be at
        // its pitch: a steady sine, every sample within the peak.
        var left = new NavSamples(Shipped, () => true).For(WallDirection.Left)!;
        Assert.Equal(1f, left.Samples.ToArray().Max(), 3);
        Assert.Equal(-1f, left.Samples.ToArray().Min(), 3);
    }

    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Metaphor.sln")))
            {
                return folder.FullName;
            }
        }
        throw new DirectoryNotFoundException("no Metaphor.sln above the test folder");
    }
}
