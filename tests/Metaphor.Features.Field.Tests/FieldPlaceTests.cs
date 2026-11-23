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

    // Map 9's row in the map table names Map_02; map 4's names a label the file hasn't.
    private static FieldPlace WithMapTable()
    {
        var tabled = new FieldPlace(new TextCleaner(TextCleanerRules.None), map => map switch
        {
            9 => MessageLibrary.LabelHash("Map_02"),
            4 => MessageLibrary.LabelHash("Map_99"),
            _ => null,
        });
        tabled.Observe(new MessageFileCompiled("EN/message/MapName.msg", 4, Source));
        return tabled;
    }

    [Fact]
    public void ASaveLoadsRegionLookupIsFollowedByTheMapsOwnName()
    {
        var tabled = WithMapTable();
        tabled.LookedUp(FieldPlace.MapNameEntry, 0, mapId: 9);

        Assert.Equal("Testshire, Mock Harbour", tabled.Name(9));
    }

    [Fact]
    public void TheMapsOwnNameIsSaidOnceAndAloneBeforeAnyLookup()
    {
        var tabled = WithMapTable();
        Assert.Equal("Mock Harbour", tabled.Name(9));

        tabled.LookedUp(FieldPlace.MapNameEntry, 2, mapId: 9);
        Assert.Equal("Mock Harbour", tabled.Name(9));

        tabled.LookedUp(FieldPlace.MapNameEntry, 0, mapId: 9);
        Assert.Equal("Mock Harbour, Testshire", tabled.Name(9));
    }

    [Fact]
    public void AMapTheTableCantNameKeepsTheLookups()
    {
        var tabled = WithMapTable();
        tabled.LookedUp(FieldPlace.MapNameEntry, 0, mapId: 4);
        tabled.LookedUp(FieldPlace.MapNameEntry, 1, mapId: 5);

        Assert.Null(tabled.Name(4));
        Assert.Equal("Lower Testwood", tabled.Name(5));
        Assert.Equal("Lower Testwood", tabled.Name(null));
    }
}
