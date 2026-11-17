namespace Metaphor.Core.Tests;

/// <summary>The list of held keys in the game's keyboard state (#58): a count and up to six key codes.</summary>
[Trait("Category", "Unit")]
public sealed class GameKeyListTests
{
    [Fact]
    public void ReadTakesAsManyKeysAsTheCountSays()
    {
        byte[] slots = [0x52, 0x0D, 0x54, 0, 0, 0];

        Assert.Equal([0x52, 0x0D], GameKeyList.Read(2, slots));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(int.MaxValue)]
    public void ACountOutsideTheSixSlotsReadsAsNoKeys(int count)
    {
        byte[] slots = [0x52, 0x0D, 0x54, 0x41, 0x42, 0x43];

        Assert.Empty(GameKeyList.Read(count, slots));
    }

    [Fact]
    public void WriteFillsTheSlotsInOrderAndZeroesTheRest()
    {
        Assert.Equal(new byte[] { 0x0D, 0x41, 0, 0, 0, 0 }, GameKeyList.Write([0x0D, 0x41]));
    }

    [Fact]
    public void WriteRefusesMoreThanSixKeys()
    {
        Assert.Throws<ArgumentException>(() => GameKeyList.Write([1, 2, 3, 4, 5, 6, 7]));
    }
}
