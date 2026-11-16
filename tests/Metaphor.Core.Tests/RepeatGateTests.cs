namespace Metaphor.Core.Tests;

/// <summary>The gate that keeps a spike log readable when the game repeats a lookup every frame.</summary>
[Trait("Category", "Unit")]
public sealed class RepeatGateTests
{
    [Fact]
    public void LetsAKeyThroughOncePerWindow()
    {
        long now = 0;
        var gate = new RepeatGate(windowMilliseconds: 100, () => now);

        Assert.True(gate.LetThrough(1, 2));
        now = 99;
        Assert.False(gate.LetThrough(1, 2));
        now = 100;
        Assert.True(gate.LetThrough(1, 2));
    }

    [Fact]
    public void KeepsKeysApart()
    {
        var gate = new RepeatGate(100, () => 0);

        Assert.True(gate.LetThrough(1, 2));
        Assert.True(gate.LetThrough(2, 1));
        Assert.True(gate.LetThrough(1, 3));
        Assert.False(gate.LetThrough(2, 1));
    }

    [Fact]
    public void ForgetsEverythingPastItsLimit()
    {
        var gate = new RepeatGate(100, () => 0);
        for (var i = 0; i < RepeatGate.MaxKeys; i++)
        {
            gate.LetThrough(i, 0);
        }

        Assert.True(gate.LetThrough(-1, 0));
        Assert.True(gate.LetThrough(0, 0));
    }

    [Fact]
    public void LetsEachKeyThroughOnceAcrossThreads()
    {
        var gate = new RepeatGate(1000, () => 0);
        var letThrough = 0;

        Parallel.For(0, 400, i =>
        {
            if (gate.LetThrough(i % 4, 0))
            {
                Interlocked.Increment(ref letThrough);
            }
        });

        Assert.Equal(4, letThrough);
    }
}
