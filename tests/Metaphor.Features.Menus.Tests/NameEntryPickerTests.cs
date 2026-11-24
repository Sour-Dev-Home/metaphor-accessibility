namespace Metaphor.Features.Menus.Tests;

/// <summary>The spoken character picker for name entry on a pad (#870): moves through groups of characters, says each
/// one, and says which character to type or that one is deleted. Moves in, what to say and type out.</summary>
[Trait("Category", "Unit")]
public sealed class NameEntryPickerTests
{
    [Fact]
    public void StartsOnCapitalA()
    {
        var picker = new NameEntryPicker();

        Assert.Equal('A', picker.Current);
        Assert.Equal("Capital letters, capital A", picker.Describe());
    }

    [Fact]
    public void RightAndLeftMoveWithinTheGroupAndSayTheCharacter()
    {
        var picker = new NameEntryPicker();

        Assert.Equal(new PickerStep("capital B"), picker.Move(PickerMove.Right));
        Assert.Equal(new PickerStep("capital C"), picker.Move(PickerMove.Right));
        Assert.Equal(new PickerStep("capital B"), picker.Move(PickerMove.Left));
    }

    [Fact]
    public void LeftFromTheFirstCharacterWrapsToTheLast()
    {
        var picker = new NameEntryPicker();

        Assert.Equal(new PickerStep("capital Z"), picker.Move(PickerMove.Left));
        Assert.Equal(new PickerStep("capital A"), picker.Move(PickerMove.Right));
    }

    [Fact]
    public void DownAndUpChangeGroupAndSayItsNameAndCharacter()
    {
        var picker = new NameEntryPicker();

        Assert.Equal(new PickerStep("Small letters, a"), picker.Move(PickerMove.Down));
        Assert.Equal(new PickerStep("Digits, 0"), picker.Move(PickerMove.Down));
        Assert.Equal(new PickerStep("Symbols, space"), picker.Move(PickerMove.Down));
        Assert.Equal(new PickerStep("Capital letters, capital A"), picker.Move(PickerMove.Down));
        Assert.Equal(new PickerStep("Symbols, space"), picker.Move(PickerMove.Up));
    }

    [Fact]
    public void EachGroupKeepsItsPlaceWhenLeftAndCameBackTo()
    {
        var picker = new NameEntryPicker();
        picker.Move(PickerMove.Right);
        picker.Move(PickerMove.Right);

        picker.Move(PickerMove.Down);

        Assert.Equal(new PickerStep("Capital letters, capital C"), picker.Move(PickerMove.Up));
    }

    [Fact]
    public void PickTypesTheCurrentCharacterAndLeavesSpeakingItToTheBox()
    {
        var picker = new NameEntryPicker();
        picker.Move(PickerMove.Down);
        picker.Move(PickerMove.Right);

        Assert.Equal(new PickerStep(null, Typed: 'b'), picker.Move(PickerMove.Pick));
        Assert.Equal('b', picker.Current);
    }

    [Fact]
    public void DeleteAsksForOneCharacterRemoved()
    {
        var picker = new NameEntryPicker();

        Assert.Equal(new PickerStep(null, Deletes: true), picker.Move(PickerMove.Delete));
        Assert.Equal('A', picker.Current);
    }

    [Fact]
    public void SymbolsAreSaidByName()
    {
        var picker = new NameEntryPicker();
        picker.Move(PickerMove.Up);

        Assert.Equal(new PickerStep("hyphen"), picker.Move(PickerMove.Right));
    }

    [Fact]
    public void ResetGoesBackToCapitalAInEveryGroup()
    {
        var picker = new NameEntryPicker();
        picker.Move(PickerMove.Right);
        picker.Move(PickerMove.Down);
        picker.Move(PickerMove.Right);

        picker.Reset();

        Assert.Equal('A', picker.Current);
        Assert.Equal(new PickerStep("Small letters, a"), picker.Move(PickerMove.Down));
    }

    [Fact]
    public void EveryCharacterIsSaidAsSomething()
    {
        var picker = new NameEntryPicker();
        for (var group = 0; group < 4; group++)
        {
            var first = picker.Current;
            do
            {
                var step = picker.Move(PickerMove.Right);
                Assert.False(string.IsNullOrWhiteSpace(step.Speech));
            }
            while (picker.Current != first);
            picker.Move(PickerMove.Down);
        }
    }
}
