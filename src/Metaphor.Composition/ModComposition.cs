using Metaphor.Core;
using Metaphor.Core.Announcements;
using Metaphor.Core.Audio;
using Metaphor.Core.Battle;
using Metaphor.Core.Cues;
using Metaphor.Core.Features;
using Metaphor.Core.Input;
using Metaphor.Core.Logging;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;
using Metaphor.Features.Battle;
using Metaphor.Features.Dialogue;
using Metaphor.Features.Field;
using Metaphor.Features.Menus;

namespace Metaphor.Composition;

/// <summary>The game reads the hook layer hands the composition (ADR 0026): each over memory the loader locates, or a
/// fake in a test. The composition builds everything else.</summary>
public sealed record GameReads
{
    /// <summary>The story flags, typed names and name tables (#81, #107), for the speakers and name tags.</summary>
    public required IGameNameMemory Names { get; init; }

    /// <summary>The party as the game data holds it (#247).</summary>
    public required IBattleParty BattleParty { get; init; }

    /// <summary>The battle's units (#253), when they can be read.</summary>
    public IBattleUnits? BattleUnits { get; init; }

    /// <summary>The turn icons (#252), when they can be read.</summary>
    public IBattleTurnIcons? TurnIcons { get; init; }

    /// <summary>The purse (#166), when it can be read.</summary>
    public IBattlePurse? Purse { get; init; }

    /// <summary>Whether a battle is on (#631).</summary>
    public required IBattleScene BattleScene { get; init; }

    /// <summary>The field reads (#181), made over the names the composition builds.</summary>
    public required Func<GameNames, IFieldReads> Field { get; init; }

    /// <summary>The main mission table's rows (#658).</summary>
    public required MissionRows MissionRows { get; init; }

    /// <summary>The game's glyph device (#778), or null when unread.</summary>
    public required Func<int?> GameDevice { get; init; }

    /// <summary>The game's voice language (#538).</summary>
    public required Func<VoiceLanguage> VoiceLanguage { get; init; }

    /// <summary>Reads one story flag (#658); null when it can't be read.</summary>
    public required Func<int, bool?> StoryFlag { get; init; }

    /// <summary>Opens the game's archive read-only (#690).</summary>
    public required Func<Stream> OpenArchive { get; init; }
}

/// <summary>What the composition hands back to the hook layer: the stores and event sources the hooks feed, and the
/// context whose ports the modules provided.</summary>
public sealed record ComposedMod
{
    /// <summary>What the message hooks report (#86).</summary>
    public required MessageEvents Events { get; init; }

    /// <summary>Each input a menu's cursor handled (#94).</summary>
    public required CursorEvents Cursors { get; init; }

    /// <summary>The menu screens (#211).</summary>
    public required MenuScreens Screens { get; init; }

    /// <summary>The messages the game fetches by serial (#584).</summary>
    public required MessageSerials Serials { get; init; }

    /// <summary>The last dialogue lines (#126).</summary>
    public required DialogueHistory History { get; init; }

    /// <summary>Which device the last input came from (#623).</summary>
    public required LastInputDevice LastDevice { get; init; }

    /// <summary>The game's tutorial table, once loaded (#118).</summary>
    public required TutorialTableSource Tutorials { get; init; }

    /// <summary>The field reads made over the composition's names.</summary>
    public required IFieldReads Field { get; init; }

    /// <summary>The context the modules registered into: its controls, holds, layouts and ports.</summary>
    public required FeatureContext Context { get; init; }
}

/// <summary>Composes the mod (ADR 0026, #726): builds what the features share, the message stores, names and terms,
/// cleaner, reader switch and screens, over the hook layer's <see cref="GameReads"/>, and hands them to the feature
/// modules in their order (#784, ADR 0022); each module builds its own features. The stores hear each compiled file
/// first, the features each event next, and the report log (#188) and the message log, when there are, every event
/// last.</summary>
/// <remarks>Features that play sound cues (#636) are handed the cue player through the speech factory (#723).
/// Each feature speaks and takes keys through its own setting's gate (#661), read live, so a changed file applies
/// without a restart. Used on the mod's start-up thread only.</remarks>
public static class ModComposition
{
    /// <summary>Composes <paramref name="modules"/> over <paramref name="reads"/>, speaking through
    /// <paramref name="stoppable"/> as <paramref name="settings"/> say.</summary>
    public static ComposedMod Compose(
        IReadOnlyList<IFeatureModule> modules, GameReads reads, StoppableAnnouncer stoppable, ICuePlayer cues,
        SettingsReloader settings, ReportLog? report, MessageLog? log, string modFolder, ModLog? modLog = null,
        ISettingsStore? store = null, IVolumeDuck? gameVolume = null, string? configFolder = null)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(reads);
        ArgumentNullException.ThrowIfNull(stoppable);
        ArgumentNullException.ThrowIfNull(cues);
        ArgumentNullException.ThrowIfNull(settings);
        // Each unmapped value is written once a session, and each typed name read is cleaned from the logs (#824).
        var unknowns = modLog?.Unknowns ?? UnknownValues.None;
        IAnnouncer announcer = stoppable;
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
        var names = new GameNames(reads.Names, library)
        {
            TypedNameRead = modLog is null ? null : modLog.Scrubber.AddName,
        };
        // Game terms and icons (#108, #109): a skill or condition named by its place in its file as build 548ddc95
        // compiles it (and, while "more term names" is tested, an Archetype, enemy or Royal Virtue), anything else said
        // as a stand-in for its kind, logged in Debug builds (#83). The battle banners'
        // situation file (#692) is kept by the same store: one "file + position" key.
        var filePositions = new MessageFilePositions([.. GameTerms.Steam18330018, BattleBanners.Steam18330018]);
        // The game's own glyph device names the device when it can (#778); the mod's reads are the fallback. Button
        // icons follow it (#109).
        var lastDevice = new LastInputDevice(reads.GameDevice);
        var terms = new GameTerms(
            filePositions, log is null ? null : log.PlaceholderMissing, unknowns,
            () => settings.Current.IsOn(DialogueFeature.ButtonNames) ? lastDevice.Current : null,
            () => settings.Current.IsOn(DialogueFeature.MoreTermNames));
        Action<string, string>? nameMissing = log is null ? null : log.PlaceholderMissing;
        var cleaner = new TextCleaner(
            GameNames.Rules.With(GameTerms.Rules),
            (key, arguments) => (settings.Current.IsOn(DialogueFeature.NameStandIn)
                    ? names.PlaceholderOrStandIn(key, arguments, nameMissing)
                    : names.Placeholder(key, arguments))
                ?? (settings.Current.IsOn(DialogueFeature.TermNames) ? terms.Placeholder(key, arguments) : null));
        // The reader switch (#58): automatic speech goes through it, and the dialogue's and the menus' reader binding
        // toggles it (#214).
        var reader = new DialogueReader(announcer);
        var tutorials = new TutorialTableSource();
        var screens = new MenuScreens(KnownScreens.All, () => settings.Current.IsOn(SpokenDetail.Positions));
        events.ObserveFiles(library);
        events.ObserveFiles(serials);
        events.ObserveFiles(filesById);
        events.ObserveFiles(filePositions);
        var field = reads.Field(names);
        var context = new FeatureContext
        {
            Settings = settings,
            Announcer = announcer,
            Stoppable = stoppable,
            Reader = reader,
            Cues = cues,
            Report = report,
            Unknowns = unknowns,
            Log = log,
            ModFolder = modFolder,
            ConfigFolder = configFolder,
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
            BattleParty = reads.BattleParty,
            BattleScene = reads.BattleScene,
            VoiceLanguage = reads.VoiceLanguage,
            StoryFlag = reads.StoryFlag,
            OpenArchive = reads.OpenArchive,
            SettingsStore = store ?? new NoSettingsStore(),
            GameVolume = gameVolume ?? NoVolumeDuck.Instance,
        };
        context.Provide(field);
        if (reads.TurnIcons is not null)
        {
            context.Provide(reads.TurnIcons);
        }
        if (reads.Purse is not null)
        {
            context.Provide(reads.Purse);
        }
        if (reads.BattleUnits is not null)
        {
            context.Provide(reads.BattleUnits);
        }
        context.Provide(reads.MissionRows);
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
        return new()
        {
            Events = events,
            Cursors = cursors,
            Screens = screens,
            Serials = serials,
            History = history,
            LastDevice = lastDevice,
            Tutorials = tutorials,
            Field = field,
            Context = context,
        };
    }
}
