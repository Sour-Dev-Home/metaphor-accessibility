using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests.Messages;

/// <summary>One hook per game function hands each decoded call to its observers (#86): in the order they were added,
/// and one observer's failure never reaches the others. Made-up messages only.</summary>
[Trait("Category", "Unit")]
public sealed class MessageEventsTests
{
    private readonly MessageEvents events = new();
    private readonly List<string> seen = [];

    private sealed class Recording(string name, List<string> seen) : ILineRequestObserver, IMessageFileObserver
    {
        public void Observe(LineRequested line) => seen.Add($"{name}:{line.Kind}:{line.Label}");

        public void Observe(MessageFileCompiled file) => seen.Add($"{name}:{file.Id}:{file.Path}");
    }

    private sealed class Throwing : ILineRequestObserver, IMessageFileObserver
    {
        public void Observe(LineRequested line) => throw new InvalidOperationException("observer failed");

        public void Observe(MessageFileCompiled file) => throw new InvalidOperationException("observer failed");
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
}
