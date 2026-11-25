using Mono.Cecil;

namespace Metaphor.Architecture.Tests;

/// <summary>Rule 18 by construction (#723): a feature project takes its announcers and cue players from the speech
/// factory, never from an ungated route.</summary>
[Trait("Category", "Unit")]
public sealed class FeatureOutputRuleTests
{
    // Core's shapes, as the rule reads them by name.
    private static readonly AssemblyDefinition Core = Plant.Compile("Metaphor.Core", """
        namespace Metaphor.Core.Announcements
        {
            public interface IAnnouncer { void Announce(string text); }
        }
        namespace Metaphor.Core.Cues
        {
            public interface ICuePlayer { void Play(int cue); }
        }
        namespace Metaphor.Core.Settings
        {
            public sealed class SettingsReloader
            {
                public Metaphor.Core.Announcements.IAnnouncer Speech(System.Enum feature, Metaphor.Core.Announcements.IAnnouncer inner) => inner;
            }
        }
        namespace Metaphor.Core.Features
        {
            public sealed class FeatureOutput
            {
                public Metaphor.Core.Announcements.IAnnouncer Now => null!;
                public Metaphor.Core.Cues.ICuePlayer Cues => null!;
            }
            public sealed class FeatureContext
            {
                public Metaphor.Core.Settings.SettingsReloader Settings => new();
                public Metaphor.Core.Announcements.IAnnouncer Announcer => null!;
                public Metaphor.Core.Cues.ICuePlayer Cues => null!;
                public FeatureOutput Output(params System.Enum[] features) => new();
            }
        }
        """);

    private static IReadOnlyList<string> Check(string source, string name = "Metaphor.Features.Bells") =>
        Rules.FeaturesTakeOutputsFromTheFactory(Plant.Compile(name, """
            using Metaphor.Core.Features;
            public enum BellsFeature { Bells }
            """ + source, references: Core));

    [Theory]
    [InlineData("context.Settings.Speech(BellsFeature.Bells, context.Announcer)", "Speech")]
    [InlineData("context.Announcer", "get_Announcer")]
    public void AModuleThatBuildsItsOwnGateOrReadsTheUngatedAnnouncerIsAViolation(string speech, string call)
    {
        var violations = Check($$"""
            public sealed class BellsModule
            {
                public object Register(FeatureContext context) => {{speech}};
            }
            """);

        Assert.Contains(violations, violation => violation.Contains(call, StringComparison.Ordinal) &&
            violation.Contains("BellsModule.Register", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadingTheUngatedCuePlayerIsAViolation()
    {
        var violations = Check("""
            public sealed class BellsModule
            {
                public object Register(FeatureContext context) => context.Cues;
            }
            """);

        Assert.Contains(violations, violation => violation.Contains("get_Cues", StringComparison.Ordinal));
    }

    [Fact]
    public void OutputsFromTheFactoryPass()
    {
        Assert.Empty(Check("""
            public sealed class BellsModule
            {
                public object[] Register(FeatureContext context) =>
                    [context.Output(BellsFeature.Bells).Now, context.Output(BellsFeature.Bells).Cues];
            }
            """));
    }

    [Fact]
    public void OnlyFeaturesAreChecked()
    {
        Assert.Empty(Check(
            "public sealed class Wiring { public object Make(FeatureContext context) => context.Announcer; }",
            "Metaphor.Other"));
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void RealAssembliesComply(string name) =>
        Assert.Empty(Rules.FeaturesTakeOutputsFromTheFactory(Scanned.Load(name)));
}
