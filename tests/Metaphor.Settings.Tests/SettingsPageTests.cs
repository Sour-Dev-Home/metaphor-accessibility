using Metaphor.Core.Settings;
using Metaphor.SettingsReference;

namespace Metaphor.Settings.Tests;

/// <summary>The players' settings reference is generated from the settings in code (#784): the committed page must
/// be exactly what the generator writes, so a hand edit or a new setting without its page fails the checks.</summary>
[Trait("Category", "Unit")]
public sealed class SettingsPageTests
{
    [Fact]
    public void TheCommittedPageIsWhatTheGeneratorWrites()
    {
        var path = Path.Combine(RepositoryRoot(), SettingsPage.RelativePath);

        var committed = File.ReadAllText(path).ReplaceLineEndings("\n");

        Assert.True(
            committed == SettingsPage.Render(AllFeatures.Catalogue()),
            $"{SettingsPage.RelativePath} is stale: {SettingsPage.Regenerate}");
    }

    [Fact]
    public void ATestingFeatureSaysSoAndIsOffByDefault()
    {
        var key = new SettingKey("speech", "bells", "Bells", Testing: true) { Description = "Rings the bells." };

        Assert.Equal("| `bells` | Rings the bells. Still being tested. | testing, off |", SettingsPage.Row(key));
        Assert.Equal("| `bells` | Rings the bells. | `true` |", SettingsPage.Row(key with { Testing = false }));
    }

    [Fact]
    public void FeaturesAreListedInTheirOrderUnderTheirGroup()
    {
        var page = SettingsPage.Render(SettingCatalogue.Of<Sample>());

        var speech = page.IndexOf("## Speech", StringComparison.Ordinal);
        var field = page.IndexOf("## Field", StringComparison.Ordinal);
        var first = page.IndexOf("| `first` |", StringComparison.Ordinal);
        var second = page.IndexOf("| `second` |", StringComparison.Ordinal);
        var walk = page.IndexOf("| `walk` |", StringComparison.Ordinal);
        Assert.True(speech < first && first < second && second < field && field < walk, page);
        Assert.EndsWith("where you'll press it.\n", page, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", page, StringComparison.Ordinal);
    }

    [Fact]
    public void AFeatureInAGroupWithNoSectionIsAMistake() =>
        Assert.Throws<ArgumentException>(() => SettingsPage.Render(SettingCatalogue.Of<Lost>()));

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

    private enum Sample
    {
        [SettingKey("field", "walk", "Walk", Order = 1, Description = "Walks.")]
        Walk,

        [SettingKey("speech", "second", "Second", Order = 3, Description = "Second.")]
        Second,

        [SettingKey("speech", "first", "First", Order = 2, Description = "First.")]
        First,
    }

    private enum Lost
    {
        [SettingKey("elsewhere", "stray", "Stray", Description = "Nowhere.")]
        Stray,
    }
}
