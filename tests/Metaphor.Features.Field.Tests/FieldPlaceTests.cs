using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Field.Tests;

/// <summary>"Where am I" (#181): the place names the game looked up in <c>MapName.msg</c> (message id 3) since the map
/// changed, by their position in the file. Made-up places only.</summary>
[Trait("Category", "Unit")]
public sealed class FieldPlaceTests
{
    private const string Source = """
        @Map_00
        {
        Testshire
        }

        @Map_01
        {
        Lower Testwood
        }

        @Map_02
        {
        Mock Harbour
        }
        """;

    private readonly FieldPlace place = new(new TextCleaner(TextCleanerRules.None));

    public FieldPlaceTests() => place.Observe(new MessageFileCompiled("EN/message/MapName.msg", 4, Source));

    [Fact]
    public void TheFirstNamesLookedUpAfterArrivingNameThePlace()
    {
        place.LookedUp(FieldPlace.MapNameEntry, 0, mapId: 7);
        place.LookedUp(FieldPlace.MapNameEntry, 1, mapId: 7);
        place.LookedUp(FieldPlace.MapNameEntry, 2, mapId: 7);

        Assert.Equal("Testshire, Lower Testwood", place.Name(7));
    }

    [Fact]
    public void ANewMapForgetsTheOldNames()
    {
        place.LookedUp(FieldPlace.MapNameEntry, 0, mapId: 7);
        place.LookedUp(FieldPlace.MapNameEntry, 2, mapId: 8);

        Assert.Equal("Mock Harbour", place.Name(8));
        Assert.Null(place.Name(7));
    }

    [Fact]
    public void OtherFilesLookupsAndUnreadMapsAreHandled()
    {
        Assert.Null(place.Name(7));

        place.LookedUp(entry: 2, 0, mapId: 7);
        Assert.Null(place.Name(7));

        place.LookedUp(FieldPlace.MapNameEntry, 0, mapId: null);
        place.LookedUp(FieldPlace.MapNameEntry, 1, mapId: null);
        place.LookedUp(FieldPlace.MapNameEntry, 2, mapId: null);
        Assert.Equal("Lower Testwood, Mock Harbour", place.Name(null));
    }

    [Fact]
    public void ALabelPastTheFileOrBeforeItsCompileSaysNothing()
    {
        var early = new FieldPlace(new TextCleaner(TextCleanerRules.None));
        early.LookedUp(FieldPlace.MapNameEntry, 0, mapId: 1);
        place.LookedUp(FieldPlace.MapNameEntry, 9, mapId: 1);

        Assert.Null(early.Name(1));
        Assert.Null(place.Name(1));
    }

    [Fact]
    public void OnlyTheMapNameFileIsKept()
    {
        var other = new FieldPlace(new TextCleaner(TextCleanerRules.None));
        other.Observe(new MessageFileCompiled("EN/message/NotMapName.msg", 4, Source));
        other.LookedUp(FieldPlace.MapNameEntry, 0, mapId: 1);

        Assert.Null(other.Name(1));
    }
}
