using Metaphor.Core.Battle;
using Metaphor.Core.Features;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>The battle project's wiring and settings (#784, ADR 0022): the hit lines (#247), the status key and the
/// enemy HP key (#631, #767), and the situation banners (#692). Its controls take each input read after the menus'.
/// </summary>
public sealed class BattleModule : IFeatureModule
{
    public IEnumerable<Enum> Settings => Enum.GetValues<BattleFeature>().Select(feature => (Enum)feature);

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
        context.Provide<IBattleHitObserver>(hitLines);
        context.Provide<IBattleEnemyObserver>(hitLines);
        var units = context.Find<IBattleUnits>();
        // The in-turn situation banner, queued behind the hit lines (#692), behind `speech.battleBanners` (#661).
        context.Provide(new BattleBanners(
            context.FilePositions, settings.Speech(BattleFeature.BattleBanners, reader), context.Cleaner,
            () => Environment.TickCount64));
        // The turn line (#252), polled on each input read from the game data's turn icons, and the ambushed start
        // (#626) and the battle's end (#166) from the layouts' animations, each its own testing setting.
        if (context.Find<IBattleTurnIcons>() is { } turnIcons)
        {
            context.AddControls(settings.Input(
                BattleFeature.BattleTurnLine,
                new TurnLine(turnIcons, context.BattleScene, settings.Speech(BattleFeature.BattleTurnLine, reader),
                    TimeProvider.System)));
        }
        context.Layouts.Observe(new BattleStartLine(settings.Speech(BattleFeature.BattleStart, reader)));
        context.Layouts.Observe(new BattleEndLine(settings.Speech(BattleFeature.BattleEnd, reader)));
        // Leaving the battle scene ends the battle, so the next one numbers its enemies afresh (#750).
        context.AddControls(settings.Input(
            BattleFeature.BattleNumberReset, new BattleEnds(hitLines, context.BattleScene, TimeProvider.System)));
        var battleBindings = settings.Live(BattleBindings.From);
        // The status key acts only in a battle (#631); a Debug build logs the scene each press read.
        context.AddControls(settings.Input(
            BattleFeature.BattleHits,
            new BattleStatus(context.BattleParty, hitLines, context.BattleScene, battleSpeech, battleBindings)));
        // The HP and MP key (#692), testing, on top of the hits.
        context.AddControls(settings.Input(
            BattleFeature.BattleHits,
            settings.Input(
                BattleFeature.BattleHpMp,
                new BattleHpMp(context.BattleParty, hitLines, context.BattleScene, battleSpeech, battleBindings))));
        // The enemy HP keys (#767): K, J and L, and LB with the D-pad, each its own testing feature on top of the hits,
        // so the game keeps F and R3; the menu controls take a key first while a battle list is shown. A Debug build
        // logs each all-enemies press's enemy records for the target read.
        var enemyHp = new BattleEnemyHp(
            battleBindings, hitLines, context.BattleScene, battleSpeech,
            log is null ? null : text => log.Requested("BattleDetails", text))
        {
            SayMaxHp = () => settings.Current.IsOn(BattleFeature.BattleEnemyMaxHp),
            ListOn = () => settings.Current.IsOn(BattleFeature.BattleEnemyList),
            SayPositions = () => settings.Current.IsOn(SpokenDetail.Positions),
            Units = units,
        };
        context.AddControls(settings.Input(
            BattleFeature.BattleHits, settings.Input(BattleFeature.BattleEnemyKeys, enemyHp.Keyboard)));
        context.AddControls(settings.Input(
            BattleFeature.BattleHits, settings.Input(BattleFeature.BattleEnemyPad, enemyHp.Pad)));
        // The chords' LB is held back until it's let go, then tapped to the game if no chord came (#811).
        context.AddShoulderHold(() =>
            settings.Current.IsOn(BattleFeature.BattleHits) && settings.Current.IsOn(BattleFeature.BattleEnemyPad)
                ? enemyHp.HeldShoulders()
                : 0);
    }
}
