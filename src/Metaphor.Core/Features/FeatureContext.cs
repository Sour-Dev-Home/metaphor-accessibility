using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Cues;
using Metaphor.Core.Input;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Core.Features;

/// <summary>What the hook layer hands each <see cref="IFeatureModule"/> (#784, ADR 0022): the services features share,
/// built once, and the places a module leaves what the hooks need. Observers join <see cref="Events"/>,
/// <see cref="Cursors"/> and <see cref="Screens"/> directly, so they hear each event in module order. Controls take
/// each input read in the order they are added (<see cref="AddControls"/>). Anything else crosses by type: the hook
/// layer provides its game reads, a module provides what a hook calls or another module reads
/// (<see cref="Provide{T}"/>), one of each type.</summary>
/// <remarks>Used on the mod's start-up thread only, before any hook goes in.</remarks>
public sealed class FeatureContext
{
    private readonly List<IInputControls> controls = [];
    private readonly Dictionary<Type, object> ports = [];

    /// <summary>The settings in force, and the gates each feature speaks and takes keys through (#661).</summary>
    public required SettingsReloader Settings { get; init; }

    /// <summary>Where features speak straight away, outside the reader switch; the report log's silent screen list
    /// counts what goes through it (#597).</summary>
    public required IAnnouncer Announcer { get; init; }

    /// <summary>The speech the dialogue window can stop when it closes (#676).</summary>
    public required StoppableAnnouncer Stoppable { get; init; }

    /// <summary>The reader switch (#58, #214): automatic speech goes through it, and the reader binding toggles it.
    /// </summary>
    public required IReaderSwitch Reader { get; init; }

    /// <summary>Where features play the mod's sound cues (#636, ADR 0019).</summary>
    public required ICuePlayer Cues { get; init; }

    /// <summary>The report log (#188), or null when it couldn't be opened.</summary>
    public required ReportLog? Report { get; init; }

    /// <summary>#55's message log: null in Release builds, or when the file couldn't be opened.</summary>
    public required MessageLog? Log { get; init; }

    /// <summary>The mod's folder, where its data files ship (#687).</summary>
    public required string ModFolder { get; init; }

    /// <summary>What the message hooks report (#86).</summary>
    public required MessageEvents Events { get; init; }

    /// <summary>Each input a menu's cursor handled (#94).</summary>
    public required CursorEvents Cursors { get; init; }

    /// <summary>The menu screens (#211).</summary>
    public required MenuScreens Screens { get; init; }

    /// <summary>The message files the game compiled.</summary>
    public required MessageLibrary Library { get; init; }

    /// <summary>The messages the game fetches by serial (#584).</summary>
    public required MessageSerials Serials { get; init; }

    /// <summary>The message files by id (#545).</summary>
    public required MessageFilesById FilesById { get; init; }

    /// <summary>Messages by file and position (#108, #692).</summary>
    public required MessageFilePositions FilePositions { get; init; }

    /// <summary>The last dialogue lines (#126).</summary>
    public required DialogueHistory History { get; init; }

    /// <summary>Speakers and name tags as the screen shows them (#81, #107).</summary>
    public required GameNames Names { get; init; }

    /// <summary>Cleans a message's text for speech, with its names and terms filled in.</summary>
    public required TextCleaner Cleaner { get; init; }

    /// <summary>Which device the last input came from (#623).</summary>
    public required LastInputDevice LastDevice { get; init; }

    /// <summary>The game's tutorial table, once loaded (#118).</summary>
    public required TutorialTableSource Tutorials { get; init; }

    /// <summary>The party as the game data holds it (#247).</summary>
    public required IBattleParty BattleParty { get; init; }

    /// <summary>Whether a battle is on (#631).</summary>
    public required IBattleScene BattleScene { get; init; }

    /// <summary>The game's voice language, read on each line (#538).</summary>
    public required Func<VoiceLanguage> VoiceLanguage { get; init; }

    /// <summary>Reads one story flag (#658); null when it can't be read.</summary>
    public required Func<int, bool?> StoryFlag { get; init; }

    /// <summary>Opens the game's archive read-only (#690).</summary>
    public required Func<Stream> OpenArchive { get; init; }

    /// <summary>The controls, in the order each input read reaches them.</summary>
    public IReadOnlyList<IInputControls> Controls => controls;

    /// <summary>Adds controls after those already added: each input read reaches them with what the earlier ones
    /// handed on.</summary>
    public void AddControls(IInputControls added)
    {
        ArgumentNullException.ThrowIfNull(added);
        controls.Add(added);
    }

    /// <summary>Leaves <paramref name="port"/> for whoever asks for a <typeparamref name="T"/>. A second one of the
    /// same type is a wiring mistake and throws.</summary>
    public void Provide<T>(T port)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(port);
        if (!ports.TryAdd(typeof(T), port))
        {
            throw new InvalidOperationException($"a {typeof(T).Name} was already provided");
        }
    }

    /// <summary>The <typeparamref name="T"/> provided so far, or null for none.</summary>
    public T? Find<T>()
        where T : class => ports.TryGetValue(typeof(T), out var port) ? (T)port : null;
}
