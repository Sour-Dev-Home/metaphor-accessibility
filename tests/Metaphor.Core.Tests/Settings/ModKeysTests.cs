using Metaphor.Core.Settings;

namespace Metaphor.Core.Tests.Settings;

/// <summary>How the help key and the settings menu say a mod key (#868): its keyboard key and pad buttons as bound now,
/// by the controller's own names.</summary>
[Trait("Category", "Unit")]
public sealed class ModKeysTests
{
    private static readonly BoundKey Repeat = new("dialogue", "repeatKey", 0x52);
    private static readonly BoundKey RepeatPad = new("dialogue", "repeatButtons", 0x8000);
    private static readonly BoundKey Hold = new("field", "holdButtons", 0x0400);
    private static readonly BoundKey Next = new("field", "nextButton", 0x0040);

    private static ModSettings Parse(string json) => ModSettings.Parse(json, SampleSettings.Catalogue, _ => { });

    [Fact]
    public void AKeyAndAButtonAreSaidTogether() =>
        Assert.Equal("Repeat: R, or X on the pad", new ModKey("Repeat", Repeat, RepeatPad).Said(ModSettings.Defaults));

    [Fact]
    public void AHeldButtonIsSaidWithTheOneItIsHeldWith() =>
        Assert.Equal("Next: LB with D-pad down on the pad", new ModKey("Next", Button: Next, Hold: Hold).Said(ModSettings.Defaults));

    [Fact]
    public void TheSayingFollowsARebinding()
    {
        var settings = Parse("""{ "keys": { "dialogue": { "repeatKey": "Q", "repeatButtons": "LT+RT" } } }""");

        Assert.Equal("Repeat: Q, or LT and RT on the pad", new ModKey("Repeat", Repeat, RepeatPad).Said(settings));
    }

    [Fact]
    public void AnUnboundKeyIsNotSaid()
    {
        var settings = Parse("""{ "keys": { "dialogue": { "repeatKey": "none", "repeatButtons": "none" } } }""");

        Assert.Null(new ModKey("Repeat", Repeat, RepeatPad).Said(settings));
        Assert.Equal("Repeat: R", new ModKey("Repeat", Repeat, Next, Hold)
            .Said(Parse("""{ "keys": { "field": { "holdButtons": "none" } } }""")));
    }

    [Fact]
    public void AFixedKeyAndACtrlKeyAreSaidAsTheyWork()
    {
        Assert.Equal("Move: Up and Down", new ModKey("Move") { Fixed = "Up and Down" }.Said(ModSettings.Defaults));
        Assert.Equal("Read back: Control R", new ModKey("Read back", Repeat) { WithCtrl = true }.Said(ModSettings.Defaults));
    }

    [Theory]
    [InlineData(0x0001, "View")]
    [InlineData(0x0008, "Menu")]
    [InlineData(0x0004, "R3")]
    [InlineData(0x0400 | 0x0800, "LB and RB")]
    public void ButtonsAreSaidByTheControllersNames(int mask, string said) =>
        Assert.Equal(said, BindingNames.SpokenButtons(mask));

    [Theory]
    [InlineData(0x70, "F1")]
    [InlineData(0xDB, "left bracket")]
    [InlineData(0x4E, "N")]
    public void KeysAreSaidByTheirNames(int code, string said) => Assert.Equal(said, BindingNames.SpokenKey(code));
}
