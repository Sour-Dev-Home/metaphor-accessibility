using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Metaphor.Core;
using Reloaded.Hooks.Definitions;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace Metaphor.Hooks;

// Issue #55's spike, last step: development-only hooks on the inner functions every MsgRequest… script function calls,
// to see which one a spoken event line goes through and what it is passed. They pass every call through unchanged.
// The by-pointer request (MsgRequestByName) is logged by DialogueLineHook's log observer (#86). The functions, their arguments and the names (ours) are in
// docs/raw-sources/msg-request-functions-2026-11-16.md.
//
// Delegate shapes: every integer or pointer is pointer-sized, which is right whichever width the game reads. The
// float's width is unverified, so it is declared double: a double argument travels through as the register's low 64
// bits unchanged, which carries a 32-bit float intact too. The return is whatever the deeper function left in rax.

/// <summary>What the listeners share: formatting what a request was passed, read safely.</summary>
internal static class MsgRequestListening
{
    /// <summary>Logs one request to <paramref name="log"/>, if there is one. Nothing here may throw into the game, and
    /// every read through a pointer the game passed goes through <c>ReadProcessMemory</c>.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "An exception must never cross back into the game.")]
    public static void Log(MessageLog? log, string function, Func<string> arguments)
    {
        try
        {
            log?.Requested(function, arguments());
        }
        catch (Exception)
        {
        }
    }

    /// <summary>The pointer argument, as a C string if it reads as one. That it is a string is unverified.</summary>
    public static string Text(nint pointer) =>
        ModuleMemoryReader.ReadAnsiString(pointer) is { } text ? $"text=\"{MessageLog.Escaped(text)}\"" : "text=(unreadable)";

    /// <summary>The 12-byte options block the script wrappers build, as hex.</summary>
    public static string Options(nint options) =>
        ModuleMemoryReader.ReadPointer(options) is { } low && ModuleMemoryReader.ReadInt32(options + 8) is { } high
            ? string.Create(CultureInfo.InvariantCulture, $"options={low:x16}+{high:x8}")
            : "options=(unreadable)";

    /// <summary>The float argument under both readings, since its width is unverified.</summary>
    public static string Time(double time)
    {
        var asSingle = BitConverter.Int32BitsToSingle((int)BitConverter.DoubleToInt64Bits(time));
        return string.Create(CultureInfo.InvariantCulture, $"time={asSingle:R}(32-bit)|{time:R}(64-bit)");
    }

    public static string Kind(nint kind) => string.Create(CultureInfo.InvariantCulture, $"kind={(int)kind}");

    public static string Numbers(nint first, nint second) =>
        string.Create(CultureInfo.InvariantCulture, $"a={(int)first} b={(int)second}");
}

/// <summary>The byte-identical copy of the by-pointer request; who calls it is what this hook is here to find out.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class MsgRequestByNameTwinListener : IGameHook
{
    private delegate nint Request(nint kind, nint text, nint options);

    private readonly Request callback;
    private readonly MessageLog? log;
    private IHook<Request>? hook;

    public MsgRequestByNameTwinListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
        callback = OnRequest;
    }

    public string SignatureName => "MsgRequestByNameTwin";

    public void Install(IReloadedHooks hooks, nint address, IReadOnlyDictionary<string, nint> calls)
    {
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    private nint OnRequest(nint kind, nint text, nint options)
    {
        MsgRequestListening.Log(log, SignatureName, () =>
            $"{MsgRequestListening.Kind(kind)} {MsgRequestListening.Text(text)} {MsgRequestListening.Options(options)}");
        return hook!.OriginalFunction(kind, text, options);
    }
}

/// <summary>A request by two numbers: what <c>MsgRequestID</c> and <c>MsgRequestSystemMessageID</c> call.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class MsgRequestByIdListener : IGameHook
{
    private delegate nint Request(nint kind, nint first, nint second, nint options);

    private readonly Request callback;
    private readonly MessageLog? log;
    private IHook<Request>? hook;

    public MsgRequestByIdListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
        callback = OnRequest;
    }

    public string SignatureName => "MsgRequestById";

    public void Install(IReloadedHooks hooks, nint address, IReadOnlyDictionary<string, nint> calls)
    {
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    private nint OnRequest(nint kind, nint first, nint second, nint options)
    {
        MsgRequestListening.Log(log, SignatureName, () =>
            $"{MsgRequestListening.Kind(kind)} {MsgRequestListening.Numbers(first, second)} {MsgRequestListening.Options(options)}");
        return hook!.OriginalFunction(kind, first, second, options);
    }
}

/// <summary>A timed request by pointer: what <c>MsgRequestAuto</c> calls.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class MsgRequestByNameAutoListener : IGameHook
{
    private delegate nint Request(nint kind, nint text, double time, nint options);

    private readonly Request callback;
    private readonly MessageLog? log;
    private IHook<Request>? hook;

    public MsgRequestByNameAutoListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
        callback = OnRequest;
    }

    public string SignatureName => "MsgRequestByNameAuto";

    public void Install(IReloadedHooks hooks, nint address, IReadOnlyDictionary<string, nint> calls)
    {
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    private nint OnRequest(nint kind, nint text, double time, nint options)
    {
        MsgRequestListening.Log(log, SignatureName, () =>
            $"{MsgRequestListening.Kind(kind)} {MsgRequestListening.Text(text)} {MsgRequestListening.Time(time)} {MsgRequestListening.Options(options)}");
        return hook!.OriginalFunction(kind, text, time, options);
    }
}

/// <summary>A timed request by two numbers: what <c>MsgRequestIDAuto</c> calls.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class MsgRequestByIdAutoListener : IGameHook
{
    private delegate nint Request(nint kind, nint first, nint second, double time, nint options);

    private readonly Request callback;
    private readonly MessageLog? log;
    private IHook<Request>? hook;

    public MsgRequestByIdAutoListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
        callback = OnRequest;
    }

    public string SignatureName => "MsgRequestByIdAuto";

    public void Install(IReloadedHooks hooks, nint address, IReadOnlyDictionary<string, nint> calls)
    {
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    private nint OnRequest(nint kind, nint first, nint second, double time, nint options)
    {
        MsgRequestListening.Log(log, SignatureName, () =>
            $"{MsgRequestListening.Kind(kind)} {MsgRequestListening.Numbers(first, second)} {MsgRequestListening.Time(time)} {MsgRequestListening.Options(options)}");
        return hook!.OriginalFunction(kind, first, second, time, options);
    }
}
