using System.Collections.Concurrent;
using System.Globalization;
using Metaphor.Core;
using Metaphor.Core.Battle;

namespace Metaphor.Hooks;

/// <summary>The turn icons as the game data block holds them (#252), read through <see cref="GameDataRead"/> only.
/// </summary>
internal sealed class BattleTurnIconsMemory(GameDataRead data) : IBattleTurnIcons
{
    public TurnIcons? Read() => data.TurnIcons();
}

/// <summary>The party as the game data block holds it (#247): each slot's unit and its HP at unit <c>+0x0C</c>, the
/// offset session 6 checked (docs/findings/battle-turn-icons-and-ui-components-static.md), and the max HP the last hit
/// on it showed. Reads through <see cref="GameDataRead"/> and <see cref="ModuleMemoryReader"/> only.</summary>
internal sealed class BattlePartyMemory(GameDataRead data) : IBattleParty, IBattleUnits
{
    // HP and MP, which the hit popup's getters 0x3e60f0 and 0x3e6c00 read from any target's record, enemies included
    // (docs/findings/battle-hit-result-hp-left-static.md).
    private const int HpAt = 0x0C;
    private const int MpAt = 0x10;

    private readonly ConcurrentDictionary<long, int> maxByUnit = new();

    public IReadOnlyList<PartyMember> Members()
    {
        var members = new List<PartyMember>();
        for (var slot = 0; slot < GameDataRead.SlotCount; slot++)
        {
            if (data.SlotUnit(slot) is { } unit && data.UnitAddress(unit) is { } address &&
                ModuleMemoryReader.ReadInt32(address + HpAt) is { } hp)
            {
                members.Add(new PartyMember(slot, unit, hp, maxByUnit.TryGetValue(address, out var max) ? max : null,
                    ModuleMemoryReader.ReadInt32(address + MpAt)));
            }
        }
        return members;
    }

    public int? Hp(long record) => record == 0 ? null : ModuleMemoryReader.ReadInt32((nint)record + HpAt);

    public int? Mp(long record) => record == 0 ? null : ModuleMemoryReader.ReadInt32((nint)record + MpAt);

    /// <summary>The party slot and unit number whose unit is at <paramref name="record"/>, or null for none.</summary>
    public (int Slot, int Unit)? Find(long record)
    {
        for (var slot = 0; slot < GameDataRead.SlotCount; slot++)
        {
            if (data.SlotUnit(slot) is { } unit && data.UnitAddress(unit) is { } address && address == record)
            {
                return (slot, unit);
            }
        }
        return null;
    }

    /// <summary>Keeps the max HP a hit showed for the unit at <paramref name="record"/>.</summary>
    public void Saw(long record, int max) => maxByUnit[record] = max;
}

/// <summary>Reports each target a resolved battle action hits (#247): the battle GUI's <c>BattleHitResult</c>,
/// <c>(request*, gui*, result*, target*, ushort index, int mode)</c> in <c>.ecode</c>, which runs once per target and
/// writes into its request the HP change and the HP the target has left before it sends the damage popup. Observed after
/// it returns, so those are written: the request's <c>+0x30</c> (HP change), <c>+0x38</c> (max HP, by how it clamps)
/// and <c>+0x40</c> (HP left, clamped to 0..max), and the result's kind at <c>+0x304</c>, a short. The target's unit
/// record is <c>*(*(target+0x18)+0x8)</c>; one at a party slot's unit is an ally, any other an enemy. Function,
/// arguments and offsets are our reading of the exe on build 548ddc95… (docs/findings/battle-hit-result-hp-left-static.md);
/// the HP change and HP left were heard right by the maintainer on 2026-11-21 (#631). A call whose numbers can't be read
/// is skipped. In a Debug build the raw values go to the message log for the by-ear check.</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class BattleHitResultHook : IGameHook, IObserves6WithResult
{
    private const int HpChangeAt = 0x30;
    private const int MpChangeAt = 0x34;
    private const int HpMaxAt = 0x38;
    private const int HpLeftAt = 0x40;
    private const int MpLeftAt = 0x44;
    private const int FlagsAt = 0x300;
    private const int KindAt = 0x304;
    private const int UnitHolderAt = 0x18;
    private const int RecordAt = 0x8;

    private readonly IBattleHitObserver hits;
    private readonly BattlePartyMemory party;
    private readonly MessageLog? log;

    public BattleHitResultHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        hits = services.BattleHits;
        party = services.BattleParty;
        log = services.Log;
    }

    public string SignatureName => "BattleHitResult";

    /// <summary>Six arguments, the last two on the stack, so the shape passes them all on to the original.</summary>
    public void Observe(nint request, nint gui, nint result, nint target, nint index, nint mode, nint unused)
    {
        if (ModuleMemoryReader.ReadInt32(request + HpChangeAt) is not { } hpChange ||
            ModuleMemoryReader.ReadInt32(request + HpLeftAt) is not { } hpLeft ||
            ModuleMemoryReader.ReadInt32(result + KindAt) is not { } kindAndSub)
        {
            return;
        }
        // The kind is the low short; the high short is the sub-kind at +0x306, which picks the popup's style.
        var kind = kindAndSub & 0xFFFF;
        var hpMax = ModuleMemoryReader.ReadInt32(request + HpMaxAt);
        long? record = ModuleMemoryReader.ReadPointer(target + UnitHolderAt) is { } holder && holder != 0
            ? ModuleMemoryReader.ReadPointer(holder + RecordAt)
            : null;
        var member = record is { } known ? party.Find(known) : null;
        var side = record is null or 0 ? BattleSide.Unknown : member is null ? BattleSide.Enemy : BattleSide.Ally;
        if (member is not null && record is { } seen && hpMax is { } max)
        {
            party.Saw(seen, max);
        }
        log?.Requested(SignatureName, string.Create(CultureInfo.InvariantCulture,
            $"target=0x{target:x} record=0x{record ?? 0:x} side={side} slot={member?.Slot} unit={member?.Unit} index={index & 0xFFFF} mode={(int)mode} kind=0x{kind:x} sub=0x{(kindAndSub >> 16) & 0xFFFF:x} flags=0x{ModuleMemoryReader.ReadInt32(result + FlagsAt) ?? 0:x} hp={hpChange} hpLeft={hpLeft} hpMax={hpMax} mp={ModuleMemoryReader.ReadInt32(request + MpChangeAt)} mpLeft={ModuleMemoryReader.ReadInt32(request + MpLeftAt)}"));
        hits.Observe(new BattleHit(record is { } identity and not 0 ? identity : target, hpChange, hpLeft, kind, side,
            member?.Slot, hpMax, member?.Unit));
    }
}

/// <summary>The battle package's per-frame update (#750), <c>void (Package* this, float seconds)</c> at <c>0x21e1c0</c> in
/// <c>.ecode</c>, a full function body with one caller, the battle task's update
/// (docs/findings/battle-enemy-list-order-and-update-hook-static.md, build 548ddc95…). Observed before the original
/// runs; each call hands the package to <see cref="BattleEnemyWatch"/>, which reads the enemy count, and walks the enemy
/// list only when it may have changed. Reads through <see cref="ModuleMemoryReader"/> only, never writes or calls. The
/// float travels in <c>xmm1</c>, which the shape passes on untouched. First built and reviewed on the closed
/// <c>247-battle</c> branch (commit <c>8be3b86</c>, #251). In a Debug build each list it hands on is logged, one line per
/// enemy, so a by-ear run can match list order against the screen.</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class BattlePackageUpdateHook : IGameHook, IObserves2WithDouble
{
    private readonly BattleEnemyWatch watch;

    public BattlePackageUpdateHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var enemies = services.Log is { } log
            ? new LoggedEnemies(services.BattleEnemies, services.BattleParty, log, SignatureName)
            : services.BattleEnemies;
        watch = new BattleEnemyWatch(
            enemies, () => Environment.TickCount64,
            address => ModuleMemoryReader.ReadPointer((nint)address),
            address => ModuleMemoryReader.ReadInt32((nint)address));
    }

    public string SignatureName => "BtlPackageUpdate";

    public void Observe(nint first, double second) => watch.Frame(first);

    // Debug builds: each enemy's list index, record and HP, then the list goes on to the feature.
    private sealed class LoggedEnemies(IBattleEnemyObserver next, IBattleUnits units, MessageLog log, string function)
        : IBattleEnemyObserver
    {
        public void Enemies(long package, IReadOnlyList<long> records)
        {
            log.Requested(function, string.Create(CultureInfo.InvariantCulture,
                $"package=0x{package:x} enemies={records.Count}"));
            for (var i = 0; i < records.Count; i++)
            {
                log.Requested(function, string.Create(CultureInfo.InvariantCulture,
                    $"enemy index={i} record=0x{records[i]:x} hp={units.Hp(records[i])} mp={units.Mp(records[i])}"));
            }
            next.Enemies(package, records);
        }
    }
}



/// <summary>What the two banner hooks share (#692): the banner's <c>{ u32 id, u8 kind }</c> pair, read before the
/// original runs, handed to the banners feature, and in a Debug build logged, so a
/// by-ear run pins which of them counts the icons.</summary>
internal static class BattleBannerListening
{
    private const int KindAt = 0x4;
    private const int PairLength = 0x8;

    public static void Shown(HookServices services, string function, nint pair, string? probe = null)
    {
        var bytes = new byte[PairLength];
        if (pair == 0 || !ModuleMemoryReader.ReadBytes(pair, bytes))
        {
            return;
        }
        var id = BitConverter.ToInt32(bytes, 0);
        var kind = bytes[KindAt];
        var said = services.BattleBanners?.Shown(kind, id);
        services.Log?.Requested(function, string.Create(CultureInfo.InvariantCulture,
            $"kind={kind} id={id} said={(said is null ? "no" : "yes")}{probe}"));
    }

    /// <summary>A Debug build's probe of a banner's name (#739): message variable slot 0, which the banner queue
    /// <c>0x326e70</c> fills just before it calls <c>BattleMessageShow</c>, read as the show is called, before the
    /// original runs (docs/findings/battle-banner-name-variable-static.md), as <see cref="BannerNameSlot"/> says it.
    /// Empty when there is no log.</summary>
    public static string NameSlot(HookServices services)
    {
        if (services.Log is null)
        {
            return "";
        }
        var slot = new byte[BannerNameSlot.Read];
        return BannerNameSlot.Describe(services.DialogueWindow.ReadMessageVariable(0, slot) ? slot : []);
    }
}

/// <summary>The battle banner's show, <c>UUIC_Battle_Message</c>'s <c>0xf187d0</c>: <c>void (component*, const
/// pair*, float seconds)</c> in <c>.ecode</c>, where the battle GUI, the command panel and battle code send each banner
/// (19 call sites). The pair is <c>{ u32 id, u8 kind }</c>; kind 2 is message-file table entry 9,
/// <c>jyokyo_help_Message.msg</c>, at position id. The float travels in <c>xmm2</c>, so the shape passes it on as a
/// double untouched. Our reading of the exe on build 548ddc95… (docs/findings/battle-move-banner-text-reference-static.md,
/// docs/findings/battle-situation-banner-text-static.md); not yet heard in play.</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class BattleMessageShowHook(HookServices services) : IGameHook, IObserves4WithDouble
{
    public string SignatureName => "BattleMessageShow";

    public void Observe(nint component, nint pair, double seconds, nint unused) =>
#if DEBUG
        BattleBannerListening.Shown(services, SignatureName, pair, BattleBannerListening.NameSlot(services));
#else
        BattleBannerListening.Shown(services, SignatureName, pair);
#endif
}

/// <summary>The banner's other show, <c>0xf185d0</c>: <c>void (component*, const request*)</c> in <c>.ecode</c>, which
/// only the battle-side request <c>0x324c50</c> calls. The request starts with the same <c>{ u32 id, u8 kind }</c> pair
/// and holds the seconds at <c>+0x10</c>. Our reading of the exe on build 548ddc95…
/// (docs/findings/battle-situation-banner-text-static.md); not yet heard in play.</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class BattleMessageRequestShowHook(HookServices services) : IGameHook, IObserves3
{
    public string SignatureName => "BattleMessageRequestShow";

    public void Observe(nint component, nint request, nint unused) =>
        BattleBannerListening.Shown(services, SignatureName, request);
}
