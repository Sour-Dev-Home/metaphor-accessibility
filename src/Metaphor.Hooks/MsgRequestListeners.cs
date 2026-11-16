using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Reloaded.Hooks.Definitions;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace Metaphor.Hooks;

// Issue #55's spike, last step: development-only hooks on the five inner functions every MsgRequest… script function
// calls, to see which one a spoken event line goes through and what it is passed. They pass every call through
// unchanged. The functions, their arguments and the names (ours) are in
// docs/raw-sources/msg-request-functions-2026-11-16.md.
//
// Delegate shapes: every integer or pointer is pointer-sized, which is right whichever width the game reads. The
// float's width is unverified, so it is declared double: a double argument travels through as the register's low 64
// bits unchanged, which carries a 32-bit float intact too. The return is whatever the deeper function left in rax.

/// <summary>What the listeners share: formatting what a request was passed, read safely.</summary>
internal static class MsgRequestListening
{
    public const string Build = "548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504";

    /// <summary>Logs one request. Nothing here may throw into the game, and every read through a pointer the game
    /// passed goes through <c>ReadProcessMemory</c>.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "An exception must never cross back into the game.")]
    public static void Log(string function, Func<string> arguments)
    {
        try
        {
            MessageListening.Log?.Requested(function, arguments());
        }
        catch (Exception)
        {
        }
    }

    /// <summary>The pointer argument, as a C string if it reads as one. That it is a string is unverified.</summary>
    public static string Text(nint pointer) =>
        ModuleMemoryReader.ReadAnsiString(pointer) is { } text ? $"text=\"{Escaped(text)}\"" : "text=(unreadable)";

    /// <summary>Whatever the pointer hit stays on one log line: control characters, quotes and backslashes become
    /// <c>\xNN</c>.</summary>
    private static string Escaped(string text) => string.Concat(text.Select(c =>
        c < ' ' || c is '"' or '\\' ? string.Create(CultureInfo.InvariantCulture, $"\\x{(int)c:x2}") : c.ToString()));

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

/// <summary>A request by pointer: what <c>MsgRequest</c>, <c>MsgRequestSystemMessage</c>, <c>MsgRequestSubtitles</c> and
/// <c>MsgRequestNarration</c> call.</summary>
[GameHook(MsgRequestListening.Build, DevOnly = true)]
internal sealed class MsgRequestByNameListener : IGameHook
{
    private delegate nint Request(nint kind, nint text, nint options);

    private readonly Request callback;
    private IHook<Request>? hook;

    public MsgRequestByNameListener() => callback = OnRequest;

    public string SignatureName => "MsgRequestByName";

    public void Install(IReloadedHooks hooks, nint address, IReadOnlyDictionary<string, nint> calls)
    {
        // Stored before it goes live: the game may call it the moment it's activated.
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    private nint OnRequest(nint kind, nint text, nint options)
    {
        MsgRequestListening.Log(SignatureName, () =>
            $"{MsgRequestListening.Kind(kind)} {MsgRequestListening.Text(text)} {MsgRequestListening.Options(options)}");
        return hook!.OriginalFunction(kind, text, options);
    }
}

/// <summary>The byte-identical copy of the by-pointer request; who calls it is what this hook is here to find out.</summary>
[GameHook(MsgRequestListening.Build, DevOnly = true)]
internal sealed class MsgRequestByNameTwinListener : IGameHook
{
    private delegate nint Request(nint kind, nint text, nint options);

    private readonly Request callback;
    private IHook<Request>? hook;

    public MsgRequestByNameTwinListener() => callback = OnRequest;

    public string SignatureName => "MsgRequestByNameTwin";

    public void Install(IReloadedHooks hooks, nint address, IReadOnlyDictionary<string, nint> calls)
    {
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    private nint OnRequest(nint kind, nint text, nint options)
    {
        MsgRequestListening.Log(SignatureName, () =>
            $"{MsgRequestListening.Kind(kind)} {MsgRequestListening.Text(text)} {MsgRequestListening.Options(options)}");
        return hook!.OriginalFunction(kind, text, options);
    }
}

/// <summary>A request by two numbers: what <c>MsgRequestID</c> and <c>MsgRequestSystemMessageID</c> call.</summary>
[GameHook(MsgRequestListening.Build, DevOnly = true)]
internal sealed class MsgRequestByIdListener : IGameHook
{
    private delegate nint Request(nint kind, nint first, nint second, nint options);

    private readonly Request callback;
    private IHook<Request>? hook;

    public MsgRequestByIdListener() => callback = OnRequest;

    public string SignatureName => "MsgRequestById";

    public void Install(IReloadedHooks hooks, nint address, IReadOnlyDictionary<string, nint> calls)
    {
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    private nint OnRequest(nint kind, nint first, nint second, nint options)
    {
        MsgRequestListening.Log(SignatureName, () =>
            $"{MsgRequestListening.Kind(kind)} {MsgRequestListening.Numbers(first, second)} {MsgRequestListening.Options(options)}");
        return hook!.OriginalFunction(kind, first, second, options);
    }
}

/// <summary>A timed request by pointer: what <c>MsgRequestAuto</c> calls.</summary>
[GameHook(MsgRequestListening.Build, DevOnly = true)]
internal sealed class MsgRequestByNameAutoListener : IGameHook
{
    private delegate nint Request(nint kind, nint text, double time, nint options);

    private readonly Request callback;
    private IHook<Request>? hook;

    public MsgRequestByNameAutoListener() => callback = OnRequest;

    public string SignatureName => "MsgRequestByNameAuto";

    public void Install(IReloadedHooks hooks, nint address, IReadOnlyDictionary<string, nint> calls)
    {
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    private nint OnRequest(nint kind, nint text, double time, nint options)
    {
        MsgRequestListening.Log(SignatureName, () =>
            $"{MsgRequestListening.Kind(kind)} {MsgRequestListening.Text(text)} {MsgRequestListening.Time(time)} {MsgRequestListening.Options(options)}");
        return hook!.OriginalFunction(kind, text, time, options);
    }
}

/// <summary>A timed request by two numbers: what <c>MsgRequestIDAuto</c> calls.</summary>
[GameHook(MsgRequestListening.Build, DevOnly = true)]
internal sealed class MsgRequestByIdAutoListener : IGameHook
{
    private delegate nint Request(nint kind, nint first, nint second, double time, nint options);

    private readonly Request callback;
    private IHook<Request>? hook;

    public MsgRequestByIdAutoListener() => callback = OnRequest;

    public string SignatureName => "MsgRequestByIdAuto";

    public void Install(IReloadedHooks hooks, nint address, IReadOnlyDictionary<string, nint> calls)
    {
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    private nint OnRequest(nint kind, nint first, nint second, double time, nint options)
    {
        MsgRequestListening.Log(SignatureName, () =>
            $"{MsgRequestListening.Kind(kind)} {MsgRequestListening.Numbers(first, second)} {MsgRequestListening.Time(time)} {MsgRequestListening.Options(options)}");
        return hook!.OriginalFunction(kind, first, second, time, options);
    }
}
