using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Field.Tests;

/// <summary>The current main mission (#658): the row of the highest set flag in the main mission table names its
/// title by its label's MD5, and the title is that message's text. Spoken when it changes in free roam, at most once a
/// second, and said on "where am I". Made-up missions, flags and labels only.</summary>
[Trait("Category", "Unit")]
public sealed class MissionLineTests
{
    private const string Source = """
        @MissionText_Test_01
        {
        Walk to Testshire
        <WAIT>
        }

        @MissionText_Test_02
        {
        Queue up for <COLOR 3>mock bread<COLOR -1>
        <WAIT>
        }
        """;

    private readonly RecordingAnnouncer announcer = new();
    private readonly List<string> log = [];
    private readonly HashSet<int> set = [0x1166];
    private readonly List<MainMissionRow> rows =
    [
        new(0x1166, MessageLibrary.LabelHash("MissionText_Test_01")),
        new(0x1167, MessageLibrary.LabelHash("MissionText_Test_02")),
        new(0x1168, MessageLibrary.LabelHash("MissionText_Test_99")),
    ];

    private readonly MissionLine mission;
    private long now = 5000;
    private readonly RoamField roamField = new();
    private bool settingOn = true;
    private bool tableReadable = true;

    public MissionLineTests()
    {
        var library = new MessageLibrary();
        library.Compiled(Source);
        mission = new MissionLine(
            flag => set.Contains(flag), () => tableReadable ? rows : null, library,
            new TextCleaner(TextCleanerRules.None), announcer, () => now, new FreeRoamGate(roamField), log.Add, enabled: () => settingOn);
    }

    [Fact]
    public void WithTheWhereAmISettingOffANewMissionSaysNothing()
    {
        mission.Poll();
        settingOn = false;
        set.Add(0x1167);
        now += MissionLine.PollMilliseconds;

        Assert.Null(mission.Poll());
        Assert.Empty(Said());
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void TheCurrentMissionIsTheTitleOfTheTopFlagsRow()
    {
        Assert.Equal("Walk to Testshire", mission.Current);

        set.Add(0x1167);

        Assert.Equal("Queue up for mock bread", mission.Current);
    }

    [Fact]
    public void AnUnreadableTableAnUnknownLabelOrNoFlagHasNoMission()
    {
        tableReadable = false;
        Assert.Null(mission.Current);

        tableReadable = true;
        set.Add(0x1168);
        Assert.Null(mission.Current);

        set.Clear();
        Assert.Null(mission.Current);
    }

    [Fact]
    public void AChangeIsSpokenOnceButTheFirstMissionSeenIsNot()
    {
        Assert.Null(mission.Poll());
        set.Add(0x1167);
        now += 1000;

        Assert.Equal("Mission: Queue up for mock bread", mission.Poll());
        now += 1000;
        Assert.Null(mission.Poll());

        Assert.Equal(new Announcement("Mission: Queue up for mock bread", Interrupt: false), announcer.Said.Single());
    }

    [Fact]
    public void ItLooksAtMostOnceASecondAndOnlyInFreeRoam()
    {
        mission.Poll();
        set.Add(0x1167);
        now += 999;
        Assert.Null(mission.Poll());

        roamField.Roaming = false;
        now += 1;
        Assert.Null(mission.Poll());

        roamField.Roaming = true;
        now += 1000;
        Assert.Equal("Mission: Queue up for mock bread", mission.Poll());
    }

    [Fact]
    public void AMissionThatCantBeReadIsNotAChange()
    {
        mission.Poll();
        tableReadable = false;
        now += 1000;
        Assert.Null(mission.Poll());

        tableReadable = true;
        now += 1000;
        Assert.Null(mission.Poll());
        Assert.Empty(Said());
    }

    [Fact]
    public void TheLogNamesTheFlagAndTheLabelOfEachChange()
    {
        mission.Poll();
        set.Add(0x1168);
        now += 1000;
        mission.Poll();

        Assert.Equal(
        [
            "mission flag=0x1166 label=MissionText_Test_01 changed",
            "mission flag=0x1168 label=(unknown) hash=" + MessageLibrary.LabelHash("MissionText_Test_99") + " changed",
        ], log);
    }

    [Fact]
    public void AReadThatThrowsSaysNothing()
    {
        var throwing = new MissionLine(
            _ => throw new InvalidOperationException("stand-in"), () => rows, new MessageLibrary(),
            new TextCleaner(TextCleanerRules.None), announcer, () => now, new FreeRoamGate(roamField));

        Assert.Null(throwing.Poll());
    }

    private const string ChapterSource = """
        @MissionText_Test_10
        {
        The Test Battle:
        <WAIT>
        }

        @MissionText_Field_Test_01
        {
        Head to the mock inn
        <WAIT>
        }

        @MissionText_Field_Test_02
        {
        End the test's madness
        <WAIT>
        }
        """;

    // A chapter whose title ends with a colon, and objectives that share their flags with the main rows.
    private MissionLine Chapter(Func<IReadOnlyList<MainMissionRow>?>? objectives)
    {
        var library = new MessageLibrary();
        library.Compiled(ChapterSource);
        return new MissionLine(
            flag => set.Contains(flag), () => [new(0x1166, MessageLibrary.LabelHash("MissionText_Test_10")),
                new(0x11fd, MessageLibrary.LabelHash("MissionText_Test_10"))], library,
            new TextCleaner(TextCleanerRules.None), announcer, () => now, new FreeRoamGate(roamField), objectives: objectives);
    }

    [Fact]
    public void ATitleEndingInAColonLosesItWhenNoObjectiveFollows()
    {
        set.Add(0x11fd);

        Assert.Equal("The Test Battle", Chapter(objectives: null).Current);
        Assert.Equal("The Test Battle", Chapter(() => null).Current);
        Assert.Equal("The Test Battle", Chapter(() => [new(0x11fd, MessageLibrary.LabelHash("MissionText_Unknown"))]).Current);
    }

    [Fact]
    public void TheObjectiveOfTheTopSetFlagFollowsTheTitle()
    {
        IReadOnlyList<MainMissionRow> objectives =
        [
            new(0x11fb, MessageLibrary.LabelHash("MissionText_Field_Test_01")),
            new(0x11fd, MessageLibrary.LabelHash("MissionText_Field_Test_02")),
        ];
        var line = Chapter(() => objectives);
        set.Add(0x11fb);
        set.Add(0x11fd);

        Assert.Equal("The Test Battle: End the test's madness", line.Current);

        set.Remove(0x11fd);

        Assert.Equal("The Test Battle: Head to the mock inn", line.Current);
    }

    [Fact]
    public void ANewMissionIsSpokenWithItsObjective()
    {
        var line = Chapter(() => [new(0x11fd, MessageLibrary.LabelHash("MissionText_Field_Test_02"))]);
        Assert.Null(line.Poll());

        set.Add(0x11fd);
        now += MissionLine.PollMilliseconds;

        Assert.Equal("Mission: The Test Battle: End the test's madness", line.Poll());
    }

    private sealed class RoamField : IFieldReads
    {
        public bool Roaming { get; set; } = true;

        public bool InFreeRoam() => Roaming;

        public FieldView? Read() => null;

        public int? MapId() => null;

        public FieldPose? ReadPose() => null;
    }
}
