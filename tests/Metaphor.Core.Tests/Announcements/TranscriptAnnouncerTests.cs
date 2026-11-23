using Metaphor.Core.Announcements;

namespace Metaphor.Core.Tests.Announcements;

/// <summary>The transcript (ADR 0004): what the game would have said, in order, readable without listening.</summary>
[Trait("Category", "Unit")]
public sealed class TranscriptAnnouncerTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("metaphor-transcript-").FullName;
    private readonly RecordingAnnouncer speech = new();
    private long now;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private string TranscriptPath => Path.Combine(folder, TranscriptAnnouncer.FileName);

    [Fact]
    public void WritesEachAnnouncementInOrderAndStillSpeaksIt()
    {
        var announcer = new TranscriptAnnouncer(speech, folder, () => now);

        now = 1500;
        announcer.Announce(new Announcement("Ada: The bridge is out.", Interrupt: true));
        now = 172230;
        announcer.Announce(new Announcement("The wind picks up.", Interrupt: false));

        // #763: each line starts with the report log's clock, "<ms> ms "; #824: then whether it cut in or waited.
        Assert.Equal(
            ["1500 ms interrupting: Ada: The bridge is out.", "172230 ms queued: The wind picks up."],
            File.ReadAllLines(TranscriptPath));
        Assert.Equal(2, speech.Said.Count);
    }

    [Fact]
    public void KeepsEachAnnouncementOnOneLine()
    {
        var announcer = new TranscriptAnnouncer(speech, folder, () => now);

        announcer.Announce(new Announcement("First part\r\nsecond part", Interrupt: true));

        Assert.Equal(["0 ms interrupting: First part second part"], File.ReadAllLines(TranscriptPath));
    }

    // #585: the name the player types is spoken but never written down.
    [Fact]
    public void APrivateAnnouncementIsSpokenButOnlyItsLengthIsWritten()
    {
        var announcer = new TranscriptAnnouncer(speech, folder, () => now);
        var typed = new Announcement("Ada", Interrupt: true, Private: true);

        announcer.Announce(typed);

        Assert.Equal(["0 ms interrupting: " + TranscriptAnnouncer.PrivateLine(3)], File.ReadAllLines(TranscriptPath));
        Assert.DoesNotContain("Ada", File.ReadAllText(TranscriptPath), StringComparison.Ordinal);
        Assert.DoesNotContain("Ada", typed.ToString(), StringComparison.Ordinal);
        Assert.Equal([typed], speech.Said);
    }

    [Fact]
    public void StartsAFreshTranscriptForEachRun()
    {
        File.WriteAllText(TranscriptPath, "from an earlier run\n");

        new TranscriptAnnouncer(speech, folder, () => now).Announce(new Announcement("Now.", Interrupt: true));

        Assert.Equal(["0 ms interrupting: Now."], File.ReadAllLines(TranscriptPath));
    }

    [Fact]
    public void AFailedWriteNeitherThrowsNorStopsTheSpeech()
    {
        var missing = Path.Combine(folder, "no-such-folder");

        var announcer = new TranscriptAnnouncer(speech, missing, () => now);
        announcer.Announce(new Announcement("Still said.", Interrupt: true));

        Assert.Equal([new Announcement("Still said.", Interrupt: true)], speech.Said);
    }
}
