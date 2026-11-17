namespace Metaphor.Core.Tests;

/// <summary>The input spike's change filter (#58).</summary>
[Trait("Category", "Unit")]
public sealed class InputStateChangesTests
{
    [Fact]
    public void TheFirstStateOfASourceIsAChange() =>
        Assert.True(new InputStateChanges().Changed("pad0", "buttons=0x0000"));

    [Fact]
    public void TheSameStateAgainIsNot()
    {
        var changes = new InputStateChanges();
        changes.Changed("pad0", "buttons=0x0000");

        Assert.False(changes.Changed("pad0", "buttons=0x0000"));
        Assert.True(changes.Changed("pad0", "buttons=0x0010"));
        Assert.True(changes.Changed("pad0", "buttons=0x0000"));
    }

    [Fact]
    public void EachSourceKeepsItsOwnState()
    {
        var changes = new InputStateChanges();
        changes.Changed("pad0", "buttons=0x0010");

        Assert.True(changes.Changed("pad1", "buttons=0x0010"));
        Assert.False(changes.Changed("pad0", "buttons=0x0010"));
    }

    [Fact]
    public void PastTheCapItForgetsEverySourceAndStartsAgain()
    {
        var changes = new InputStateChanges();
        for (var pad = 0; pad < InputStateChanges.MaxSources; pad++)
        {
            changes.Changed($"pad{pad}", "buttons=0x0000");
        }
        Assert.False(changes.Changed("pad0", "buttons=0x0000"));

        changes.Changed("one-too-many", "buttons=0x0000");

        Assert.True(changes.Changed("pad0", "buttons=0x0000"));
    }
}
