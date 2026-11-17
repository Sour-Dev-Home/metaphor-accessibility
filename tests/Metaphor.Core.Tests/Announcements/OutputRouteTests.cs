using Metaphor.Core.Announcements;

namespace Metaphor.Core.Tests.Announcements;

/// <summary>Speech and braille through Prism's <c>output</c>, with <c>speak</c> for backends without it (#115).</summary>
[Trait("Category", "Unit")]
public sealed class OutputRouteTests
{
    [Fact]
    public void TriesOutputFirst() => Assert.True(new OutputRoute().TriesOutput);

    [Fact]
    public void KeepsUsingOutputWhileItWorks()
    {
        var route = new OutputRoute();

        Assert.False(route.SpeakInstead(0)); // PRISM_OK
        Assert.True(route.TriesOutput);
    }

    [Fact]
    public void SpeaksInsteadAndStopsTryingOutputWhenNotImplemented()
    {
        var route = new OutputRoute();

        Assert.True(route.SpeakInstead(OutputRoute.NotImplemented));
        Assert.False(route.TriesOutput);
    }

    [Theory]
    [InlineData(1)] // PRISM_ERROR_NOT_INITIALIZED
    [InlineData(2)] // PRISM_ERROR_INVALID_PARAM
    [InlineData(4)] // PRISM_ERROR_NO_VOICES
    [InlineData(6)] // PRISM_ERROR_SPEAK_FAILURE
    [InlineData(9)] // PRISM_ERROR_INTERNAL: NVDA's braille failing after its speech worked
    public void DoesNotSpeakAgainAfterOtherFailures(int status)
    {
        var route = new OutputRoute();

        Assert.False(route.SpeakInstead(status));
        Assert.True(route.TriesOutput);
    }

    [Fact]
    public void NotImplementedIsPrismsValue() => Assert.Equal(3, OutputRoute.NotImplemented);
}
