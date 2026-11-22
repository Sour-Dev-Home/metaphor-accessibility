using Metaphor.Core.Announcements;

namespace Metaphor.Features.Field.Tests;

/// <summary>The reach callout (#644): a door or chest is said once as it comes into reach in free roam, again only after
/// it has left reach and come back, at most one check per interval. Made-up positions only.</summary>
[Trait("Category", "Unit")]
public sealed class ReachCalloutsTests
{
    private readonly RecordingAnnouncer announcer = new();
    private readonly StandInField field = new();
    private readonly ReachCallouts reach;
    private long now = 1000;

    public ReachCalloutsTests() => reach = new ReachCallouts(field, announcer, () => now);

    private sealed class StandInField : IFieldReads
    {
        public bool Roaming { get; set; } = true;

        public FieldView? View { get; set; }

        public int Reads { get; private set; }

        public bool InFreeRoam() => Roaming;

        public FieldView? Read()
        {
            Reads++;
            return View;
        }

        public int? MapId() => null;
    }

    // Facing +Z at heading 0; a door within 150 ahead is in reach, at 900 it isn't.
    private static FieldView Standing(double heading, params FieldUnit[] units) => new(0, 0, 0, heading, units);

    private static FieldUnit Door(double z) => new(FieldKind.Door, 0, 0, (float)z);

    private void Tick(FieldView? view)
    {
        field.View = view;
        now += ReachCallouts.CheckIntervalMilliseconds;
        reach.Check();
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void EnteringReachSaysItOnceAndStayingDoesNotRepeatIt()
    {
        Tick(Standing(0, Door(900)));
        Assert.Empty(announcer.Said);

        Tick(Standing(0, Door(100)));
        Tick(Standing(0, Door(100)));
        Tick(new FieldView(0, 0, 10, 0, [Door(100)]));

        Assert.Equal(["Door, in reach"], Said());
    }

    // Enough checks, one interval apart, to outlast the leave time.
    private void Away(FieldView view)
    {
        for (var i = 0; i < ReachCallouts.LeaveMilliseconds / ReachCallouts.CheckIntervalMilliseconds; i++)
        {
            Tick(view);
        }
    }

    [Fact]
    public void LeavingAndReenteringSaysItAgain()
    {
        Tick(Standing(0, Door(100)));
        Away(Standing(0, Door(900)));
        Tick(Standing(0, Door(100)));

        Assert.Equal(["Door, in reach", "Door, in reach"], Said());
    }

    [Fact]
    public void TurningAwayAndBackSaysItAgain()
    {
        Tick(Standing(0, Door(100)));
        Away(Standing(Math.PI, Door(100)));
        Tick(Standing(0, Door(100)));

        Assert.Equal(["Door, in reach", "Door, in reach"], Said());
    }

    [Fact]
    public void StandingStillForManyIntervalsSaysItOnce()
    {
        for (var i = 0; i < 100; i++)
        {
            Tick(Standing(0, Door(100)));
        }

        Assert.Equal(["Door, in reach"], Said());
    }

    [Fact]
    public void ADoorJustPastTheGamesReachIsCalledOutButNotOneWellPastIt()
    {
        // The game's door reach is 150; the callout is said from 20% further out (#644).
        Tick(Standing(0, Door(150 * 1.3)));
        Assert.Empty(Said());

        Tick(Standing(0, Door(150 * 1.15)));
        Assert.Equal(["Door, in reach"], Said());
    }

    [Fact]
    public void StandingAtTheEdgeOfReachDoesNotFlicker()
    {
        // The door's reach is 150 and its facing limit 90°: sway either side of both, well inside the hold's slack.
        for (var i = 0; i < 50; i++)
        {
            Tick(Standing(0, Door(i % 2 == 0 ? 149 : 160)));
            Tick(Standing(i % 2 == 0 ? 0 : 1.7, Door(100)));
        }

        Assert.Equal(["Door, in reach"], Said());
    }

    [Fact]
    public void AReadThatMissesItOrADoorThatShiftsAsItOpensIsNotANewArrival()
    {
        Tick(Standing(0, Door(100)));
        Tick(Standing(0));
        Tick(Standing(0, new FieldUnit(FieldKind.Door, 30, 0, 120)));
        Tick(Standing(0, Door(100)));

        Assert.Equal(["Door, in reach"], Said());
    }

    [Fact]
    public void LeavingForLessThanTheLeaveTimeDoesNotSayItAgain()
    {
        Tick(Standing(0, Door(100)));
        Tick(Standing(0, Door(900)));
        Tick(Standing(0, Door(100)));

        Assert.Equal(["Door, in reach"], Said());
    }

    [Fact]
    public void ASecondThingComingIntoReachIsSaidOnItsOwn()
    {
        Tick(Standing(0, Door(100)));
        Tick(Standing(0, Door(100)));
        Tick(Standing(0, Door(100), new FieldUnit(FieldKind.Chest, 50, 0, 200, "Box")));
        Tick(Standing(0, Door(100), new FieldUnit(FieldKind.Chest, 50, 0, 200, "Box")));

        Assert.Equal(["Door, in reach", "Box, in reach"], Said());
    }

    [Fact]
    public void ALongDialogueDoesNotLetGoOfWhatStayedInReach()
    {
        Tick(Standing(0, Door(100)));
        field.Roaming = false;
        Away(Standing(0, Door(100)));
        field.Roaming = true;
        Tick(Standing(0, Door(100)));

        Assert.Equal(["Door, in reach"], Said());
    }

    [Fact]
    public void NothingIsReadOrSaidOutsideFreeRoam()
    {
        field.Roaming = false;
        Tick(Standing(0, Door(100)));

        Assert.Empty(announcer.Said);
        Assert.Equal(0, field.Reads);
    }

    [Fact]
    public void WhatStayedInReachThroughADialogueIsNotSaidAgainAfterIt()
    {
        Tick(Standing(0, Door(100)));
        field.Roaming = false;
        Tick(Standing(0, Door(100)));
        field.Roaming = true;
        Tick(Standing(0, Door(100)));

        Assert.Equal(["Door, in reach"], Said());
    }

    [Fact]
    public void ItQueuesBehindSpeechInsteadOfCuttingItOff()
    {
        Tick(Standing(0, Door(100)));

        Assert.False(Assert.Single(announcer.Said).Interrupt);
    }

    [Fact]
    public void SeveralComingIntoReachAreSaidNearestFirstInOneAnnouncement()
    {
        Tick(Standing(0, Door(140), new(FieldKind.Chest, 0, 0, 50, "Box")));

        Assert.Equal(["Box, in reach. Door, in reach"], Said());
    }

    [Fact]
    public void PeopleAreLeftOutUntilTheirReachIsKnown()
    {
        Tick(Standing(0, new FieldUnit(FieldKind.Person, 0, 0, 20, "Testa")));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void AnUnreadableFieldKeepsWhatWasInReach()
    {
        Tick(Standing(0, Door(100)));
        Tick(null);
        Tick(Standing(0, Door(100)));

        Assert.Equal(["Door, in reach"], Said());
    }

    [Fact]
    public void ItChecksAtMostOncePerInterval()
    {
        field.View = Standing(0, Door(100));
        reach.Check();
        now += ReachCallouts.CheckIntervalMilliseconds - 1;
        reach.Check();
        reach.Check();

        Assert.Equal(1, field.Reads);
    }

    [Fact]
    public void AFrameBetweenChecksAllocatesNothing()
    {
        field.View = Standing(0, Door(100));
        reach.Check();
        reach.Check();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            reach.Check();
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
