using Metaphor.Core.Menus;

namespace Metaphor.Core.Tests.Menus;

/// <summary>The #250 menu-state read: the walk that replays the game's open test, over made-up memory.</summary>
[Trait("Category", "Unit")]
public sealed class MenuOpenStateTests
{
    private static Func<long, long?> Memory(Dictionary<long, long> cells) =>
        address => cells.TryGetValue(address, out var value) ? value : null;

    [Fact]
    public void KindPicksTheLink()
    {
        Assert.Equal(0x68, MenuOpenState.NextOffset(1));
        Assert.Equal(0x78, MenuOpenState.NextOffset(2));
        Assert.Equal(0x68, MenuOpenState.NextOffset(3));
        Assert.Null(MenuOpenState.NextOffset(0));
        Assert.Null(MenuOpenState.NextOffset(4));
    }

    [Fact]
    public void FindsTheNodeCarryingTheId()
    {
        var memory = Memory(new()
        {
            [0x1000 + 0xB0] = 5,
            [0x1000 + 0x68] = 0x2000,
            [0x2000 + 0xB0] = 9,
            [0x2000 + 0x68] = 0,
        });

        Assert.Equal(0x2000, MenuOpenState.FindNode(0x1000, 0x68, 9, memory));
        Assert.Equal(0, MenuOpenState.FindNode(0x1000, 0x68, 7, memory));
        Assert.Equal(0, MenuOpenState.FindNode(0, 0x68, 7, memory));
    }

    [Fact]
    public void AFailedReadOrALoopIsUnknown()
    {
        Assert.Null(MenuOpenState.FindNode(0x1000, 0x68, 9, Memory(new() { [0x1000 + 0xB0] = 5 })));
        var loop = Memory(new() { [0x1000 + 0xB0] = 5, [0x1000 + 0x68] = 0x1000 });
        Assert.Null(MenuOpenState.FindNode(0x1000, 0x68, 9, loop));
    }

    [Fact]
    public void LogsNumbersAndClassesOnly()
    {
        Assert.Equal("camp=none", new MenuOpenState(0, null, null, null, null, null).ToString());
        Assert.Equal(
            "camp=0xab class=Task@x kind=2 id=0x10 alive=1 node=Node@x",
            new MenuOpenState(0xab, "Task@x", 2, 0x10, true, "Node@x").ToString());
        Assert.Equal(
            "camp=0xab class=? kind=? id=? alive=? node=-",
            new MenuOpenState(0xab, null, null, null, null, null).ToString());
    }
}
