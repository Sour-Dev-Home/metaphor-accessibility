using Metaphor.Core.Cues;
using Metaphor.Core.Settings;
using Metaphor.SettingsReference;

namespace Metaphor.Settings.Tests;

/// <summary>Every setting as it stands: key, spoken label, default with no file, and whether it is still being tested.
/// One snapshot file per project that declares settings, <c>Snapshots/&lt;assembly&gt;.txt</c>, so a feature PR edits
/// only its own project's file and two projects never touch the same lines (#892). A change to a line is a change a
/// reviewer sees there; an added, removed or changed setting, or a project with no file, fails.</summary>
[Trait("Category", "Unit")]
public sealed class SettingsSnapshotTests
{
    private const string Folder = "tests/Metaphor.Settings.Tests/Snapshots";

    [Fact]
    public void EveryProjectsSettingsKeepTheirKeyLabelDefaultAndTestingMark()
    {
        var actual = Actual();
        var folder = Path.Combine(RepositoryRoot(), Folder);
        var files = Directory.GetFiles(folder, "*.txt").Select(path => Path.GetFileNameWithoutExtension(path))
            .Order(StringComparer.Ordinal).ToList();
        var projects = actual.Keys.Order(StringComparer.Ordinal).ToList();

        Assert.True(
            projects.SequenceEqual(files),
            $"{Folder} needs one <assembly>.txt per project with settings: have [{string.Join(", ", files)}], need "
            + $"[{string.Join(", ", projects)}]");
        foreach (var (project, lines) in actual)
        {
            var expected = File.ReadAllLines(Path.Combine(folder, project + ".txt")).Where(line => line.Length > 0);
            Assert.True(
                expected.SequenceEqual(lines),
                $"{Folder}/{project}.txt differs from the settings in code. It should read:\n{string.Join("\n", lines)}");
        }
    }

    [Fact]
    public void ASessionThatTurnsTestingOnTurnsEveryFeatureOn() =>
        Assert.All(AllFeatures.Catalogue().Features,
            feature => Assert.True(ModSettings.Defaults.WithTesting(true).IsOn(feature)));

    /// <summary>The snapshot lines of each project, by the assembly that declares the setting's enum.</summary>
    private static Dictionary<string, List<string>> Actual()
    {
        var defaults = ModSettings.Defaults;
        var settings = AllFeatures.Catalogue().Features.Concat(Enum.GetValues<Cue>().Select(cue => (Enum)cue));
        return settings
            .Select(setting => (Project: setting.GetType().Assembly.GetName().Name!, Setting: setting))
            .GroupBy(entry => entry.Project, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => Line(SettingKey.Of(entry.Setting), IsOn(defaults, entry.Setting)))
                    .Order(StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);
    }

    private static bool IsOn(ModSettings defaults, Enum setting) =>
        setting is Cue cue ? defaults.Sound(cue).On : defaults.IsOn(setting);

    private static string Line(SettingKey key, bool on) =>
        $"{key.Path} | {key.Spoken} | {(on ? "on" : "off")} | {(key.Testing ? "testing" : "done")}";

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
