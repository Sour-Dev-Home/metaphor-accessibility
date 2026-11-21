using Mono.Cecil;

namespace Metaphor.Architecture.Tests;

/// <summary>Rule 18 (#661): a feature class that speaks, plays a cue or takes a key is marked with the setting that
/// turns it off.</summary>
[Trait("Category", "Unit")]
public sealed class FeatureSettingRuleTests
{
    // Core's shapes, as the rule reads them by name.
    private static readonly AssemblyDefinition Core = Plant.Compile("Metaphor.Core", """
        namespace Metaphor.Core.Announcements
        {
            public sealed record Announcement(string Text, bool Interrupt);
            public interface IAnnouncer { void Announce(Announcement announcement); }
            public interface IReaderSwitch : IAnnouncer { void Toggle(); }
        }
        namespace Metaphor.Core.Cues
        {
            public enum Cue { AllyHit }
            public interface ICuePlayer { void Play(Cue cue); }
        }
        namespace Metaphor.Core.Input
        {
            public interface IInputControls { int Pad(int pad, int buttons, bool made); }
            public interface IKeyWatcher { void Keys(System.Collections.Generic.IReadOnlyList<int> keys); }
        }
        namespace Metaphor.Core.Settings
        {
            public enum Feature { Bells }
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class FeatureSettingAttribute(params Feature[] features) : System.Attribute
            {
                public Feature[] Features { get; } = features;
            }
        }
        """);

    private static IReadOnlyList<string> Check(string source) =>
        Rules.FeaturesHaveSettings(Plant.Compile("Metaphor.Features.Bells", """
            using Metaphor.Core.Announcements;
            using Metaphor.Core.Cues;
            using Metaphor.Core.Input;
            using Metaphor.Core.Settings;
            """ + source, references: Core));

    [Fact]
    public void AFeatureThatSpeaksWithoutASettingIsAViolationThatSaysWhatToAdd()
    {
        var violations = Check("""
            public sealed class Bell(IAnnouncer announcer) { public void Ring() => announcer.Announce(new("Ding", true)); }
            """);

        var violation = Assert.Single(violations);
        Assert.Contains("Bell speaks", violation, StringComparison.Ordinal);
        Assert.Contains("Metaphor.Core.Settings.Feature", violation, StringComparison.Ordinal);
        Assert.Contains("[FeatureSetting(Feature.", violation, StringComparison.Ordinal);
        Assert.Contains("HookServices.Speaking", violation, StringComparison.Ordinal);
        Assert.Contains("docs/players/reference/settings.md", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void SpeakingFromALambdaCountsForTheClassThatHoldsIt()
    {
        var violations = Check("""
            public sealed class Bell(IAnnouncer announcer)
            {
                public System.Action Later(string text) => () => announcer.Announce(new(text, true));
            }
            """);

        Assert.Contains("Bell speaks", Assert.Single(violations), StringComparison.Ordinal);
    }

    [Fact]
    public void SpeakingThroughAReaderSwitchCounts()
    {
        Assert.Single(Check("""
            public sealed class Bell(IReaderSwitch reader) { public void Ring() => reader.Announce(new("Ding", true)); }
            """));
    }

    [Fact]
    public void AFeatureThatPlaysACueWithoutASettingIsAViolation()
    {
        Assert.Contains("Bell plays a cue", Assert.Single(Check("""
            public sealed class Bell(ICuePlayer cues) { public void Ring() => cues.Play(Cue.AllyHit); }
            """)), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("public sealed class Bell : IInputControls { public int Pad(int pad, int buttons, bool made) => buttons; }")]
    [InlineData("public sealed class Bell : IKeyWatcher { public void Keys(System.Collections.Generic.IReadOnlyList<int> keys) { } }")]
    public void AFeatureThatTakesKeysWithoutASettingIsAViolation(string source)
    {
        Assert.Contains("Bell takes keys", Assert.Single(Check(source)), StringComparison.Ordinal);
    }

    [Fact]
    public void AFeatureMarkedWithItsSettingPasses()
    {
        Assert.Empty(Check("""
            [FeatureSetting(Feature.Bells)]
            public sealed class Bell(IAnnouncer announcer, ICuePlayer cues) : IInputControls
            {
                public void Ring() { announcer.Announce(new("Ding", true)); cues.Play(Cue.AllyHit); }
                public int Pad(int pad, int buttons, bool made) => buttons;
            }
            """));
    }

    // A switch or filter that implements the announcer speaks only what it's handed; the feature handing it speaks.
    [Fact]
    public void AnAnnouncerThatPassesSpeechOnIsNotAFeature()
    {
        Assert.Empty(Check("""
            public sealed class Muffle(IAnnouncer inner) : IAnnouncer
            {
                public void Announce(Announcement announcement) => inner.Announce(announcement with { Interrupt = false });
            }
            """));
    }

    [Fact]
    public void AClassThatNeitherSpeaksNorPlaysNorTakesKeysPasses()
    {
        Assert.Empty(Check("public static class Chime { public static string Text() => \"Ding\"; }"));
    }

    [Fact]
    public void OnlyFeaturesAreChecked()
    {
        var core = Plant.Compile(
            "Metaphor.Other",
            "public sealed class Bell(Metaphor.Core.Announcements.IAnnouncer a) { public void Ring() => a.Announce(new(\"Ding\", true)); }",
            references: Core);

        Assert.Empty(Rules.FeaturesHaveSettings(core));
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void RealAssembliesComply(string name) => Assert.Empty(Rules.FeaturesHaveSettings(Scanned.Load(name)));
}
