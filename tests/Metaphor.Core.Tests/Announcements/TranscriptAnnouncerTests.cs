using Metaphor.Core.Announcements;

namespace Metaphor.Core.Tests.Announcements;

/// <summary>The transcript (ADR 0004): what the game would have said, in order, readable without listening.</summary>
[Trait("Category", "Unit")]
public sealed class TranscriptAnnouncerTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("metaphor-transcript-").FullName;
    private readonly RecordingAnnouncer speech = new();

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private string TranscriptPath => Path.Combine(folder, TranscriptAnnouncer.FileName);

    [Fact]
    public void WritesEachAnnouncementInOrderAndStillSpeaksIt()
    {
        var announcer = new TranscriptAnnouncer(speech, folder);

        announcer.Announce(new Announcement("Ada: The bridge is out.", Interrupt: true));
        announcer.Announce(new Announcement("The wind picks up.", Interrupt: false));

        Assert.Equal(["Ada: The bridge is out.", "The wind picks up."], File.ReadAllLines(TranscriptPath));
        Assert.Equal(2, speech.Said.Count);
    }

    [Fact]
    public void KeepsEachAnnouncementOnOneLine()
    {
        var announcer = new TranscriptAnnouncer(speech, folder);

        announcer.Announce(new Announcement("First part\r\nsecond part", Interrupt: true));

        Assert.Equal(["First part second part"], File.ReadAllLines(TranscriptPath));
    }

    [Fact]
    public void StartsAFreshTranscriptForEachRun()
    {
        File.WriteAllText(TranscriptPath, "from an earlier run\n");

        new TranscriptAnnouncer(speech, folder).Announce(new Announcement("Now.", Interrupt: true));

        Assert.Equal(["Now."], File.ReadAllLines(TranscriptPath));
    }

    [Fact]
    public void AFailedWriteNeitherThrowsNorStopsTheSpeech()
    {
        var missing = Path.Combine(folder, "no-such-folder");

        var announcer = new TranscriptAnnouncer(speech, missing);
        announcer.Announce(new Announcement("Still said.", Interrupt: true));

        Assert.Equal([new Announcement("Still said.", Interrupt: true)], speech.Said);
    }
}
