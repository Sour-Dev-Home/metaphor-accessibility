using Metaphor.Core.Announcements;

namespace Metaphor.Features.Menus.Tests;

/// <summary>A menu prompt is not cut off by the row line after it (#923): the first interrupting line within the window
/// is queued behind it, and a later one interrupts as usual.</summary>
[Trait("Category", "Unit")]
public sealed class PromptHoldTests
{
    private sealed class Reader : IReaderSwitch
    {
        public List<Announcement> Said { get; } = [];

        public bool IsOn => true;

        public void Toggle()
        {
        }

        public void Announce(Announcement announcement) => Said.Add(announcement);
    }

    private readonly Reader reader = new();
    private long now = 1000;

    private PromptHold Hold() => new(reader, () => now);

    [Fact]
    public void TheFirstRowAfterAPromptQueuesBehindIt()
    {
        var hold = Hold();
        hold.Prompts.Announce(new Announcement("Which item?", Interrupt: false));
        now += 400;
        hold.Announce(new Announcement("Potion", Interrupt: true));
        Assert.Equal([false, false], reader.Said.Select(said => said.Interrupt));
    }

    [Fact]
    public void ANextMoveWhileThePromptSpeaksInterruptsAtOnce()
    {
        var hold = Hold();
        hold.Prompts.Announce(new Announcement("Which item?", Interrupt: false));
        hold.Announce(new Announcement("Potion", Interrupt: true));
        now += 300;
        hold.Announce(new Announcement("Ether", Interrupt: true));
        Assert.True(reader.Said[2].Interrupt);
    }

    [Fact]
    public void ARowAfterTheWindowInterrupts()
    {
        var hold = Hold();
        hold.Prompts.Announce(new Announcement("Which item?", Interrupt: false));
        now += PromptHold.Window;
        hold.Announce(new Announcement("Potion", Interrupt: true));
        Assert.True(reader.Said[1].Interrupt);
    }

    [Fact]
    public void LinesWithNoPromptAreUntouched()
    {
        Hold().Announce(new Announcement("Potion", Interrupt: true));
        Assert.True(reader.Said[0].Interrupt);
    }
}
