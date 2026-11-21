using Metaphor.Core.Announcements;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The field map's name strip (#586): when the strip is set to a place, the name composed for it is said,
/// remembered by the place's id. Made-up names only.</summary>
[Trait("Category", "Unit")]
public sealed class FieldMapNameTests
{
    private readonly RecordingAnnouncer announcer = new();
    private readonly FieldMapName strip;

    public FieldMapNameTests()
    {
        strip = new FieldMapName(announcer, new TextCleaner(TextCleanerRules.None));
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void TheComposedNameIsSaidWhenTheStripIsSetToItsPlace()
    {
        strip.Composed(12, "Grey Coast, <COLOR 2>Lower Caves");
        strip.Shown(12);

        Assert.Equal(["Grey Coast, Lower Caves"], Said());
        Assert.True(announcer.Said.Single().Interrupt);
    }

    [Fact]
    public void APlaceSetAgainWithoutBeingComposedIsStillSaid()
    {
        strip.Composed(12, "Grey Coast, Lower Caves");
        strip.Composed(13, "Grey Coast, Upper Caves");
        strip.Shown(12);
        strip.Shown(13);
        strip.Shown(12);

        Assert.Equal(["Grey Coast, Lower Caves", "Grey Coast, Upper Caves", "Grey Coast, Lower Caves"], Said());
    }

    [Fact]
    public void APlaceWhoseNameWasNotReadSaysNothing()
    {
        strip.Composed(12, null);
        strip.Composed(13, "<COLOR 2>");
        strip.Shown(12);
        strip.Shown(13);
        strip.Shown(14);

        Assert.Empty(Said());
    }
}
