using Metaphor.Core;
using Metaphor.Core.Menus;

namespace Metaphor.Hooks;

// Issues #213 and #227: development-only, opt-in listeners to find which screen owns a menu cursor. The functions, their
// arguments and the field offsets are our reading of the exe on one build (docs/raw-sources/screen-class-link-2026-11-17.md,
// docs/raw-sources/archetype-detail-cursors-2026-11-17.md); the class names are the game's own, from its run-time type information. Every argument is observed pointer-sized (rule 9).

/// <summary>The shared cursor getter of the game's menu choices (the 12th virtual function of 279 classes, among them
/// <c>TitleMenuChoice</c> and <c>RootMenuChoice</c>): <c>shared_ptr&lt;CursorManagerBase&gt;* (Choice* this,
/// shared_ptr&lt;CursorManagerBase&gt;* out)</c>, which copies the choice's manager pointer from <c>+0x230</c> into
/// <c>out</c>. Each call records which choice handed out which manager, so the <see cref="ScreenClassProbe"/> can name the
/// screen of each cursor input. Opt-in: it is on a small function that may run every frame.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true, OptInFile = ScreenClassProbe.OptInFile)]
internal sealed class ChoiceCursorGetterListener : IGameHook, IObserves4WithResult
{
    private readonly ScreenClassProbe probe;

    public ChoiceCursorGetterListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        probe = services.ScreenClass;
        // The probe hears cursor inputs only when this hook is installed, so without the opt-in file nothing runs.
        probe.Start(services.Cursors);
    }

    public string SignatureName => "ChoiceGetCursor";

    /// <summary>Observed after the getter returns, when <paramref name="output"/> holds the manager pointer.</summary>
    public void Observe(nint choice, nint output, nint third, nint fourth, nint result)
    {
        if (ModuleMemoryReader.ReadPointer(output) is { } manager && manager != 0)
        {
            probe.Owners.Saw(choice, manager);
        }
    }
}

/// <summary>Issue #227: the cursor setup of the tabbed archetype detail choices, the 11th virtual function (slot 10) of
/// <c>ClassDetailBase&lt;CampArchetypeDetailLayout&gt;</c>, <c>EquipClassDetailChoice</c>,
/// <c>StatusMenuClassDetailChoice</c>, <c>AcademiaClassDetailChoice</c> and <c>BTL_ClassChange</c>: <c>void (Choice*
/// this)</c>, which makes the choice's three tab cursor managers and stores them at <c>+0x1740</c>, <c>+0x1750</c> and
/// <c>+0x1760</c>, the fields its tabbed cursor getter (<c>0xd976f0</c>) hands out. That getter matches three times on
/// disk and can't be hooked; this function can (docs/raw-sources/archetype-detail-cursors-2026-11-17.md). After each call
/// it records the choice as the owner of all three managers, so the <see cref="ScreenClassProbe"/> names archetype
/// details on its cursor inputs. Turned on by the same file as <see cref="ChoiceCursorGetterListener"/>.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true, OptInFile = ScreenClassProbe.OptInFile)]
internal sealed class ClassDetailCursorSetupListener : IGameHook, IObserves1WithResult
{
    // Where the choice keeps its tab managers' shared pointers, 16 bytes apart (pointer, then control block).
    private static readonly int[] ManagerOffsets = [0x1740, 0x1750, 0x1760];

    private readonly ScreenClassProbe probe;

    public ClassDetailCursorSetupListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        probe = services.ScreenClass;
        probe.Start(services.Cursors);
    }

    public string SignatureName => "ClassDetailSetupCursors";

    /// <summary>Observed after the setup returns, when the three managers are in place.</summary>
    public void Observe(nint choice, nint result)
    {
        foreach (var offset in ManagerOffsets)
        {
            if (ModuleMemoryReader.ReadPointer(choice + offset) is { } manager && manager != 0)
            {
                probe.Owners.Saw(choice, manager);
            }
        }
    }
}

/// <summary>Logs the screen class candidates of each cursor input (#213): the manager's class, the class of the choice
/// that last handed the manager out (<see cref="ChoiceCursorGetterListener"/>), and the manager's fields that point at
/// objects with a class name, in case the manager keeps a back pointer. Names and numbers only, to the local message
/// log.</summary>
internal sealed class ScreenClassProbe : ICursorObserver
{
    /// <summary>The file in the mod's folder that turns the probe's hook on.</summary>
    public const string OptInFile = "opt-in-screen-class.txt";

    // How much of the manager the field scan reads. Its known fields end at the flags at +0xAC
    // (docs/raw-sources/ui-focus-candidates-2026-11-16.md); its size is unknown.
    private const int ManagerBytes = 0x100;

    private readonly MessageLog? log;
    private readonly CallerAddress module = new();
    private readonly Lock gate = new();
    private bool started;

    public ScreenClassProbe(MessageLog? log) => this.log = log;

    /// <summary>Which choice last handed out each manager.</summary>
    public CursorOwners Owners { get; } = new();

    /// <summary>Starts hearing <paramref name="cursors"/>' inputs; later calls do nothing.</summary>
    public void Start(CursorEvents cursors)
    {
        ArgumentNullException.ThrowIfNull(cursors);
        lock (gate)
        {
            if (started || log is null)
            {
                return;
            }
            started = true;
        }
        cursors.Observe(this);
    }

    public void Observe(CursorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (log is null)
        {
            return;
        }
        var fields = new byte[ManagerBytes];
        var named = ModuleMemoryReader.ReadBytes((nint)input.Manager, fields) ? NamedFields.Of(fields, ClassNameOf) : [];
        var owner = Owners.OwnerOf(input.Manager);
        log.ScreenClassSeen(new ScreenClassSeen(
            input.Manager, ClassNameOf(input.Manager), owner, owner is { } o ? ClassNameOf(o) : null, named));
    }

    private string? ClassNameOf(long instance) => RttiName.Of(
        instance,
        module.ModuleBase,
        module.ModuleSize,
        address => ModuleMemoryReader.ReadPointer((nint)address),
        address => ModuleMemoryReader.ReadInt32((nint)address),
        address => ModuleMemoryReader.ReadAnsiString((nint)address, 256));
}
