using System.Collections.Concurrent;
using Metaphor.Core;

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

/// <summary>Names the screen class of a cursor manager: the class of the choice that last handed it out through
/// <see cref="ChoiceCursorGetterHook"/>, such as <c>TitleChoice@ope@ui@app</c>. Names are kept by the choice's vtable,
/// so each class's type information is read once. Safe from any game thread.</summary>
internal sealed class ScreenClassLookup
{
    // Far more than the 279 choice classes that share the getter; past it the names are read again.
    private const int MaxClasses = 1024;

    private readonly CallerAddress module = new();
    private readonly ConcurrentDictionary<nint, string> classByVtable = new();

    /// <summary>Which choice last handed out each manager.</summary>
    public CursorOwners Owners { get; } = new();

    /// <summary>The screen class of <paramref name="manager"/>'s owner, or null when no owner was seen or its class is
    /// unreadable.</summary>
    public string? Of(long manager)
    {
        if (Owners.OwnerOf(manager) is not { } owner || ModuleMemoryReader.ReadPointer((nint)owner) is not { } vtable)
        {
            return null;
        }
        if (classByVtable.TryGetValue(vtable, out var known))
        {
            return known;
        }
        var name = RttiName.Of(
            owner,
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
