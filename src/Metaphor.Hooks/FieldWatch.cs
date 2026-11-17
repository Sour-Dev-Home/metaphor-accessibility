using Metaphor.Core;
using Metaphor.Core.Field;

namespace Metaphor.Hooks;

/// <summary>Reads, without a hook of its own, the field's state and the units around the player (#177, #178, #180).
/// Three game functions in <c>.ecode</c> load the field's globals rip-relatively; their signatures find them, and each
/// global's address is read from that instruction. Everything after is plain reads through those globals. The offsets
/// and what each value is thought to mean are in docs/raw-sources/field-position-2026-11-16.md,
/// docs/raw-sources/field-actors-2026-11-16.md and docs/raw-sources/field-state-and-map-2026-11-16.md, all
/// <c>[NEEDS VERIFICATION]</c> until a session logs them. Numbers and class names only, no game text; no game function is
/// called, so a layout name or a map name is never read.</summary>
/// <remarks>Carried by <c>PadReadHook</c> (<c>InputHooks.cs</c>), Debug only, when <see cref="OptInFile"/> is in the
/// mod's folder (#250).</remarks>
internal sealed class FieldWatch
{
    /// <summary>The file in the mod's folder that turns the watch on.</summary>
    public const string OptInFile = "opt-in-field.txt";

    /// <summary><c>FldCameraIsTypeFollow</c>: <c>mov rax,[rip+disp32]</c> (7 bytes) loads <c>fldWorld</c>.</summary>
    public const string WorldReader = "FldCameraIsTypeFollow";

    /// <summary>Inside the field's start-up: <c>mov [rip+disp32],rbx</c> (7 bytes, at <c>+0x2F</c>) stores the
    /// <c>fldScene_Root</c>.</summary>
    public const string SceneRootWriter = "FieldSceneRootCreate";

    /// <summary>The function <c>FldGetCurrentMapID</c> jumps to: <c>mov rcx,[rip+disp32]</c> (7 bytes) loads
    /// <c>fldDatabase</c>.</summary>
    public const string DatabaseReader = "FldCurrentMapIdBody";

    // How often the state and the unit lists are read; a pad read runs every frame.
    private const long StateMilliseconds = 250;
    private const long NearbyMilliseconds = 1000;

    // Most pointers read from one list, and in all; a list longer than this is counted but only its first units placed.
    private const int MaxUnitsPerList = 128;
    private const int MaxUnits = 512;

    private const int WorldCamera = 0x10;
    private const int WorldUnits = 0x20;
    private const int CameraObject = 0x00;
    private const int CameraType = 0x08;
    private const int CameraVectorA = 0x60;
    private const int CameraVectorB = 0x70;
    private const int RootMachine = 0x10;
    private const int MachineScene = 0x10;
    private const int DatabaseMapId = 0x48;
    private const int UnitPosition = 0x80;
    private const int UnitRotation = 0x90;
    private const int UnitId = 0x9F8;
    private const int PlayerActionFlags = 0x34C8;
    private const int PlayerInputLocks = 0x34CC;
    private const int DoorList = 5;
    private const int UnitModel = 0x10;
    private const int ModelNode = 0x48;
    private const int ModelNodeFallback = 0x20;
    private const int NodePosition = 0x50;

    private readonly MessageLog log;
    private readonly CallerAddress module = new();
    private readonly InputStateChanges changes = new();
    private readonly IntervalGate stateGate = new(StateMilliseconds, () => Environment.TickCount64);
    private readonly IntervalGate nearbyGate = new(NearbyMilliseconds, () => Environment.TickCount64);

    private nint worldGlobal;
    private nint rootGlobal;
    private nint databaseGlobal;

    public FieldWatch(MessageLog log) => this.log = log;

    public static IReadOnlyList<string> Signatures => [WorldReader, SceneRootWriter, DatabaseReader];

    /// <summary>Finds the three globals from the functions the loader located. Leaves a global unknown (0) when its
    /// instruction can't be read.</summary>
    public void Resolve(IReadOnlyDictionary<string, nint> functions)
    {
        ArgumentNullException.ThrowIfNull(functions);
        worldGlobal = RipRelativeTarget(functions[WorldReader], operandAt: 3);
        rootGlobal = RipRelativeTarget(functions[SceneRootWriter] + 0x2F, operandAt: 3);
        databaseGlobal = RipRelativeTarget(functions[DatabaseReader], operandAt: 3);
    }

    /// <summary>Called on every pad read: reads and logs the state and the nearby units when each is due and changed.
    /// </summary>
    public void Tick()
    {
        if (!stateGate.Due())
        {
            return;
        }
        var world = Pointer(worldGlobal);
        var player = world is { } w && Pointer(w + WorldUnits) is { } units ? Pointer(units) : null;
        var state = ReadState(world, player);
        if (changes.Changed("field", state.ToString()))
        {
            log.FieldStateRead(state);
        }
        if (nearbyGate.Due() && state.Position is { } position && world is { } wd && Pointer(wd + WorldUnits) is { } manager)
        {
            var nearby = ReadNearby(manager, position);
            if (changes.Changed("field-near", nearby))
            {
                log.FieldNearbyRead(nearby);
            }
        }
    }

    private FieldState ReadState(nint? world, nint? player)
    {
        var scene = Pointer(rootGlobal) is { } root && Pointer(root + RootMachine) is { } machine
            ? Pointer(machine + MachineScene)
            : null;
        var mapId = Pointer(databaseGlobal) is { } database ? ModuleMemoryReader.ReadInt32(database + DatabaseMapId) : null;
        var camera = world is { } w ? Pointer(w + WorldCamera) : null;
        var cameraObject = camera is { } c ? Pointer(c + CameraObject) : null;
        FieldVector? position = null;
        double? facing = null;
        if (player is { } p && Floats(p + UnitPosition, 3) is { } at)
        {
            position = new FieldVector(at[0], at[1], at[2]);
            facing = Floats(p + UnitRotation, 4) is { } q ? FieldGeometry.YawDegrees(q[0], q[1], q[2], q[3]) : null;
        }
        return new FieldState(
            scene is { } s ? ClassNameOf(s) : null,
            mapId,
            player is { } pc ? ClassNameOf(pc) : null,
            position,
            facing,
            player is { } pl ? Unsigned(ModuleMemoryReader.ReadInt32(pl + PlayerInputLocks)) : null,
            player is { } pa ? Unsigned(ModuleMemoryReader.ReadInt32(pa + PlayerActionFlags)) : null,
            camera is { } ct ? ModuleMemoryReader.ReadInt32(ct + CameraType) : null,
            cameraObject is { } a ? Vector(a + CameraVectorA) : null,
            cameraObject is { } b ? Vector(b + CameraVectorB) : null);
    }

    private string ReadNearby(nint manager, FieldVector player)
    {
        var lists = new Dictionary<int, (int Count, string? FirstClass)>();
        var units = new List<FieldUnit>();
        for (var index = 0; index < FieldNearby.ListNames.Count; index++)
        {
            var offset = FieldNearby.BeginOffset(index);
            if (Pointer(manager + offset) is not { } begin || Pointer(manager + offset + 8) is not { } end ||
                end < begin || (end - begin) % 8 != 0)
            {
                continue;
            }
            var count = (int)Math.Min((end - begin) / 8, int.MaxValue);
            string? firstClass = null;
            for (var i = 0; i < Math.Min(count, MaxUnitsPerList) && units.Count < MaxUnits; i++)
            {
                if (Pointer(begin + (i * 8)) is not { } unit || unit == 0 || Vector(unit + UnitPosition) is not { } at)
                {
                    continue;
                }
                firstClass ??= ClassNameOf(unit);
                var id = ModuleMemoryReader.ReadInt32(unit + UnitId) ?? -1;
                units.Add(new FieldUnit(index, id, at, index == DoorList ? DoorNode(unit) : null));
            }
            lists[index] = (count, firstClass);
        }
        return FieldNearby.Describe(player, lists, units);
    }

    /// <summary>A door's position as <c>FldDoorGetUidByClosestOnPlayer</c> takes it: the node at <c>+0x48</c> (else
    /// <c>+0x20</c>) of the component at <c>+0x10</c>, its floats at <c>+0x50</c>.</summary>
    private static FieldVector? DoorNode(nint door)
    {
        if (Pointer(door + UnitModel) is not { } model)
        {
            return null;
        }
        var node = Pointer(model + ModelNode) is { } first and not 0 ? first : Pointer(model + ModelNodeFallback);
        return node is { } n and not 0 ? Vector(n + NodePosition) : null;
    }

    private static uint? Unsigned(int? value) => value is { } v ? unchecked((uint)v) : null;

    private static nint? Pointer(nint address) =>
        address == 0 ? null : ModuleMemoryReader.ReadPointer(address) is { } value and not 0 ? value : null;

    private static FieldVector? Vector(nint address) =>
        Floats(address, 3) is { } v ? new FieldVector(v[0], v[1], v[2]) : null;

    private static float[]? Floats(nint address, int count)
    {
        var bytes = new byte[count * sizeof(float)];
        if (!ModuleMemoryReader.ReadBytes(address, bytes))
        {
            return null;
        }
        var values = new float[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = BitConverter.ToSingle(bytes, i * sizeof(float));
        }
        return values.All(float.IsFinite) ? values : null;
    }

    private string? ClassNameOf(nint instance) => RttiName.Of(
        instance,
        module.ModuleBase,
        module.ModuleSize,
        address => ModuleMemoryReader.ReadPointer((nint)address),
        address => ModuleMemoryReader.ReadInt32((nint)address),
        address => ModuleMemoryReader.ReadAnsiString((nint)address, 256));

    /// <summary>The address a 7-byte rip-relative instruction names: its end plus its signed 32-bit displacement.</summary>
    private static nint RipRelativeTarget(nint instruction, int operandAt) =>
        ModuleMemoryReader.ReadInt32(instruction + operandAt) is { } displacement ? instruction + 7 + displacement : 0;
}
