using Metaphor.Core.Menus;

namespace Metaphor.Core.Tests.Menus;

/// <summary>The menu cursor hook hands each handled input to its observers (#94), as the message hooks do (#86): in
/// the order they were added, only when a manager's cursor or command changed, and one observer's failure never
/// reaches the others.</summary>
[Trait("Category", "Unit")]
public sealed class CursorEventsTests
{
    private const int Move = 0x16;
    private const int Settle = 0x1;

    private readonly CursorEvents events = new();
    private readonly List<string> seen = [];

    private sealed class Recording(string name, List<string> seen) : ICursorObserver
    {
        public void Observe(CursorInput input) =>
            seen.Add($"{name}:0x{input.Manager:x}:{input.Command:x}:{input.Index}");
    }

    private sealed class Throwing : ICursorObserver
    {
        public void Observe(CursorInput input) => throw new InvalidOperationException("observer failed");
    }

    private static CursorInput Input(long manager, int command, int row, int top = 0) =>
        new(manager, ClassRva: 0x100, command, Result: 1, top, row, Flags: 0x46);

    [Fact]
    public void AnInputReachesEachObserverInTheOrderAdded()
    {
        events.Observe(new Recording("menus", seen));
        events.Observe(new Recording("log", seen));

        events.Handled(Input(0xa0, Settle, row: 2));

        Assert.Equal(["menus:0xa0:1:2", "log:0xa0:1:2"], seen);
    }

    [Fact]
    public void TheIndexIsTheTopPlusTheRow()
    {
        events.Observe(new Recording("menus", seen));

        events.Handled(Input(0xa0, Settle, row: 2, top: 3));

        Assert.Equal(["menus:0xa0:1:5"], seen);
    }

    [Fact]
    public void AnInputLikeThatManagersLastIsDropped()
    {
        events.Observe(new Recording("menus", seen));

        events.Handled(Input(0xa0, Settle, row: 0));
        events.Handled(Input(0xa0, Settle, row: 0));
        events.Handled(Input(0xa0, Move, row: 1));
        events.Handled(Input(0xa0, Settle, row: 1));
        events.Handled(Input(0xa0, Settle, row: 1));

        Assert.Equal(["menus:0xa0:1:0", "menus:0xa0:16:1", "menus:0xa0:1:1"], seen);
    }

    [Fact]
    public void EachManagerIsComparedWithItsOwnLastInput()
    {
        events.Observe(new Recording("menus", seen));

        events.Handled(Input(0xa0, Settle, row: 0));
        events.Handled(Input(0xb0, Settle, row: 0));
        events.Handled(Input(0xa0, Settle, row: 0));

        Assert.Equal(["menus:0xa0:1:0", "menus:0xb0:1:0"], seen);
    }

    [Fact]
    public void AnObserverThatThrowsDoesNotStopTheOthers()
    {
        events.Observe(new Recording("menus", seen));
        events.Observe(new Throwing());
        events.Observe(new Recording("log", seen));

        events.Handled(Input(0xa0, Settle, row: 1));

        Assert.Equal(["menus:0xa0:1:1", "log:0xa0:1:1"], seen);
    }

    [Theory]
    [InlineData(0x14, true)]
    [InlineData(0x15, true)]
    [InlineData(0x16, true)]
    [InlineData(0x17, true)]
    [InlineData(0x1, false)]
    [InlineData(0x18, false)]
    public void TheFourMoveCommandsAreMoves(int command, bool isMove) =>
        Assert.Equal(isMove, Input(0xa0, command, row: 0).IsMove);
}
