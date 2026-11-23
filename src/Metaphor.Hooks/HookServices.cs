using Metaphor.Core;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Cues;
using Metaphor.Core.Features;
using Metaphor.Core.Input;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;
using Metaphor.Features.Battle;
using Metaphor.Features.Dialogue;
using Metaphor.Features.Field;
using Metaphor.Features.Menus;

namespace Metaphor.Hooks;

/// <summary>What hooks are handed through their constructor (#85, architecture rule 12): the entry point builds it once
/// speech has started and the dev log is open, and the loader passes it to each hook it creates.</summary>
/// <remarks>Not a positional record: its generated <c>Deconstruct</c> writes through <c>out</c> parameters, which
/// architecture rule 8 flags in the hook layer.</remarks>
internal sealed record HookServices
{
    /// <summary>Where the message hooks report what the game compiled and asked for (#86).</summary>
    public required MessageEvents Messages { get; init; }

    /// <summary>Where the menu cursor hook reports each input a menu's cursor handled (#94).</summary>
    public required CursorEvents Cursors { get; init; }

    /// <summary>#55's message log, for the listening hooks that remain: null in Release builds, or when the file
    /// couldn't be opened.</summary>
    public required MessageLog? Log { get; init; }

    /// <summary>The last dialogue lines (#126), for the repeat key (#58) and the backlog fallback (#114). Empty unless
    /// the dialogue feature is feeding it.</summary>
    public DialogueHistory DialogueHistory { get; init; } = new();

    /// <summary>Every feature's controls, each behind its setting's gate (#661), in the order the feature modules added
    /// them (#784): the input hooks' frame (#301) hands each read to the first, what it leaves to the next, and the
    /// dialogue window's message pointer (#232) to those that take it.</summary>
    public IReadOnlyList<IInputControls> Controls { get; init; } = [];

    /// <summary>The menu screens (#211): the input hooks' frame (#301) hands them each read of <see cref="MenusOpen"/>
    /// (#268) before the menu controls take the read.</summary>
    public required MenuScreens MenuScreens { get; init; }

    /// <summary>Reads whether the game has its menus open (#268); the loader locates what it reads (#300).</summary>
    public MenusOpenRead MenusOpen { get; init; } = new();

    /// <summary>Where the name entry hook reports each update of the box (#585): nowhere unless the menus feature is
    /// speaking it.</summary>
    public INameEntryObserver NameEntry { get; init; } = new NoNameEntry();

    /// <summary>What the input hooks' frame (#301) shows each keyboard read to, unchanged: the name entry's Enter hint
    /// (#585), or nothing.</summary>
    public IKeyWatcher? KeyWatcher { get; init; }

    /// <summary>What the input hooks' frame (#301) shows each pad read to, unchanged: the settings confirm questions
    /// (#562), or nothing.</summary>
    public IPadWatcher? PadWatcher { get; init; }

    /// <summary>What the input hooks' frame (#301) tells which device each read came from (#623), or nothing.</summary>
    public LastInputDevice? LastDevice { get; init; }

    /// <summary>Whether a name entry screen is up (#585), so the keyboard hook's dev log gives a key count, not the
    /// codes of a name being typed.</summary>
    public Func<bool> NameEntryUp { get; init; } = () => false;

    /// <summary>Reads the message the dialogue window holds (#232); the loader locates what it reads (#300).</summary>
    public DialogueWindowPointer DialogueWindow { get; init; } = new();

    /// <summary>Reads the story flags and typed names (#81, #107); the loader locates what it reads.</summary>
    public GameDataRead GameData { get; init; } = new();

    /// <summary>Reads the character name table (#81); the loader locates what it reads.</summary>
    public DataTableRead CharacterTable { get; init; } = DataTableRead.Characters();

    /// <summary>Reads the follower table (#107); the loader locates what it reads.</summary>
    public DataTableRead FollowerTable { get; init; } = DataTableRead.Followers();

    /// <summary>Every reader of a game global, which the loader hands each global it locates before any hook goes in.
    /// </summary>
    public IReadOnlyList<IReadsGlobal> Globals =>
        [MenusOpen, DialogueWindow, GameData, CharacterTable, FollowerTable, Voice, BattleScene, FieldWorld, FieldScene,
            FieldDatabase, PersonTable, MissionTable, InputDevice];

    /// <summary>Reads whether a battle is on, for the status key (#631); the loader locates what it reads.</summary>
    public BattleSceneRead BattleScene { get; init; } = new();

    /// <summary>Reads <c>fldWorld</c> for the nearby list (#181); the loader locates it.</summary>
    public FieldGlobal FieldWorld { get; init; } = FieldGlobal.World();

    /// <summary>Reads the field's scene root for free roam (#181); the loader locates it.</summary>
    public FieldGlobal FieldScene { get; init; } = FieldGlobal.SceneRoot();

    /// <summary>Reads the field database for the map id (#181); the loader locates it.</summary>
    public FieldGlobal FieldDatabase { get; init; } = FieldGlobal.Database();

    /// <summary>Reads the person table for people's names (#181); the loader locates it.</summary>
    public DataTableRead PersonTable { get; init; } = DataTableRead.People();

    /// <summary>The place names "where am I" says (#181): the message lookup hook feeds it.</summary>
    public FieldPlace? FieldPlace { get; init; }

    /// <summary>Reads the main mission table (#658); the loader locates what it reads.</summary>
    public MissionTableRead MissionTable { get; init; } = new();

    /// <summary>Reads the game's glyph device for the button names (#778); the loader locates what it reads.</summary>
    public InputDeviceRead InputDevice { get; init; } = new();

    /// <summary>The field reads (#181), for the map id beside each place-name lookup.</summary>
    public FieldMemory? Field { get; init; }

    /// <summary>Reads the game's voice language for the auto voiced-lines mode (#538); the loader locates what it reads
    /// (#300).</summary>
    public VoiceLanguageRead Voice { get; init; } = new();

    /// <summary>Names each cursor manager's screen class (#208): the choice cursor getter hook feeds it, and the menu
    /// cursor hook asks it for each input.</summary>
    public ScreenClassLookup ScreenClasses { get; init; } = new();

    /// <summary>The messages the game fetches by serial (#584), from the files it compiled; empty unless the message
    /// file hook is feeding it.</summary>
    public MessageSerials Serials { get; init; } = new([]);

    /// <summary>Where the battle hit hook reports each target an action hit (#247).</summary>
    public IBattleHitObserver BattleHits { get; init; } = new NoBattleHits();

    /// <summary>Says the battle's situation banners (#692): the banner hooks hand it each one shown; null for none.
    /// </summary>
    public BattleBanners? BattleBanners { get; init; }

    /// <summary>The party as the game data holds it (#247), for the battle hit hook's sides and the status key.</summary>
    public required BattlePartyMemory BattleParty { get; init; }

    /// <summary>The game's tutorial table (#118), which the entry point loads from the game's archive once the hooks
    /// are in.</summary>
    public TutorialTableSource Tutorials { get; init; } = new();

    /// <summary>The field's map and objective tables (#742, #743), which the entry point loads from the game's archive
    /// once the hooks are in.</summary>
    public FieldTablesSource FieldTables { get; init; } = new();

    /// <summary>Speaks tutorial pop-ups as they open (#118); silent unless built by <see cref="Speaking"/>.</summary>
    public TutorialPopups TutorialPopups { get; init; } =
        new(new MessageLibrary(), new SilentAnnouncer(), new TextCleaner(TextCleanerRules.None), () => null);

    /// <summary>Where features play the mod's sound cues (#636, ADR 0019); silent unless the entry point starts one.
    /// </summary>
    public ICuePlayer Cues { get; init; } = new SilentCuePlayer();

    /// <summary>The travel map's destination panel (#545): the panel hook hands it each destination shown.</summary>
    public required TravelMapDestination TravelMap { get; init; }

    /// <summary>The field map's name strip (#586): its composer and set hooks hand it each place's name and showing.
    /// </summary>
    public required FieldMapName FieldMapName { get; init; }

    /// <summary>The mod's folder, outside the repository, where a development run leaves files for its hooks.</summary>
    public required string ModFolder { get; init; }

    /// <summary>Services for the mod in <paramref name="modFolder"/>, speaking through <paramref name="stoppable"/>
    /// as <paramref name="settings"/> say. This builds what the features share, the game reads and the message stores,
    /// and hands them to <paramref name="modules"/> in their order (#784, ADR 0022); each module builds its own
    /// features. The stores hear each compiled file first, the features each event next, and the report log (#188) and
    /// the message log, when there are, every event last.</summary>
    /// <remarks>Features that play sound cues (#636) are handed <paramref name="cues"/>. Each feature speaks and takes
    /// keys through its own setting's gate (#661), read live, so a changed file applies without a restart.</remarks>
    public static HookServices Speaking(
        IReadOnlyList<IFeatureModule> modules, StoppableAnnouncer stoppable, ICuePlayer cues, SettingsReloader settings,
        ReportLog? report, MessageLog? log, string modFolder)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(stoppable);
        IAnnouncer announcer = stoppable;
        ArgumentNullException.ThrowIfNull(cues);
        ArgumentNullException.ThrowIfNull(settings);
        // The silent screen list (#597) counts everything the features below say, so it hears each screen event after
        // them; its entries go to the report log only.
        var silence = report is null ? null : new SilentScreens(announcer, report, () => settings.Current);
        announcer = (IAnnouncer?)silence ?? announcer;
        var library = new MessageLibrary();
        // Serials as build 548ddc95 numbers them; the message file hook that feeds it installs on that build only.
        var serials = new MessageSerials(MessageSerials.Steam18330018);
        var filesById = new MessageFilesById();
        var history = new DialogueHistory();
        var events = new MessageEvents();
        var cursors = new CursorEvents();
        // Speakers and name tags are named as the screen shows them (#81, #107), read from the game when a line is
        // shown; a read that fails falls back to the file's speaker, and drops the tag as before.
        var gameData = new GameDataRead();
        var party = new BattlePartyMemory(gameData);
        var characterTable = DataTableRead.Characters();
        var followerTable = DataTableRead.Followers();
        var names = new GameNames(new GameNameMemory(gameData, characterTable, followerTable), library);
        // Game terms and icons (#108, #109): a skill or condition named by its place in its file as build 548ddc95
        // compiles it, anything else said as a stand-in for its kind, logged in Debug builds (#83). The battle banners'
        // situation file (#692) is kept by the same store: one "file + position" key.
        var filePositions = new MessageFilePositions([.. GameTerms.Steam18330018, BattleBanners.Steam18330018]);
        var terms = new GameTerms(filePositions, log is null ? null : log.PlaceholderMissing);
        var cleaner = new TextCleaner(
            GameNames.Rules.With(GameTerms.Rules),
            (key, arguments) => names.Placeholder(key, arguments)
                ?? (settings.Current.IsOn(DialogueFeature.TermNames) ? terms.Placeholder(key, arguments) : null));
        // The reader switch (#58): automatic speech goes through it, and the dialogue's and the menus' reader binding
        // toggles it (#214).
        var reader = new DialogueReader(announcer);
        var voiceLanguage = new VoiceLanguageRead();
        var tutorials = new TutorialTableSource();
        // The game's own glyph device names the device when it can (#778); the mod's reads are the fallback.
        var inputDevice = new InputDeviceRead();
        var lastDevice = new LastInputDevice(inputDevice.Value);
        var screens = new MenuScreens(KnownScreens.All);
        events.ObserveFiles(library);
        events.ObserveFiles(serials);
        events.ObserveFiles(filesById);
        events.ObserveFiles(filePositions);
        // The game reads the features use; the loader locates what each reads (#300).
        var menusOpen = new MenusOpenRead();
        var window = new DialogueWindowPointer();
        var fieldWorld = FieldGlobal.World();
        var fieldScene = FieldGlobal.SceneRoot();
        var fieldDatabase = FieldGlobal.Database();
        var personTable = DataTableRead.People();
        var field = new FieldMemory(fieldWorld, fieldScene, fieldDatabase, personTable, names, menusOpen, window);
        // Debug builds only: the "where am I" record lists the main-mission flags, read through the build-checked flag
        // global (#658, docs/findings/mission-box-text-source-static.md).
        var missionTable = new MissionTableRead(log is null ? null : log.FieldRead);
        // The status key acts only in a battle (#631); a Debug build logs the scene each press read.
        var battleScene = new BattleSceneRead { Log = log };
        var context = new FeatureContext
        {
            Settings = settings,
            Announcer = announcer,
            Stoppable = stoppable,
            Reader = reader,
            Cues = cues,
            Report = report,
            Log = log,
            ModFolder = modFolder,
            Events = events,
            Cursors = cursors,
            Screens = screens,
            Library = library,
            Serials = serials,
            FilesById = filesById,
            FilePositions = filePositions,
            History = history,
            Names = names,
            Cleaner = cleaner,
            LastDevice = lastDevice,
            Tutorials = tutorials,
            BattleParty = party,
            BattleScene = battleScene,
            VoiceLanguage = voiceLanguage.Read,
            StoryFlag = gameData.StoryFlag,
            OpenArchive = OpenArchive,
        };
        context.Provide<IFieldReads>(field);
        context.Provide(new MissionRows(missionTable.Rows));
        // Each module builds its features, in the list's order: their observers hear each event, and their controls
        // take each input read, in that order (#784, ADR 0022).
        foreach (var module in modules)
        {
            module.Register(context);
        }
        if (silence is not null)
        {
            screens.Observe(silence);
        }
        if (report is not null)
        {
            events.ObserveFiles(report);
            events.ObserveLines(report);
            events.ObserveChoices(report);
            cursors.Observe(report);
        }
        if (log is not null)
        {
            events.ObserveFiles(log);
            events.ObserveLines(log);
            events.ObserveChoices(log);
            events.ObserveSelects(log);
            cursors.Observe(log);
        }
        // What the hooks call on, as the modules provided it.
        var nameEntry = context.Find<NameEntrySpeech>();
        return new()
        {
            Messages = events,
            Cursors = cursors,
            Log = log,
            ModFolder = modFolder,
            Serials = serials,
            BattleHits = context.Find<IBattleHitObserver>() ?? new NoBattleHits(),
            BattleParty = party,
            BattleBanners = context.Find<BattleBanners>(),
            BattleScene = battleScene,
            Controls = context.Controls,
            TravelMap = context.Find<TravelMapDestination>() ?? throw Missing(nameof(TravelMapDestination)),
            FieldMapName = context.Find<FieldMapName>() ?? throw Missing(nameof(FieldMapName)),
            DialogueHistory = history,
            MenuScreens = screens,
            NameEntry = (INameEntryObserver?)nameEntry ?? new NoNameEntry(),
            KeyWatcher = nameEntry,
            PadWatcher = context.Find<IPadWatcher>(),
            LastDevice = lastDevice,
            NameEntryUp = nameEntry is null ? () => false : nameEntry.OnScreen,
            GameData = gameData,
            CharacterTable = characterTable,
            FollowerTable = followerTable,
            Voice = voiceLanguage,
            Tutorials = tutorials,
            FieldTables = context.Find<FieldTablesSource>() ?? new(),
            TutorialPopups = context.Find<TutorialPopups>() ?? throw Missing(nameof(TutorialPopups)),
            MenusOpen = menusOpen,
            DialogueWindow = window,
            FieldWorld = fieldWorld,
            FieldScene = fieldScene,
            FieldDatabase = fieldDatabase,
            PersonTable = personTable,
            Field = field,
            FieldPlace = context.Find<FieldPlace>(),
            MissionTable = missionTable,
            InputDevice = inputDevice,
            Cues = cues,
        };
    }

    private static InvalidOperationException Missing(string port) => new($"no feature module provided a {port}");

    // The game's archive, base.cpk beside the exe, opened read-only; a throw is logged by the navmesh source.
    private static FileStream OpenArchive() => File.OpenRead(Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? throw new FileNotFoundException("no game folder"),
        "base.cpk"));
}

/// <summary>A name entry observer that ignores every update: what the hook reports to when no feature listens.</summary>
internal sealed class NoNameEntry : INameEntryObserver
{
    public void Observe(NameEntryFrame frame)
    {
    }
}
