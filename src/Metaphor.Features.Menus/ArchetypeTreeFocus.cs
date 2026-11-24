using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the Archetype Tree (#220): its character pick, "Archetype Tree", then each row's character and
/// position; on the tree, each archetype the cursor reaches as "&lt;archetype&gt;, &lt;line&gt;, rank &lt;n&gt;", each
/// cutting off the last. The rank is the archetype's place in its line (<see cref="ClassNames"/>), the tree's position
/// until #220 settles another. Off (<see cref="MenusFeature.ArchetypeTree"/>), the pick says positions only and the tree says nothing, as
/// before. The details key on the tree (#933) says the focused archetype's study state: "Studied", or the game's
/// requirement line and the archetypes it needs, from the messages the node fetches after its name. Not said,
/// <c>[NEEDS VERIFICATION]</c>: the archetype's description (no fetch of it is pinned; the node's <c>lookup id=55</c> is
/// a lead), requirements other than archetypes, and the follower each archetype is tied to. Safe to call from several
/// threads.</summary>
/// <remarks>On build 548ddc95 (2026-11-23 evening session, docs/findings/class-name-and-battle-item-tab-serials.md): the
/// pick is <c>AcademiaClassGetCharaSel</c>, which fetches the members' names with flags 9 in row order before its first
/// cursor, the protagonist's row 0 fetching none; the tree has no cursor, and each node it focuses fetches its archetype
/// from <see cref="MessageSerials.ClassNames"/> with flags 10, then a studied node <c>System_Academy_24</c> ("Studied")
/// and one not studied <c>System_Academy_02</c> and a prerequisite archetype with flags 9.</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.ArchetypeTree)]
public sealed class ArchetypeTreeFocus : IScreenObserver, ISerialResolveObserver, ICursorObserver, IScreenDetails
{
    // The study state a node fetches after its name (docs/findings/class-name-and-battle-item-tab-serials.md).
    private const string StudiedLabel = "System_Academy_24";
    private const string RequirementLabel = "System_Academy_02";

    /// <summary>The flags the pick fetches a member's name with.</summary>
    public const int MemberFlags = 9;

    /// <summary>The flags the tree fetches its focused archetype with.</summary>
    public const int NodeFlags = 10;

    // The archetype lines' first serial, ATCategoryName_01 (docs/findings/equipment-character-archetype-serials-static.md):
    // line n is the serial before it plus n, checked against its label.
    private const int LinesBeforeFirst = 9440;

    /// <summary>The character pick, by its owner's class on build 548ddc95.</summary>
    public static Screen CharacterPick { get; } = new("Archetype Tree", "AcademiaClassGetCharaSel@ope@ui@app");

    // The screens of the tree's visit: the pick, and the details and class change it opens
    // (docs/findings/follower-and-archetype-tree-screen-classes-static.md). The tree's own fetches come while one of
    // them is the last screen told.
    private static readonly HashSet<string> TreeScreens = new(StringComparer.Ordinal)
    {
        CharacterPick.ScreenClass,
        "AcademiaClassSelectChoice@ope@ui@app",
        "AcademiaClassDetailChoice@ope@ui@app",
        "AcademiaClassDetailLearnChoice@ope@ui@app",
        "AcademiaClassChangeConfirm@ope@ui@app",
    };

    private readonly MessageSerials serials;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly Func<bool> on;
    private readonly Lock gate = new();
    private readonly List<int> members = [];
    private bool runOpen;
    private bool inTree;

    // The node focused since the last cursor input, and what it fetched after its name: "Studied", or the requirement
    // line and the archetypes it needs (#933).
    private int? node;
    private int? studyState;
    private readonly List<int> prerequisites = [];

    /// <param name="serials">The game's messages by serial.</param>
    /// <param name="announcer">Where the lines are said: the menus' speech.</param>
    /// <param name="cleaner">Cleans a message's text for speech.</param>
    /// <param name="on">Whether the names and the tree are said, read at each line.</param>
    public ArchetypeTreeFocus(MessageSerials serials, IAnnouncer announcer, TextCleaner cleaner, Func<bool> on)
    {
        ArgumentNullException.ThrowIfNull(serials);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(on);
        this.serials = serials;
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.on = on;
    }

    /// <summary>The protagonist's name as the player typed it, or null when it can't be read: the pick's row 0.</summary>
    public Func<string?>? ProtagonistName { get; init; }

    /// <summary>Speaks the pick's row, and notes whether the tree's visit goes on.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Change != ScreenChange.Left)
        {
            lock (gate)
            {
                inTree = TreeScreens.Contains(change.ScreenClass);
            }
        }
        if (change.Screen != CharacterPick || change.Change == ScreenChange.Left || change.Index < 0)
        {
            return;
        }
        string? name = null;
        if (on())
        {
            int? member;
            lock (gate)
            {
                member = change.Index >= 1 && change.Index <= members.Count ? members[change.Index - 1] : null;
            }
            name = change.Index == 0 ? ProtagonistName?.Invoke()
                : member is { } serial ? serials.TextOf(serial, cleaner) : null;
        }
        var row = change.Positioned(name);
        announcer.Announce(new Announcement(
            change.Change == ScreenChange.Entered ? $"{CharacterPick.Name}, {row}" : row, Interrupt: true));
    }

    /// <summary>Takes the pick's member names, and speaks each archetype the tree focuses.</summary>
    public void Observe(SerialResolved resolve)
    {
        if (resolve.Flags == MemberFlags && serials.InFile(resolve.Serial, MessageSerials.CharacterNames))
        {
            lock (gate)
            {
                if (!runOpen)
                {
                    members.Clear();
                    runOpen = true;
                }
                members.Add(resolve.Serial);
            }
            return;
        }
        if (resolve.Flags != NodeFlags)
        {
            TakeStudyState(resolve);
            return;
        }
        if (!on())
        {
            return;
        }
        lock (gate)
        {
            if (!inTree)
            {
                return;
            }
        }
        if (Node(resolve.Serial) is { } line)
        {
            lock (gate)
            {
                node = resolve.Serial;
                studyState = null;
                prerequisites.Clear();
            }
            announcer.Announce(new Announcement(line, Interrupt: true));
        }
    }

    /// <summary>The focused node's study state for the details key on the tree (#933), or null off the tree, before a
    /// node is focused, when nothing was fetched for it, or when it is off.</summary>
    public string? Details(ScreenEvent focus)
    {
        ArgumentNullException.ThrowIfNull(focus);
        if (!on() || !TreeScreens.Contains(focus.ScreenClass))
        {
            return null;
        }
        int? state;
        int[] needed;
        lock (gate)
        {
            if (node is null || studyState is null)
            {
                return null;
            }
            state = studyState;
            needed = [.. prerequisites];
        }
        string?[] parts = [serials.TextOf(state.Value, cleaner), .. needed.Select(serial => ClassNames.Text(serials, serial, cleaner))];
        var said = string.Join(", ", parts.OfType<string>().Where(part => part.Length > 0));
        return said.Length > 0 ? said : null;
    }

    /// <summary>Ends the pick's run of names, and the node's focus: a cursor input means a screen with a cursor has it.
    /// </summary>
    public void Observe(CursorInput input)
    {
        lock (gate)
        {
            runOpen = false;
            node = null;
        }
    }

    // After a node's name: "Studied", or the requirement line followed by the archetypes it needs (flags 9).
    private void TakeStudyState(SerialResolved resolve)
    {
        var label = serials.FindBySerial(resolve.Serial)?.Label;
        lock (gate)
        {
            if (node is null)
            {
                return;
            }
            if (label is StudiedLabel or RequirementLabel)
            {
                studyState = resolve.Serial;
                prerequisites.Clear();
            }
            else if (resolve.Flags == MemberFlags && serials.InFile(resolve.Serial, MessageSerials.ClassNames)
                && studyState is { } state && serials.FindBySerial(state)?.Label == RequirementLabel
                && !prerequisites.Contains(resolve.Serial))
            {
                prerequisites.Add(resolve.Serial);
            }
        }
    }

    // "<archetype>, <line>, rank <n>", or null for a serial that isn't a used archetype.
    private string? Node(int serial)
    {
        if (ClassNames.Place(serials, serial) is not { } place || ClassNames.Text(serials, serial, cleaner) is not { } name)
        {
            return null;
        }
        var lineSerial = LinesBeforeFirst + place.Line;
        var line = ClassNames.Line(serials, lineSerial) == place.Line ? serials.TextOf(lineSerial, cleaner) : null;
        return string.Join(", ", new[] { name, line, $"rank {place.Rank}" }.OfType<string>());
    }
}
