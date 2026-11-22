using Metaphor.Core.Menus;

namespace Metaphor.Core.Tests;

/// <summary>The settings pages' working copy, logged in Debug builds for #558's checks 2 and 3 (#560): which classes are
/// the four pages, and a page's bytes logged only when they are new or changed. Made-up bytes only.</summary>
[Trait("Category", "Unit")]
public sealed class SettingsPagesTests
{
    [Theory]
    [InlineData("?$ConfigMenuChoicePcT@$0HG@@ope@ui@app", true)]
    [InlineData("?$ConfigMenuChoicePcT@$0HH@@ope@ui@app", true)]
    [InlineData("?$ConfigMenuChoicePcT@$0HI@@ope@ui@app", true)]
    [InlineData("?$ConfigMenuChoicePcT@$0HJ@@ope@ui@app", true)]
    [InlineData("?$ConfigTopChoicePc@VConfigTopChoicePcLayoutInit@ui@app@@@ope@ui@app", false)]
    [InlineData("ConfigMenuChoiceKeyConfig@ope@ui@app", false)]
    [InlineData(null, false)]
    public void TheFourPagesAreTheOnesWhoseCopyIsLogged(string? screenClass, bool page)
    {
        Assert.Equal(page, SettingsPages.IsPage(screenClass));
    }

    [Fact]
    public void APagesBytesAreGivenTheFirstTimeAndWhenTheyChange()
    {
        var copies = new ChangedBytes();

        Assert.Equal("0102ff", copies.Changed(0xa0, [0x01, 0x02, 0xff]));
        Assert.Null(copies.Changed(0xa0, [0x01, 0x02, 0xff]));
        Assert.Equal("0103ff", copies.Changed(0xa0, [0x01, 0x03, 0xff]));
    }

    // A copy as the page keeps it: 8 bytes of header, the setting count (51) at +0x8, then a dword per ID from +0x10.
    private static byte[] Copy(int count = SettingValues.Count, params (int Id, int Value)[] values)
    {
        var bytes = new byte[SettingsPages.WorkingCopyLength];
        BitConverter.GetBytes(count).CopyTo(bytes, 0x8);
        foreach (var (id, value) in values)
        {
            BitConverter.GetBytes(value).CopyTo(bytes, 0x10 + (4 * id));
        }
        return bytes;
    }

    [Fact]
    public void ACopysValuesAreReadByTheirSettingId()
    {
        var values = SettingValues.Of(Copy(values: [(0, 100), (27, 77), (50, -1)]));

        Assert.NotNull(values);
        Assert.Equal(100, values.Value(0));
        Assert.Equal(77, values.Value(27));
        Assert.Equal(-1, values.Value(50));
        Assert.Equal(0, values.Value(1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(51)]
    public void AnIdOutsideTheCopyHasNoValue(int id)
    {
        Assert.Null(SettingValues.Of(Copy())!.Value(id));
    }

    [Theory]
    [InlineData(50)]
    [InlineData(0)]
    [InlineData(0x7ffe)]
    public void ACopyWithAnotherSettingCountIsNotRead(int count)
    {
        Assert.Null(SettingValues.Of(Copy(count)));
    }

    [Fact]
    public void ACopyTooShortForEveryIdIsNotRead()
    {
        Assert.Null(SettingValues.Of(Copy().AsSpan(0, 0x10 + (4 * SettingValues.Count) - 1)));
    }

    [Fact]
    public void TwoCopiesWithTheSameValuesAreEqual()
    {
        Assert.Equal(SettingValues.Of(Copy(values: [(17, 1)])), SettingValues.Of(Copy(values: [(17, 1)])));
        Assert.NotEqual(SettingValues.Of(Copy(values: [(17, 1)])), SettingValues.Of(Copy(values: [(17, 2)])));
    }

    [Fact]
    public void EachPageKeepsItsOwnBytes()
    {
        var copies = new ChangedBytes();
        copies.Changed(0xa0, [0x01]);

        Assert.Equal("01", copies.Changed(0xb0, [0x01]));
        Assert.Null(copies.Changed(0xa0, [0x01]));
    }
}
