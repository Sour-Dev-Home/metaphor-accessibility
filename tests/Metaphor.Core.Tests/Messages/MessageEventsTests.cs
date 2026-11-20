using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests.Messages;

/// <summary>One hook per game function hands each decoded call to its observers (#86): in the order they were added,
/// and one observer's failure never reaches the others. Made-up messages only.</summary>
[Trait("Category", "Unit")]
public sealed class MessageEventsTests
{
    private readonly MessageEvents events = new();
    private readonly List<string> seen = [];

    private sealed class Recording(string name, List<string> seen)
        : ILineRequestObserver, IMessageFileObserver, IChoiceRequestObserver, ILineShownObserver, ISelectRequestObserver
    {
        public void Observe(LineRequested line) => seen.Add($"{name}:{line.Kind}:{line.Label}");

        public void Observe(SelectRequested window) =>
            seen.Add($"{name}:select:{window.Kind}:{window.PromptLabel}:{window.ChoiceLabel}");

        public void Observe(LineShown line) => seen.Add($"{name}:{line.Kind}:{line.LabelHash}");

        public void Observe(MessageFileCompiled file) => seen.Add($"{name}:{file.Id}:{file.Path}");

        public void Observe(ChoiceRequested choice) =>
            seen.Add($"{name}:{choice.Kind}:{choice.PromptLabel}:{choice.ChoiceLabel}");
    }

    private sealed class Throwing
        : ILineRequestObserver, IMessageFileObserver, IChoiceRequestObserver, ILineShownObserver, ISelectRequestObserver
    {
        public void Observe(SelectRequested window) => throw new InvalidOperationException("observer failed");

        public void Observe(LineShown line) => throw new InvalidOperationException("observer failed");

        public void Observe(LineRequested line) => throw new InvalidOperationException("observer failed");

        public void Observe(MessageFileCompiled file) => throw new InvalidOperationException("observer failed");

        public void Observe(ChoiceRequested choice) => throw new InvalidOperationException("observer failed");
    }

    [Fact]
    public void ARequestedLineReachesEachObserverInTheOrderAdded()
    {
        events.ObserveLines(new Recording("speech", seen));
        events.ObserveLines(new Recording("log", seen));

        events.Requested(new LineRequested(0, "MES_a"));

        Assert.Equal(["speech:0:MES_a", "log:0:MES_a"], seen);
    }

    [Fact]
    public void AnObserverThatThrowsDoesNotStopTheOthersFromHearingALine()
    {
        events.ObserveLines(new Recording("speech", seen));
        events.ObserveLines(new Throwing());
        events.ObserveLines(new Recording("log", seen));

        events.Requested(new LineRequested(0, "MES_a"));

        Assert.Equal(["speech:0:MES_a", "log:0:MES_a"], seen);
    }

    [Fact]
    public void ACompiledFileReachesEachObserverEvenAfterOneThrows()
    {
        events.ObserveFiles(new Throwing());
        events.ObserveFiles(new Recording("library", seen));
        events.ObserveFiles(new Recording("log", seen));

        events.Compiled(new MessageFileCompiled("event/e100.msg", 7, "@MES_a"));

        Assert.Equal(["library:7:event/e100.msg", "log:7:event/e100.msg"], seen);
    }

    [Fact]
    public void LineObserversDoNotHearCompiledFiles()
    {
        events.ObserveLines(new Recording("speech", seen));

        events.Compiled(new MessageFileCompiled("event/e100.msg", 7, "@MES_a"));

        Assert.Empty(seen);
    }

    [Fact]
    public void AnOpenedChoiceReachesEachObserverInOrderEvenAfterOneThrows()
    {
        events.ObserveChoices(new Recording("speech", seen));
        events.ObserveChoices(new Throwing());
        events.ObserveChoices(new Recording("log", seen));

        events.ChoiceOpened(new ChoiceRequested(0, "MES_q", "MES_c"));

        Assert.Equal(["speech:0:MES_q:MES_c", "log:0:MES_q:MES_c"], seen);
    }

    [Fact]
    public void AShownLineReachesEachObserverInOrderEvenAfterOneThrows()
    {
        events.ObserveShownLines(new Recording("speech", seen));
        events.ObserveShownLines(new Throwing());
        events.ObserveShownLines(new Recording("log", seen));
        events.ObserveLines(new Recording("requests", seen));

        events.Shown(new LineShown(0, "900150983CD24FB0D6963F7D28E17F72"));

        Assert.Equal(["speech:0:900150983CD24FB0D6963F7D28E17F72", "log:0:900150983CD24FB0D6963F7D28E17F72"], seen);
    }

    [Fact]
    public void LineObserversDoNotHearChoices()
    {
        events.ObserveLines(new Recording("speech", seen));

        events.ChoiceOpened(new ChoiceRequested(0, "MES_q", "MES_c"));

        Assert.Empty(seen);
    }

    // #569: a select window any game code opened, not only the scripts' choices, which keep their own event.
    [Fact]
    public void AnOpenedSelectWindowReachesEachSelectObserverInOrderEvenAfterOneThrows()
    {
        events.ObserveSelects(new Recording("speech", seen));
        events.ObserveSelects(new Throwing());
        events.ObserveSelects(new Recording("log", seen));
        events.ObserveChoices(new Recording("choices", seen));

        events.SelectOpened(new SelectRequested(1, "MES_q", "MES_c"));

        Assert.Equal(["speech:select:1:MES_q:MES_c", "log:select:1:MES_q:MES_c"], seen);
    }

    [Fact]
    public void AMissingObserverIsRefusedWhenAdded()
    {
        Assert.Throws<ArgumentNullException>("observer", () => events.ObserveFiles(null!));
        Assert.Throws<ArgumentNullException>("observer", () => events.ObserveLines(null!));
        Assert.Throws<ArgumentNullException>("observer", () => events.ObserveChoices(null!));
        Assert.Throws<ArgumentNullException>("observer", () => events.ObserveSelects(null!));
    }
}
