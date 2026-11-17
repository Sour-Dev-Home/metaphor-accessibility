using Metaphor.Core.Menus;

namespace Metaphor.Core.Tests.Menus;

/// <summary>The #155 spike's gate on the game's value getters: a getter a menu calls every frame logs once per visit,
/// and again when its value changes. Made-up numbers only.</summary>
[Trait("Category", "Unit")]
public sealed class MenuValueSpyTests
{
    [Fact]
    public void LetsAFirstReadThroughButNotItsRepeat()
    {
        var gate = new ValueReadGate(quietMilliseconds: 1000, () => 0);

        Assert.True(gate.LetThrough(new ValueRead("GetMoney", [], 1200)));
        Assert.False(gate.LetThrough(new ValueRead("GetMoney", [], 1200)));
    }

    [Fact]
    public void LetsAChangedResultThrough()
    {
        var gate = new ValueReadGate(1000, () => 0);

        gate.LetThrough(new ValueRead("GetBagItem", [0x1005], 3));

        Assert.True(gate.LetThrough(new ValueRead("GetBagItem", [0x1005], 2)));
        Assert.False(gate.LetThrough(new ValueRead("GetBagItem", [0x1005], 2)));
    }

    [Fact]
    public void LetsARepeatThroughAfterAQuietSpellButNotWhileCalledEveryFrame()
    {
        long now = 0;
        var gate = new ValueReadGate(quietMilliseconds: 1000, () => now);
        var read = new ValueRead("GetMoney", [], 1200);
        gate.LetThrough(read);

        now = 900;
        Assert.False(gate.LetThrough(read));
        now = 1800;
        Assert.False(gate.LetThrough(read));
        now = 2800;
        Assert.True(gate.LetThrough(read));
    }

    [Fact]
    public void KeepsGettersAndArgumentsApart()
    {
        var gate = new ValueReadGate(1000, () => 0);

        Assert.True(gate.LetThrough(new ValueRead("UnitGetParam", [0x500, 1], 7)));
        Assert.True(gate.LetThrough(new ValueRead("UnitGetParam", [0x500, 2], 7)));
        Assert.True(gate.LetThrough(new ValueRead("UnitGetParam", [0x5002], 7)));
        Assert.True(gate.LetThrough(new ValueRead("GetBagItem", [0x500, 1], 7)));
        Assert.False(gate.LetThrough(new ValueRead("UnitGetParam", [0x500, 2], 7)));
    }

    [Fact]
    public void ForgetsEverythingPastItsLimit()
    {
        var gate = new ValueReadGate(1000, () => 0);
        gate.LetThrough(new ValueRead("GetMoney", [], 1));
        for (var i = 0; i < ValueReadGate.MaxKeys; i++)
        {
            gate.LetThrough(new ValueRead("GetBagItem", [i], 1));
        }

        Assert.True(gate.LetThrough(new ValueRead("GetMoney", [], 1)));
    }

    [Fact]
    public void WritesAReadAsNamesAndDecimalNumbers()
    {
        Assert.Equal("fn=UnitGetParam args=4096,3 result=-2", new ValueRead("UnitGetParam", [0x1000, 3], -2).ToString());
        Assert.Equal("fn=GetMoney args=none result=1200", new ValueRead("GetMoney", [], 1200).ToString());
    }
}
