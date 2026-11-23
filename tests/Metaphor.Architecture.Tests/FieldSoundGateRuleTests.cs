namespace Metaphor.Architecture.Tests;

/// <summary>Rule 19 (#702): every field type that plays a cue or speaks goes through the free-roam gate.</summary>
[Trait("Category", "Unit")]
public sealed class FieldSoundGateRuleTests
{
    // Stand-ins under the real names, so the snippet needs no reference to the mod's assemblies.
    private const string Contracts = """
        namespace Metaphor.Core.Cues
        {
            public interface ICuePlayer { void Hold(int channel, int tone); void Silence(int channel); }
        }
        namespace Metaphor.Core.Announcements
        {
            public interface IAnnouncer { void Announce(string text); }
        }
        namespace Metaphor.Features.Field
        {
            public sealed class FreeRoamGate { public bool Open() => true; }
        }
        """;

    [Fact]
    public void ACueWithoutTheGateIsAViolation()
    {
        var assembly = Plant.Compile(Rules.FieldFeature, Contracts + """
            public sealed class Sonar
            {
                public void Tick(Metaphor.Core.Cues.ICuePlayer cues) => cues.Hold(1, 2);
            }
            """);

        Assert.Contains(Rules.FieldSoundsGoThroughTheGate(assembly), v => v.Contains("Sonar", StringComparison.Ordinal));
    }

    [Fact]
    public void SpeechFromALambdaWithoutTheGateIsAViolation()
    {
        var assembly = Plant.Compile(Rules.FieldFeature, Contracts + """
            public sealed class Callout
            {
                public System.Action Later(Metaphor.Core.Announcements.IAnnouncer announcer) =>
                    () => announcer.Announce("Door");
            }
            """);

        Assert.Contains(Rules.FieldSoundsGoThroughTheGate(assembly), v => v.Contains("Callout", StringComparison.Ordinal));
    }

    [Fact]
    public void ACueAfterTheGatePassesAndSilenceNeedsNoGate()
    {
        var assembly = Plant.Compile(Rules.FieldFeature, Contracts + """
            public sealed class Sonar
            {
                private readonly Metaphor.Features.Field.FreeRoamGate roam = new();

                public void Tick(Metaphor.Core.Cues.ICuePlayer cues)
                {
                    if (roam.Open())
                    {
                        cues.Hold(1, 2);
                    }
                }

                public void Stop(Metaphor.Core.Cues.ICuePlayer cues) => cues.Silence(1);
            }

            public sealed class Quiet
            {
                public void Stop(Metaphor.Core.Cues.ICuePlayer cues) => cues.Silence(1);
            }
            """);

        Assert.Empty(Rules.FieldSoundsGoThroughTheGate(assembly));
    }

    // Only the field feature: a menu speaks wherever its screen is.
    [Fact]
    public void OtherAssembliesAreNotChecked()
    {
        var assembly = Plant.Compile("Metaphor.Features.Planted", Contracts + """
            public sealed class Sonar
            {
                public void Tick(Metaphor.Core.Cues.ICuePlayer cues) => cues.Hold(1, 2);
            }
            """);

        Assert.Empty(Rules.FieldSoundsGoThroughTheGate(assembly));
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void RealFieldSoundsGoThroughTheGate(string name) =>
        Assert.Empty(Rules.FieldSoundsGoThroughTheGate(Scanned.Load(name)));
}
