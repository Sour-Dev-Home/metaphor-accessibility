using System.Globalization;
using Metaphor.Core;
using Metaphor.Core.Battle;

namespace Metaphor.Hooks;

// Issue #162: a development-only, opt-in listener for the battle spike. One hook on plain code in .ecode, observed
// before the original runs (rule 14); everything else is read, never written or called. The functions, field offsets and
// class names are our reading of the exe on one build (docs/raw-sources/battle-state-leads-2026-11-17.md); what each
// changing field means is what capture evening 2 (#250) is for. Addresses, offsets, numbers and class names only.

/// <summary>The battle package's per-frame update, <c>void (Package* this, float seconds)</c>, called once a frame
/// while a command battle runs. Each call hands the frame to <see cref="BattleWatch"/>, which reads the package and the
/// objects it points at and logs what changed.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true, OptInFile = BattleWatch.OptInFile)]
internal sealed class BattlePackageUpdateListener : IGameHook, IObserves2WithDouble
{
    private readonly BattleWatch? watch;

    public BattlePackageUpdateListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        watch = services.Log is { } log ? new BattleWatch(log) : null;
    }

    public string SignatureName => "BtlPackageUpdate";

    /// <summary>The battle info getters (for the global battle info) and the party unit getter (for the party's units),
    /// located and read from, never hooked or called.</summary>
    public IReadOnlyList<string> Calls => watch is null ? [] : BattleWatch.Signatures;

    public void UseCalls(IReadOnlyDictionary<string, nint> calls) => watch?.Resolve(calls);

    public void Observe(nint package, double seconds) => watch?.Frame(package);
}

/// <summary>Reads the battle package each frame and logs, to the local message log: a new package (a battle starts), each
/// change of phase by class name, the 32-bit fields that change in the package, its turn order, formation and GUI
/// objects and the global battle info, which of their fields point at battle objects, each action that enters or leaves
/// the action list, and, at each phase change, the changes in every battle object and party unit. A capture session ties
/// these numbers to what was on screen (#250).</summary>
internal sealed class BattleWatch
{
    /// <summary>The file in the mod's folder that turns the listener on.</summary>
    public const string OptInFile = "opt-in-battle.txt";

    /// <summary>Three one-line getters in a row whose first reads the elapsed player turns, an <c>int</c> at
    /// <c>+0x14</c> of the global battle info, through <c>mov eax,[rip+disp32]</c> (6 bytes).</summary>
    public const string InfoGetters = "BtlInfoGetters";

    /// <summary>The party unit getter: <c>mov rax,[rip+disp32]</c> at <c>+0xA</c> (7 bytes) loads the object that holds
    /// the party's units from <c>+0x228</c>, <c>0x16A8</c> bytes each.</summary>
    public const string PartyUnits = "GetPlayerUnit";

    // Package fields (smart pointers keep their object 0x18 past their own start; lists their first node 8 past theirs).
    private const int PhaseOffset = 0x60;
    private const int OrderOffset = 0x80;
    private const int FormationOffset = 0xA0;
    private const int ActionListOffset = 0x110;
    private const int ObjectListOffset = 0x1F0;
    private const int GuiOffset = 0x358;
    private const int CurrentActionOffset = 0x708;

    // A list node: the next node at +0, the object at +0x28.
    private const int NodeObject = 0x28;
    private const int MaxNodes = 64;

    private const int InfoFieldOffset = 0x14;
    private const int InfoBytes = 0x20;
    private const int PartyOffset = 0x228;
    private const int PartyUnitBytes = 0x16A8;
    // How many units the party array holds is unknown; the growth screen showed seven members.
    private const int PartyUnitCount = 8;

    private const int PackageBytes = 0x800;
    private const int OrderBytes = 0x200;
    private const int FormationBytes = 0x200;
    private const int GuiBytes = 0x400;
    private const int ObjectBytes = 0x400;
    private const int ActionBytes = 0x200;
    private const int MaxValues = 96;

    private readonly MessageLog log;
    private readonly CallerAddress module = new();
    private readonly FieldWatch fields = new();
    private readonly Dictionary<string, string> lastRefs = [];
    private readonly HashSet<long> objects = [];
    private readonly HashSet<long> actions = [];
    private nint infoGlobal;
    private nint partyGlobal;
    private nint package;
    private nint phaseVtable;

    public BattleWatch(MessageLog log) => this.log = log;

    public static IReadOnlyList<string> Signatures => [InfoGetters, PartyUnits];

    /// <summary>Finds both globals from the functions the loader located. Leaves a global unknown (0) when its
    /// instruction can't be read.</summary>
    public void Resolve(IReadOnlyDictionary<string, nint> functions)
    {
        ArgumentNullException.ThrowIfNull(functions);
        var turns = RipRelativeTarget(functions[InfoGetters], operandAt: 2, instructionLength: 6);
        infoGlobal = turns == 0 ? 0 : turns - InfoFieldOffset;
        partyGlobal = RipRelativeTarget(functions[PartyUnits] + 0xA, operandAt: 3, instructionLength: 7);
    }

    /// <summary>One frame of the battle whose package is <paramref name="current"/>.</summary>
    public void Frame(nint current)
    {
        if (current == 0)
        {
            return;
        }
        if (current != package)
        {
            package = current;
            phaseVtable = 0;
            fields.Clear();
            lastRefs.Clear();
            objects.Clear();
            actions.Clear();
            log.Battle("start", Format($"pkg=0x{current:x}"));
        }
        if (ModuleMemoryReader.ReadPointer(current + PhaseOffset) is { } phase &&
            ModuleMemoryReader.ReadPointer(phase) is { } vtable && vtable != phaseVtable)
        {
            phaseVtable = vtable;
            log.Battle("phase", Format($"pkg=0x{current:x} phase=0x{phase:x} class={ClassOf(phase) ?? "?"}"));
            Objects(current);
            Party();
        }
        Actions(current);
        var targets = new HashSet<long>(objects.Concat(actions));
        Watch("package", current, PackageBytes, targets);
        WatchThrough("order", current + OrderOffset, OrderBytes, targets);
        WatchThrough("formation", current + FormationOffset, FormationBytes, targets);
        WatchThrough("gui", current + GuiOffset, GuiBytes, targets);
        if (infoGlobal != 0)
        {
            Watch("info", infoGlobal, InfoBytes, targets: null);
        }
    }

    /// <summary>Each battle object in the package's object list: new ones with their non-zero fields, known ones with
    /// what changed since the last phase, and the ones that left.</summary>
    private void Objects(nint current)
    {
        var now = Walk(current + ObjectListOffset);
        foreach (var gone in objects.Where(o => !now.Contains(o)).ToList())
        {
            objects.Remove(gone);
            fields.Forget(Key("obj", gone));
            log.Battle("object-gone", Format($"obj=0x{gone:x}"));
        }
        var buffer = new byte[ObjectBytes];
        foreach (var item in now)
        {
            if (!ModuleMemoryReader.ReadBytes((nint)item, buffer))
            {
                continue;
            }
            var diff = fields.Compare(Key("obj", item), buffer);
            if (objects.Add(item))
            {
                log.Battle("object", Format(
                    $"obj=0x{item:x} class={ClassOf((nint)item) ?? "?"} values={SmallFields.Describe(SmallFields.NonZero(buffer, MaxValues))}"));
            }
            else if (diff.Changes.Count > 0)
            {
                log.Battle("object-fields", Format($"obj=0x{item:x} changes={SmallFields.Describe(diff.Changes)}"));
            }
        }
    }

    /// <summary>The actions in the package's action list and its current action: new ones with their class and non-zero
    /// fields, and the ones that left with what changed in them meanwhile.</summary>
    private void Actions(nint current)
    {
        var now = Walk(current + ActionListOffset);
        if (ModuleMemoryReader.ReadPointer(current + CurrentActionOffset) is { } acting and not 0)
        {
            now.Add(acting);
        }
        var buffer = new byte[ActionBytes];
        foreach (var gone in actions.Where(a => !now.Contains(a)).ToList())
        {
            actions.Remove(gone);
            var key = Key("act", gone);
            var changes = ModuleMemoryReader.ReadBytes((nint)gone, buffer) ? fields.Compare(key, buffer).Changes : [];
            fields.Forget(key);
            log.Battle("action-gone", Format($"act=0x{gone:x} changes={SmallFields.Describe(changes)}"));
        }
        foreach (var item in now.Where(actions.Add))
        {
            if (ModuleMemoryReader.ReadBytes((nint)item, buffer))
            {
                fields.Compare(Key("act", item), buffer);
                log.Battle("action", Format(
                    $"act=0x{item:x} class={ClassOf((nint)item) ?? "?"} values={SmallFields.Describe(SmallFields.NonZero(buffer, MaxValues))}"));
            }
        }
    }

    /// <summary>The party's units, by index: what changed since the last phase (HP, EXP, levels are expected here).
    /// </summary>
    private void Party()
    {
        if (partyGlobal == 0 || ModuleMemoryReader.ReadPointer(partyGlobal) is not { } holder || holder == 0)
        {
            return;
        }
        var buffer = new byte[PartyUnitBytes];
        for (var index = 0; index < PartyUnitCount; index++)
        {
            var unit = holder + PartyOffset + (index * PartyUnitBytes);
            if (!ModuleMemoryReader.ReadBytes(unit, buffer))
            {
                continue;
            }
            var diff = fields.Compare(Key("party", index), buffer);
            if (diff.Changes.Count > 0)
            {
                log.Battle("party", Format($"index={index} unit=0x{unit:x} changes={SmallFields.Describe(diff.Changes)}"));
            }
        }
    }

    private void WatchThrough(string name, nint pointerAt, int length, IReadOnlySet<long> targets)
    {
        if (ModuleMemoryReader.ReadPointer(pointerAt) is { } target and not 0)
        {
            Watch(name, target, length, targets);
        }
    }

    /// <summary>Logs what changed in the object at <paramref name="address"/> since the last frame, and which of its
    /// fields point at battle objects or actions when that set changes.</summary>
    private void Watch(string name, nint address, int length, IReadOnlySet<long>? targets)
    {
        var buffer = new byte[length];
        if (!ModuleMemoryReader.ReadBytes(address, buffer))
        {
            return;
        }
        var diff = fields.Compare(name, buffer);
        if (diff.First)
        {
            log.Battle("fields", Format($"of={name} at=0x{address:x} class={ClassOf(address) ?? "?"} values={SmallFields.Describe(SmallFields.NonZero(buffer, MaxValues))}"));
        }
        else if (diff.Changes.Count > 0 || diff.NewlyMuted.Count > 0)
        {
            log.Battle("fields", Format(
                $"of={name} changes={SmallFields.Describe(diff.Changes)} muted={(diff.NewlyMuted.Count == 0 ? "none" : string.Join(',', diff.NewlyMuted.Select(o => string.Create(CultureInfo.InvariantCulture, $"+0x{o:x}"))))}"));
        }
        if (targets is null)
        {
            return;
        }
        var refs = SmallFields.PointersTo(buffer, targets);
        var text = refs.Count == 0
            ? "none"
            : string.Join(',', refs.Select(r => string.Create(CultureInfo.InvariantCulture, $"+0x{r.Offset:x}->0x{r.Target:x}")));
        if (!lastRefs.TryGetValue(name, out var last) || last != text)
        {
            lastRefs[name] = text;
            log.Battle("refs", Format($"of={name} refs={text}"));
        }
    }

    /// <summary>The objects of the list whose first node is at <paramref name="head"/>, at most
    /// <see cref="MaxNodes"/>.</summary>
    private static HashSet<long> Walk(nint head)
    {
        var found = new HashSet<long>();
        var node = ModuleMemoryReader.ReadPointer(head) ?? 0;
        for (var count = 0; node != 0 && count < MaxNodes; count++)
        {
            if (ModuleMemoryReader.ReadPointer(node + NodeObject) is { } item and not 0)
            {
                found.Add(item);
            }
            node = ModuleMemoryReader.ReadPointer(node) ?? 0;
        }
        return found;
    }

    private string? ClassOf(nint instance) => RttiName.Of(
        instance,
        module.ModuleBase,
        module.ModuleSize,
        address => ModuleMemoryReader.ReadPointer((nint)address),
        address => ModuleMemoryReader.ReadInt32((nint)address),
        address => ModuleMemoryReader.ReadAnsiString((nint)address, 256));

    private static string Key(string kind, long id) => string.Create(CultureInfo.InvariantCulture, $"{kind}:{id:x}");

    private static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    /// <summary>The address a rip-relative operand names: the instruction's end plus its signed 32-bit displacement.</summary>
    private static nint RipRelativeTarget(nint instruction, int operandAt, int instructionLength) =>
        ModuleMemoryReader.ReadInt32(instruction + operandAt) is { } displacement
            ? instruction + instructionLength + displacement
            : 0;
}
