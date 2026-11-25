namespace Metaphor.Features.Field.Tests;

/// <summary>The map bookmarks (#877, the maintainer's decision of 2026-11-24): per install, per map id, numbered from
/// 1, kept as JSON in the mod's config folder. Made-up maps and positions.</summary>
[Trait("Category", "Unit")]
public sealed class MapBookmarksTests
{
    private readonly List<string> saved = [];

    [Fact]
    public void ABookmarkIsNumberedOnItsMapAndSaved()
    {
        var marks = new MapBookmarks(null, saved.Add);

        var first = marks.Add(7, 100, 2, -50);
        var second = marks.Add(7, 300, 2, -50);
        var other = marks.Add(9, 0, 0, 0);

        Assert.Equal((1, 2, 1), (first!.Number, second!.Number, other!.Number));
        Assert.Equal([first, second], marks.On(7));
        Assert.Empty(marks.On(8));
        Assert.Equal(3, saved.Count);
    }

    [Fact]
    public void WhatIsSavedReadsBack()
    {
        var marks = new MapBookmarks(null, saved.Add);
        marks.Add(7, 100.5f, 2, -50);
        marks.Add(9, 1, 0, 2);

        var read = new MapBookmarks(saved[^1], _ => { });

        Assert.Equal(marks.On(7), read.On(7));
        Assert.Equal(marks.On(9), read.On(9));
    }

    [Fact]
    public void AFullMapTakesNoMore()
    {
        var marks = new MapBookmarks(null, saved.Add);
        for (var i = 0; i < MapBookmarks.MaxPerMap; i++)
        {
            Assert.NotNull(marks.Add(7, i, 0, 0));
        }

        Assert.Null(marks.Add(7, 0, 0, 0));
        Assert.Equal(MapBookmarks.MaxPerMap, saved.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("""{"maps": []}""")]
    [InlineData("""{"maps": {"x": []}}""")]
    [InlineData("""{"maps": {"7": [{"x": "a", "y": 0, "z": 0}]}}""")]
    [InlineData("""{"maps": {"7": [{"x": 1}]}}""")]
    public void AFileOfAnotherShapeStartsEmptyAndSaysSo(string text)
    {
        var problems = new List<string>();

        var marks = new MapBookmarks(text, saved.Add, problems.Add);

        Assert.Empty(marks.On(7));
        Assert.Single(problems);
    }

    [Fact]
    public void AStoreWithNoFolderKeepsBookmarksForTheSessionOnly()
    {
        var marks = MapBookmarks.Load(null, _ => { });

        Assert.NotNull(marks.Add(7, 1, 0, 1));
        Assert.Single(marks.On(7));
    }
}
