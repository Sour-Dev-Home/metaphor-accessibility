using Metaphor.Core.Battle;
using Metaphor.Core.Features;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>The battle project's wiring and settings (#784, ADR 0022): the hit lines (#247), the status key and the
/// enemy HP key (#631, #767), and the situation banners (#692). Its controls take each input read after the menus'.
/// </summary>
public sealed class BattleModule : IFeatureModule
{
    public IEnumerable<Enum> Settings => Enum.GetValues<BattleFeature>().Select(feature => (Enum)feature);

    public IEnumerable<SliderLimit> Limits => [BattleStatus.HoldLimit];

    public void Register(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = context.Settings;
        var reader = context.Reader;
        var log = context.Log;
        // Each hit is spoken through the reader, queued, with a summary after each action (#247, #631). No safe read
        // of a party member's name is known yet, so each is named by its slot.
        var battleSpeech = settings.Speech(BattleFeature.BattleHits, reader);
        // The enemy list from the battle's start (#750) and numbers-only hits with a placed sound (#855), each testing.
        var hitLines = new HitLines(
            battleSpeech, TimeProvider.System, _ => null,
            outcomesOn: () => settings.Current.IsOn(BattleFeature.BattleOutcomes))
        {
            ListOn = () => settings.Current.IsOn(BattleFeature.BattleEnemyList),
            NumbersOnly = () => settings.Current.IsOn(BattleFeature.BattleHitNumbers),
            SlotCue = new PlacedHitCue(context.Cues).Play,
        };
        var units = context.Find<IBattleUnits>();
        // The enemy tables from the player's own archive (#164, #166), read off the game's thread on the first input
        // read with affinities or reward EXP on; each enemy's affinities found in play, tracked this game session.
        var enemyTables = new EnemyTableSource(context.OpenArchive, line => log?.Requested("EnemyTables", line));
        var tableLoad = new EnemyTableLoad(enemyTables);
        context.AddControls(settings.Input(BattleFeature.BattleAffinities, tableLoad));
        context.AddControls(settings.Input(BattleFeature.BattleRewardExp, tableLoad));
        var serials = context.Serials;
        var affinities = new EnemyAffinities(
            () => enemyTables.Tables, () => hitLines.Battle, () => Environment.TickCount64,
            serial => serials.InFile(serial, MessageSerials.EnemyNames) ? serials.FindBySerial(serial)?.Label : null)
        {
            On = () => settings.Current.IsOn(BattleFeature.BattleAffinities),
            FoundOnly = () => settings.Current.IsOn(BattleFeature.BattleAffinitiesFoundOnly),
            Units = units,
            Log = log is null ? null : text => log.Requested("EnemyAffinities", text),
        };
        // The hit lines hear each hit first, so a new battle's number is in place when the affinities hear it.
        context.Provide<IBattleHitObserver>(new BattleHitFanOut(hitLines, affinities));
        // The enemies stunned as a battle starts (#772, #626), testing, from the same list.
        if (units is not null)
        {
            var startStun = new BattleStartStun(
                units, settings.Speech(BattleFeature.BattleStartStun, reader), () => Environment.TickCount64);
            context.Layouts.Observe(startStun);
            context.Provide<IBattleEnemyObserver>(new BattleEnemyFanOut(hitLines, startStun));
        }
        else
        {
            context.Provide<IBattleEnemyObserver>(hitLines);
        }
        // The skill tables come from the player's own archive, read-only, off the game's thread, on the first input
        // read with the costs or the move names on, so a game the hooks didn't load into never reads it.
        var costSource = new SkillCostSource(
            context.OpenArchive, line => log?.Requested("SkillCosts", line));
        var costLoad = new SkillCostLoad(costSource);
        context.AddControls(settings.Input(BattleFeature.BattleMoveNames, costLoad));
        // The move banner's skill by name (#954), testing, queued behind the hit lines.
        var moveNames = new MoveNames(
            costSource.LabelHashOf, serials, context.Cleaner, settings.Speech(BattleFeature.BattleMoveNames, reader));
        // The in-turn situation banner, queued behind the hit lines (#692), behind `speech.battleBanners` (#661).
        // The banner's subject (#739), testing: a party member's or enemy's name serial, or a name's text.
        context.Provide(new BattleBanners(
            context.FilePositions, settings.Speech(BattleFeature.BattleBanners, reader), context.Cleaner,
            () => Environment.TickCount64)
        {
            SerialName = serial =>
                serials.InFile(serial, MessageSerials.CharacterNames) || serials.InFile(serial, MessageSerials.EnemyNames)
                    ? serials.TextOf(serial, context.Cleaner)
                    : null,
            NamesOn = () => settings.Current.IsOn(BattleFeature.BattleBannerNames),
            Moved = skill =>
            {
                affinities.Move(skill);
                moveNames.Moved(skill);
            },
            Subject = affinities.Named,
        });
        // The turn line (#252), polled on each input read from the game data's turn icons, and the ambushed start
        // (#626) and the battle's end (#166) from the layouts' animations, each its own testing setting.
        if (context.Find<IBattleTurnIcons>() is { } turnIcons)
        {
            context.AddControls(settings.Input(
                BattleFeature.BattleTurnLine,
                new TurnLine(turnIcons, context.BattleScene, settings.Speech(BattleFeature.BattleTurnLine, reader),
                    TimeProvider.System)
                {
                    // A Debug build logs each icon change read, for #254's unsaid mid-turn gain.
                    Log = log is null ? null : text => log.Requested("BattleTurnIcons", text),
                }));
        }
        context.Layouts.Observe(new BattleStartLine(settings.Speech(BattleFeature.BattleStart, reader)));
        context.Layouts.Observe(new BattleEndLine(settings.Speech(BattleFeature.BattleEnd, reader)));
        // The money and MAG a won battle gave (#166), testing, from the purse at its start and end.
        if (context.Find<IBattlePurse>() is { } purse)
        {
            var rewards = new BattleRewards(
                purse, settings.Speech(BattleFeature.BattleRewards, reader), TimeProvider.System)
            {
                TableRewards = () => affinities.Rewards(hitLines.AllEnemies()),
                ExpOn = () => settings.Current.IsOn(BattleFeature.BattleRewardExp),
                Log = log is null ? null : text => log.Requested("BattleRewards", text),
            };
            context.Layouts.Observe(rewards);
            context.AddControls(settings.Input(BattleFeature.BattleRewards, rewards));
        }
        // Leaving the battle scene ends the battle, so the next one numbers its enemies afresh (#750).
        context.AddControls(settings.Input(
            BattleFeature.BattleNumberReset, new BattleEnds(hitLines, context.BattleScene, TimeProvider.System)));
        var battleBindings = settings.Live(BattleBindings.From);
        // The status key acts only in a battle (#631); a Debug build logs the scene each press read.
        var status = new BattleStatus(
            context.BattleParty, hitLines, context.BattleScene, battleSpeech, battleBindings, TimeProvider.System)
        {
            // The L3 hold time (#941), read on each pad read so a changed file applies at once.
            HoldTime = () => BattleStatus.HoldLimit.From(settings.Current),
        };
        context.AddControls(settings.Input(BattleFeature.BattleHits, status));
        // L3 is held back until it's let go, then tapped to the game (Retry) unless a hold spoke (#925).
        context.AddShoulderHold(() => settings.Current.IsOn(BattleFeature.BattleHits) ? status.HeldButtons() : 0);
        // The HP and MP key (#692), testing, on top of the hits.
        context.AddControls(settings.Input(
            BattleFeature.BattleHits,
            settings.Input(
                BattleFeature.BattleHpMp,
                new BattleHpMp(context.BattleParty, hitLines, context.BattleScene, battleSpeech, battleBindings))));
        // The enemy HP keys (#767): K, J and L, and LB with the D-pad, each its own testing feature on top of the hits,
        // so the game keeps F and R3; the menu controls take a key first while a battle list is shown. A Debug build
        // logs each all-enemies press's enemy records for the target read.
        // The target cursor's enemy (#253), testing, which the keys start from.
        // The enemy it moves to is said (#253), testing, from the same reports: no other write to the game.
        var targetLine = new TargetLine(
            hitLines, settings.Speech(BattleFeature.BattleTargetSpeech, battleSpeech), TimeProvider.System)
        {
            On = () => settings.Current.IsOn(BattleFeature.BattleHits) &&
                settings.Current.IsOn(BattleFeature.BattleEnemyList) &&
                settings.Current.IsOn(BattleFeature.BattleTargetSpeech),
            SayMaxHp = () => settings.Current.IsOn(BattleFeature.BattleEnemyMaxHp),
            SayPositions = () => settings.Current.IsOn(SpokenDetail.Positions),
            Units = units,
            Affinities = affinities.Says,
        };
        var targeted = new TargetedEnemy(hitLines) { Then = targetLine };
        context.Provide<IBattleTargetObserver>(targeted);
        var enemyHp = new BattleEnemyHp(
            battleBindings, hitLines, context.BattleScene, battleSpeech,
            log is null ? null : text => log.Requested("BattleDetails", text))
        {
            SayMaxHp = () => settings.Current.IsOn(BattleFeature.BattleEnemyMaxHp),
            ListOn = () => settings.Current.IsOn(BattleFeature.BattleEnemyList),
            Target = targeted,
            TargetOn = () => settings.Current.IsOn(BattleFeature.BattleEnemyTarget),
            SayPositions = () => settings.Current.IsOn(SpokenDetail.Positions),
            Units = units,
            Affinities = affinities.Says,
        };
        context.AddControls(settings.Input(
            BattleFeature.BattleHits, settings.Input(BattleFeature.BattleEnemyKeys, enemyHp.Keyboard)));
        context.AddControls(settings.Input(
            BattleFeature.BattleHits, settings.Input(BattleFeature.BattleEnemyPad, enemyHp.Pad)));
        RegisterListsAndParty(context, hitLines, costSource, costLoad);
        context.AddKeys(BattleKeys.All);
        // The chords' LB is held back until it's let go, then tapped to the game if no chord came (#811).
        context.AddShoulderHold(() =>
            settings.Current.IsOn(BattleFeature.BattleHits) && settings.Current.IsOn(BattleFeature.BattleEnemyPad)
                ? enemyHp.HeldShoulders()
                : 0);
    }

    // Each hit to the hit lines, then to the affinities (#164).
    private sealed class BattleHitFanOut(IBattleHitObserver first, IBattleHitObserver then) : IBattleHitObserver
    {
        public void Observe(BattleHit hit)
        {
            first.Observe(hit);
            then.Observe(hit);
        }
    }

    // Each enemy list to the hit lines, then to the start stun (#772).
    private sealed class BattleEnemyFanOut(IBattleEnemyObserver first, IBattleEnemyObserver then) : IBattleEnemyObserver
    {
        public void Enemies(long package, IReadOnlyList<long> records)
        {
            first.Enemies(package, records);
            then.Enemies(package, records);
        }
    }

    // The skill lists' costs (#692 (b)) and the Synthesis list (#260), which the menus' battle lists speak; the Party
    // screen (#258) and the Formation watch (#259). Each its own testing setting.
    private static void RegisterListsAndParty(
        FeatureContext context, HitLines hitLines, SkillCostSource costSource, SkillCostLoad costLoad)
    {
        var settings = context.Settings;
        var log = context.Log;
        var serials = context.Serials;
        var costs = new SkillCostLines(
            serial => serials.FindBySerial(serial)?.Label, costSource.CostOf,
            () => settings.Current.IsOn(BattleFeature.BattleSkillCosts))
        {
            Log = log is null ? null : text => log.Requested("SkillCost", text),
        };
        context.AddControls(settings.Input(BattleFeature.BattleSkillCosts, costLoad));
        context.Find<BattleListAdditions>()?.Add(
            costs.CostOf, () => settings.Current.IsOn(BattleFeature.BattleSynthesis));
        var partyScreen = new PartyScreenFocus(
            serials, context.Cleaner, context.BattleParty,
            settings.Speech(BattleFeature.BattlePartyScreen, context.Reader), () => Environment.TickCount64)
        {
            ProtagonistName = () => context.Names.Typed(TypedName.Hero),
            Log = log is null ? null : text => log.Requested("BattlePartyScreen", text),
        };
        context.Screens.Observe(partyScreen);
        context.Events.ObserveSerials(partyScreen);
        // The reserve list it swaps from (#922), with the same setting.
        var reserveScreen = new ReserveScreenFocus(
            serials, context.Cleaner, settings.Speech(BattleFeature.BattlePartyScreen, context.Reader),
            () => Environment.TickCount64);
        context.Screens.Observe(reserveScreen);
        context.Events.ObserveSerials(reserveScreen);
        context.AddControls(settings.Input(
            BattleFeature.BattleFormation,
            new FormationWatch(
                context.BattleParty, context.BattleScene,
                settings.Speech(BattleFeature.BattleFormation, context.Reader), TimeProvider.System,
                (slot, unit, rest) => partyScreen.NameOf(unit) is { } name
                    ? $"{name}, {rest}"
                    : hitLines.MemberSays(slot, unit, rest))
            {
                Log = log is null ? null : text => log.Requested("BattleFormation", text),
            }));
    }
}
