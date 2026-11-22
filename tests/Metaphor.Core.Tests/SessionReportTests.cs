using Metaphor.Core.Menus;

namespace Metaphor.Core.Tests;

/// <summary>The report log's player lines (#597): a marker for each press of the mark key and an entry for each screen
/// or focus move the mod said nothing for. Numbers and class names only, never game text or a path.</summary>
[Trait("Category", "Unit")]
public sealed class SessionReportTests
{
    private static (ReportLog Log, StringWriter Text) Rebuild(long now)
    {
        var text = new StringWriter();
        return (new ReportLog(text, () => now, []), text);
    }

    private static string Written(ReportLog log, StringWriter text)
    {
        Assert.True(log.WaitForWrites(TimeSpan.FromSeconds(5)));
        return text.ToString();
    }

    [Fact]
    public void AMarkerSaysItsNumberAndHowFarIntoTheSessionItCame()
    {
        var (log, text) = Rebuild(now: 3_723_456);

        log.Mark(2);

        Assert.Equal($"3723456 ms mark number=2 session=1:02:03{Environment.NewLine}", Written(log, text));
    }

    [Fact]
    public void ASilentFocusMoveNamesItsScreenClassIndexAndCount()
    {
        var (log, text) = Rebuild(now: 800);

        log.Silent(new ScreenEvent(ScreenChange.FocusMoved, null, "ItemMenuList@ope@ui@app", Index: 4, Count: 12));

        Assert.Equal(
            $"800 ms silent change=FocusMoved screen=ItemMenuList@ope@ui@app index=4 count=12{Environment.NewLine}",
            Written(log, text));
    }

    [Fact]
    public void ACountTheScreenDoesNotGiveIsAQuestionMark()
    {
        var (log, text) = Rebuild(now: 5);

        log.Silent(new ScreenEvent(ScreenChange.Entered, null, "Foo@ui@app", Index: 0));

        Assert.Equal($"5 ms silent change=Entered screen=Foo@ui@app index=0 count=?{Environment.NewLine}", Written(log, text));
    }

    [Theory]
    [InlineData("The bridge is out, Ada.")]
    [InlineData(@"D:\Games\Metaphor ReFantazio")]
    public void AScreenClassThatIsNotAClassNameIsCountedInstead(string screenClass)
    {
        var (log, text) = Rebuild(now: 5);

        log.Silent(new ScreenEvent(ScreenChange.Entered, null, screenClass, Index: 0));

        var written = Written(log, text);
        Assert.DoesNotContain(screenClass, written, StringComparison.Ordinal);
        Assert.Contains($"screen=(not a class name, {screenClass.Length} chars)", written, StringComparison.Ordinal);
    }

    [Fact]
    public void AFullListSaysSoOnce()
    {
        var (log, text) = Rebuild(now: 9);

        log.SilentListFull(200);

        Assert.Equal(
            $"9 ms silent list full at 200 entries; later silent screens aren't listed{Environment.NewLine}",
            Written(log, text));
    }
}
