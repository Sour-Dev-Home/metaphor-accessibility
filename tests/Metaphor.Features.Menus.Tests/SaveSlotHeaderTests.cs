using System.Buffers.Binary;

namespace Metaphor.Features.Menus.Tests;

/// <summary>A save file's slot header (#773), in the format pinned on game build 548ddc95
/// (docs/findings/save-slot-header-in-save-files.md), and its reading from a save folder. Made-up bytes only.</summary>
[Trait("Category", "Unit")]
public sealed class SaveSlotHeaderTests : IDisposable
{
    private readonly DirectoryInfo appData = Directory.CreateTempSubdirectory("save slots ü-");

    public void Dispose() => appData.Delete(recursive: true);

    private static byte[] File(int slot, int day, int weekday, int time, int playtime, int version = 22)
    {
        var bytes = new byte[0x228 + 64];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x00), version);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x04), slot);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x08), day);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x0C), weekday);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x10), time);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x54), playtime);
        return bytes;
    }

    [Fact]
    public void TheHeaderGivesTheDayWeekdayTimeAndPlaytime() =>
        Assert.Equal(new SaveSlotHeader(124, 4, 3, 170330), SaveSlotHeader.Parse(File(0, 124, 4, 3, 170330), 0));

    [Theory]
    [InlineData(21, 4, 1, 1, 0, 643, "another version")]
    [InlineData(22, 5, 1, 1, 0, 643, "another slot's header")]
    [InlineData(22, 4, 1, 2, 0, 643, "a weekday that isn't the day mod 5")]
    [InlineData(22, 4, 1, 1, 7, 643, "a time of day past the seven")]
    [InlineData(22, 4, -1, 4, 0, 643, "a negative day")]
    [InlineData(22, 4, int.MaxValue, 2, 0, 643, "a day count past ten years")]
    [InlineData(22, 4, 1, 1, 0, -1, "a negative playtime")]
    public void AHeaderOutsideThePinnedFormatIsNotRead(
        int version, int slot, int day, int weekday, int time, int playtime, string why)
    {
        Assert.Null(SaveSlotHeader.Parse(File(slot, day, weekday, time, playtime, version), 4));
        Assert.NotEmpty(why);
    }

    [Fact]
    public void AShortFileIsNotRead() => Assert.Null(SaveSlotHeader.Parse(File(4, 1, 1, 6, 643).AsSpan(0, 0x227), 4));

    [Theory]
    [InlineData(0, 6, 1)]
    [InlineData(1, 6, 2)]
    [InlineData(124, 10, 5)]
    [InlineData(179, 11, 30)]
    [InlineData(180, 12, 1)]
    [InlineData(214, 12, 35)]
    [InlineData(215, 1, 1)]
    [InlineData(364, 5, 30)]
    [InlineData(365, 6, 1)]
    public void TheDateIsTheCalendarsMonthAndDay(int day, int month, int dayOfMonth)
    {
        var header = new SaveSlotHeader(day, day % 5, 0, 0);
        Assert.Equal((month, dayOfMonth), (header.Month, header.Day));
        Assert.Equal($"month {month}, day {dayOfMonth}", header.DateWords);
    }

    [Theory]
    [InlineData(0, "less than a minute")]
    [InlineData(59, "less than a minute")]
    [InlineData(60, "1 minute")]
    [InlineData(643, "10 minutes")]
    [InlineData(3600, "1 hour")]
    [InlineData(3660, "1 hour 1 minute")]
    [InlineData(170330, "47 hours 18 minutes")]
    public void ThePlaytimeIsHoursAndWholeMinutes(int seconds, string said) =>
        Assert.Equal(said, new SaveSlotHeader(0, 0, 0, seconds).PlaytimeWords);

    [Fact]
    public void TheLabelsAreTheListsWeekdayAndTimeOfDayMessages()
    {
        var header = new SaveSlotHeader(1, 1, 6, 643);
        Assert.Equal(("System_General_Week_02", "System_General_Time_07"), (header.WeekdayLabel, header.TimeOfDayLabel));
    }

    private DirectoryInfo SteamUser(string id) =>
        Directory.CreateDirectory(Path.Combine(appData.FullName, "SEGA", "METAPHOR", "Steam", id));

    [Fact]
    public void TheFilesAreReadFromTheSteamFolder()
    {
        var user = SteamUser("123");
        System.IO.File.WriteAllBytes(Path.Combine(user.FullName, "save0004.sav"), File(4, 1, 1, 6, 643));

        var files = SaveSlotFiles.Steam(appData.FullName);

        Assert.Equal(new SaveSlotHeader(1, 1, 6, 643), files.Read(4));
        Assert.Null(files.Read(3));
    }

    [Fact]
    public void AFileTheGameHoldsOpenForWritingStillReads()
    {
        var path = Path.Combine(SteamUser("123").FullName, "save0004.sav");
        System.IO.File.WriteAllBytes(path, File(4, 1, 1, 6, 643));
        using var writing = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        Assert.NotNull(SaveSlotFiles.Steam(appData.FullName).Read(4));
    }

    [Fact]
    public void WithSeveralSteamIdsTheOneWithTheNewestSaveIsThePlayers()
    {
        var old = SteamUser("111");
        var recent = SteamUser("222");
        var oldFile = Path.Combine(old.FullName, "save0001.sav");
        var recentFile = Path.Combine(recent.FullName, "save0001.sav");
        System.IO.File.WriteAllBytes(oldFile, File(1, 1, 1, 0, 60));
        System.IO.File.WriteAllBytes(recentFile, File(1, 2, 2, 0, 120));
        System.IO.File.SetLastWriteTimeUtc(oldFile, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        System.IO.File.SetLastWriteTimeUtc(recentFile, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SteamUser("333");

        Assert.Equal(recent.FullName, SaveSlotFiles.SteamFolder(appData.FullName));
        Assert.Equal(2, SaveSlotFiles.Steam(appData.FullName).Read(1)?.DayCount);
    }

    [Fact]
    public void WithNoSaveFolderNothingIsRead()
    {
        Assert.Null(SaveSlotFiles.SteamFolder(appData.FullName));
        Assert.Null(SaveSlotFiles.Steam(appData.FullName).Read(0));
    }

    [Fact]
    public void ReadingLeavesTheFileAsItWas()
    {
        var path = Path.Combine(SteamUser("123").FullName, "save0004.sav");
        var bytes = File(4, 1, 1, 6, 643);
        System.IO.File.WriteAllBytes(path, bytes);
        var written = System.IO.File.GetLastWriteTimeUtc(path);

        SaveSlotFiles.Steam(appData.FullName).Read(4);

        Assert.Equal(bytes, System.IO.File.ReadAllBytes(path));
        Assert.Equal(written, System.IO.File.GetLastWriteTimeUtc(path));
    }
}
