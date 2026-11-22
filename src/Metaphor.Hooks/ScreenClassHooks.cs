using System.Collections.Concurrent;
using Metaphor.Core;
using Metaphor.Core.Menus;

namespace Metaphor.Hooks;

// Issues #213 and #208: the menu choices' shared cursor getter, hooked in every build so each cursor input names its
// screen class. The function, its arguments and the field offsets are our reading of the exe on one build
// (docs/raw-sources/screen-class-link-2026-11-17.md); that the owner's class names the screen on 926 of 948 inputs is
// from a run on that build (docs/raw-sources/screen-class-session-2026-11-17.md). The class names are the game's own,
// from its run-time type information. Every argument is observed pointer-sized (rule 9).

/// <summary>The shared cursor getter of the game's menu choices (the 12th virtual function of 279 classes, among them
/// <c>TitleChoice</c> and <c>RootMenuChoice</c>): <c>shared_ptr&lt;CursorManagerBase&gt;* (Choice* this,
/// shared_ptr&lt;CursorManagerBase&gt;* out)</c>, which copies the choice's manager pointer from <c>+0x230</c> into
/// <c>out</c>. Each call records which choice handed out which manager, for <see cref="ScreenClassLookup"/>. It may run
/// every frame for every open screen, so a call costs one pointer read and, for a pair already known, one lock-free
/// lookup.</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class ChoiceCursorGetterHook : IGameHook, IObserves4WithResult
{
    private readonly CursorOwners owners;

    public ChoiceCursorGetterHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        owners = services.ScreenClasses.Owners;
    }

    public string SignatureName => "ChoiceGetCursor";

    /// <summary>Observed after the getter returns, when <paramref name="output"/> holds the manager pointer.</summary>
    public void Observe(nint choice, nint output, nint third, nint fourth, nint result)
    {
        if (ModuleMemoryReader.ReadPointer(output) is { } manager && manager != 0)
        {
            owners.Saw(choice, manager);
        }
    }
}

/// <summary>The update function of New Game's difficulty list (#540), <c>UUIC_Common_DifficultySelect</c>'s 14th
/// virtual function: <c>void (UUIC_Common_DifficultySelect* this)</c>. The list isn't a choice, so it never calls the
/// shared cursor getter and its cursor inputs had no screen class; its update reads its input through the
/// <c>CursorManager</c> held at <c>+0x28</c>, so each call records the list as that manager's owner, before the input
/// is read. The function, the field and that the manager is the one the menu cursor hook logs are our reading of the
/// exe on one build (docs/findings/difficulty-select-shared-cursor.md). One pointer read a call.</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class DifficultySelectOwnerHook : IGameHook, IObserves3
{
    private const int ManagerOffset = 0x28;

    private readonly CursorOwners owners;

    public DifficultySelectOwnerHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        owners = services.ScreenClasses.Owners;
    }

    public string SignatureName => "DifficultySelectUpdate";

    /// <summary>Observed before the update runs; only <paramref name="list"/> is an argument, the other two are
    /// whatever the registers held.</summary>
    public void Observe(nint list, nint second, nint third)
    {
        if (ModuleMemoryReader.ReadPointer(list + ManagerOffset) is { } manager && manager != 0)
        {
            owners.Saw(list, manager);
        }
    }
}

/// <summary>Names the screen class of a cursor manager: the class of the choice that last handed it out through
/// <see cref="ChoiceCursorGetterHook"/>, such as <c>TitleChoice@ope@ui@app</c>. Names are kept by the choice's vtable,
/// so each class's type information is read once. Safe from any game thread.</summary>
internal sealed class ScreenClassLookup
{
    private readonly ClassNames names = new();

    /// <summary>Which choice last handed out each manager.</summary>
    public CursorOwners Owners { get; } = new();

    /// <summary>The screen class of <paramref name="manager"/>'s owner, or null when no owner was seen or its class is
    /// unreadable.</summary>
    public string? Of(long manager) => Owners.OwnerOf(manager) is { } owner ? names.Of((nint)owner) : null;

    /// <summary>The mode field of <paramref name="manager"/>'s owner, whose class is <paramref name="screenClass"/>
    /// (<see cref="ScreenModes"/>, #210), or null when the class has none, no owner was seen, or it is unreadable.
    /// </summary>
    public int? ModeOf(long manager, string? screenClass) =>
        ScreenModes.FieldOffset(screenClass) is { } offset && Owners.OwnerOf(manager) is { } owner
            ? ModuleMemoryReader.ReadInt32((nint)(owner + offset))
            : null;
}

/// <summary>Names a game object's class from its run-time type information (<see cref="RttiName"/>), such as
/// <c>TitleChoice@ope@ui@app</c>. Names are kept by vtable, so each class's type information is read once and a known
/// class costs one pointer read and one lock-free lookup. Safe from any game thread.</summary>
internal sealed class ClassNames
{
    // Far more than the 279 choice classes or the few hundred UI layouts; past it the names are read again.
    private const int MaxClasses = 1024;

    private readonly CallerAddress module = new();
    private readonly ConcurrentDictionary<nint, string> classByVtable = new();

    /// <summary>The class of the object at <paramref name="instance"/>, or null when it is unreadable.</summary>
    public string? Of(nint instance)
    {
        if (ModuleMemoryReader.ReadPointer(instance) is not { } vtable)
        {
            return null;
        }
        if (classByVtable.TryGetValue(vtable, out var known))
        {
            return known;
        }
        var name = RttiName.Of(
            instance,
            module.ModuleBase,
            module.ModuleSize,
            address => ModuleMemoryReader.ReadPointer((nint)address),
            address => ModuleMemoryReader.ReadInt32((nint)address),
            address => ModuleMemoryReader.ReadAnsiString((nint)address, 256));
        if (name is not null)
        {
            if (classByVtable.Count >= MaxClasses)
            {
                classByVtable.Clear();
            }
            classByVtable[vtable] = name;
        }
        return name;
    }
}
