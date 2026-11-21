using System.Globalization;
using Metaphor.Core;

namespace Metaphor.Hooks;

// Issue #118: development-only hooks to find how a tutorial pop-up picks and turns its pages. The functions, their
// arguments and the window's fields are our reading of the exe on one build
// (docs/findings/tutorial-popup-window-static.md); the class names are the game's own, from its run-time type
// information. Every function is in .ecode; the pages themselves are drawn from .text1, which nothing here touches.
// Every argument is observed pointer-sized (rule 9) and narrowed only for the log.

/// <summary>Shared by the tutorial listeners: the number an open reads through its second argument, and the window's
/// state read safely.</summary>
internal static class TutorialListening
{
    public static string Number(nint at) =>
        ModuleMemoryReader.ReadInt32(at) is { } number ? number.ToString(CultureInfo.InvariantCulture) : "?";

    public static string Byte(nint value) => ((byte)value).ToString(CultureInfo.InvariantCulture);

    /// <summary>The window's state, or null when its bytes can't be read. The window is the game's own argument; the
    /// string buffers it points to are read only through the guarded reader, since the layout is still a lead.</summary>
    public static (TutorialWindowState State, byte[] Key)? Window(nint window, CallerAddress module)
    {
        var bytes = new byte[TutorialWindowState.Length];
        if (!ModuleMemoryReader.ReadBytes(window, bytes) ||
            TutorialWindowState.Read(window, module.ModuleBase, module.ModuleSize, bytes, GuardedText) is not { } state)
        {
            return null;
        }
        return (state, TutorialWindowState.StateKey(bytes));
    }

    /// <summary>The fields of the object the open builds for the tutorial, <c>model …</c>, or empty when it can't be
    /// read. The pointer comes from the window's unverified layout, so it is read only through the guarded
    /// reader.</summary>
    public static string Model(long model, CallerAddress module)
    {
        var bytes = new byte[ModelLength];
        return model != 0 && ModuleMemoryReader.ReadBytesAvoidingGuardPages((nint)model, bytes) &&
            ObjectFields.Read(model, module.ModuleBase, module.ModuleSize, bytes, sizeof(long)) is { } fields
            ? $" model {fields}"
            : "";
    }

    // A string's heap buffer, by address and length (MsvcString caps the length), through the guarded reader.
    private static string? GuardedText(long address, int length)
    {
        var bytes = new byte[length];
        return ModuleMemoryReader.ReadBytesAvoidingGuardPages((nint)address, bytes)
            ? System.Text.Encoding.UTF8.GetString(bytes)
            : null;
    }

    // The object's allocation size in both opens (the finding's step 4).
    private const int ModelLength = 0x98;
}

/// <summary>The tutorial pop-up window's open (<c>UUIC_Common_TutorialWindow</c>):
/// <c>void (window*, uint32* tutorial, bool force, uint8, uint8)</c>, the last on the stack. Logged before it runs:
/// the window, the tutorial number and the three flags.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class TutorialWindowShowListener : IGameHook, IObserves5
{
    private readonly MessageLog? log;

    public TutorialWindowShowListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "TutorialWindowShow";

    public void Observe(nint window, nint tutorial, nint force, nint fourth, nint fifth) =>
        log?.TutorialSeen("open", string.Create(CultureInfo.InvariantCulture,
            $"via={SignatureName} window=0x{window:x} id={TutorialListening.Number(tutorial)} force={TutorialListening.Byte(force)} b4={TutorialListening.Byte(fourth)} b5={TutorialListening.Byte(fifth)}"));
}

/// <summary>The controller's other open, on a second tutorial window: <c>void (window*, uint32* tutorial, bool
/// force)</c>. Logged after it runs: the tutorial number, the flag, and the window's object at <c>+0x30</c>.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class TutorialWindowShowOtherListener : IGameHook, IObserves4WithResult
{
    // Where this window keeps the object its open builds (the finding's step 4).
    private const int ModelAt = 0x30;

    private readonly MessageLog? log;
    private readonly CallerAddress module = new();

    public TutorialWindowShowOtherListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "TutorialWindowShowOther";

    public void Observe(nint window, nint tutorial, nint force, nint unused4, nint result)
    {
        if (log is null)
        {
            return;
        }
        var model = ModuleMemoryReader.ReadPointer(window + ModelAt) ?? 0;
        log.TutorialSeen("open", string.Create(CultureInfo.InvariantCulture,
            $"via={SignatureName} window=0x{window:x} id={TutorialListening.Number(tutorial)} force={TutorialListening.Byte(force)}{TutorialListening.Model(model, module)}"));
    }
}

/// <summary>The tutorial pop-up window's per-frame update (<c>UUIC_Common_TutorialWindow</c>'s vtable slot 13):
/// <c>void (window*)</c>. After each run, the window's state and its tutorial object's fields, when either changed, so a
/// page turn shows as the fields that change with it. Opt-in: it runs every frame while a window exists, and its object
/// may hold timers, so it stops after <see cref="MaxRecords"/> records.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true, OptInFile = "opt-in-tutorial-window.txt",
    OptInName = "tutorial window")]
internal sealed class TutorialWindowUpdateListener : IGameHook, IObserves4WithResult
{
    /// <summary>The most records one run writes.</summary>
    public const int MaxRecords = 2000;

    private readonly MessageLog? log;
    private readonly CallerAddress module = new();
    private readonly ChangeGate gate = new();
    private int records;

    public TutorialWindowUpdateListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "TutorialWindowUpdate";

    public void Observe(nint window, nint unused2, nint unused3, nint unused4, nint result)
    {
        if (log is null || records >= MaxRecords || TutorialListening.Window(window, module) is not { } read)
        {
            return;
        }
        var model = TutorialListening.Model(read.State.Model, module);
        if (!gate.Changed(window, [.. read.Key, .. System.Text.Encoding.ASCII.GetBytes(model)]) ||
            Interlocked.Increment(ref records) > MaxRecords)
        {
            return;
        }
        log.TutorialSeen("window", $"{read.State}{model}");
    }
}
