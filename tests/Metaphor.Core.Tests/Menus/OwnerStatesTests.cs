using Metaphor.Core.Menus;

namespace Metaphor.Core.Tests.Menus;

/// <summary>The choices whose fields a Debug build logs (#806, #624, #219, #851): each lead's span, and none for any
/// other class.</summary>
[Trait("Category", "Unit")]
public sealed class OwnerStatesTests
{
    [Theory]
    [InlineData("SystemTutorialChoice@ope@ui@app", 0x1478)]
    [InlineData("ResultDistributionPointChoice@ope@ui@app", 0x3e0)]
    [InlineData("EquipClassDetailChoice@ope@ui@app", 0x1700)]
    [InlineData("StatusMenuClassDetailChoice@ope@ui@app", 0x1700)]
    [InlineData("AcademiaClassDetailChoice@ope@ui@app", 0x1700)]
    [InlineData("BTL_ItemSelect@ope@ui@app", 0x4440)]
    public void EachLeadsChoiceLogsItsFields(string screenClass, int first) =>
        Assert.Equal(first, OwnerStates.Of(screenClass)[0].Offset);

    [Theory]
    [InlineData("RootMenuChoice@ope@ui@app")]
    [InlineData(null)]
    public void AnyOtherClassLogsNone(string? screenClass) => Assert.Empty(OwnerStates.Of(screenClass));

    [Fact]
    public void NoSpanIsLongerThanTheCap()
    {
        string[] classes =
        [
            "SystemTutorialChoice@ope@ui@app", "ResultDistributionPointChoice@ope@ui@app",
            "EquipClassDetailChoice@ope@ui@app", "BTL_ItemSelect@ope@ui@app",
        ];
        Assert.All(classes.SelectMany(OwnerStates.Of), span => Assert.InRange(span.Length, 1, OwnerStates.MaxLength));
    }
}
