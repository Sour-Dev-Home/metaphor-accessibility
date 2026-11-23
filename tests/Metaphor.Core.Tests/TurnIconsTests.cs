using Metaphor.Core.Battle;

namespace Metaphor.Core.Tests;

/// <summary>#252's turn icons as the game data block holds them: the side's int, then the full and half icons' shorts.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TurnIconsTests
{
    private static byte[] Bytes(int side, short full, short half)
    {
        var bytes = new byte[TurnIcons.Size];
        BitConverter.GetBytes(side).CopyTo(bytes, 0);
        BitConverter.GetBytes(full).CopyTo(bytes, 4);
        BitConverter.GetBytes(half).CopyTo(bytes, 6);
        return bytes;
    }

    [Fact]
    public void ThePlayersAndTheEnemysTurnsDecode()
    {
        Assert.Equal(new TurnIcons(0, 3, 1), TurnIcons.From(Bytes(0, 3, 1)));
        Assert.Equal(new TurnIcons(1, 4, 0), TurnIcons.From(Bytes(1, 4, 0)));
    }

    [Theory]
    [InlineData(2, 4, 0)]
    [InlineData(-1, 4, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 4, 17)]
    public void AnOutOfRangeValueIsNoTurn(int side, short full, short half) =>
        Assert.Null(TurnIcons.From(Bytes(side, full, half)));

    [Fact]
    public void TooFewBytesAreNoTurn() => Assert.Null(TurnIcons.From(new byte[TurnIcons.Size - 1]));
}
