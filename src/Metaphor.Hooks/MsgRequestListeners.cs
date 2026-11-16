using System.Globalization;
using Metaphor.Core;

namespace Metaphor.Hooks;

// Issue #55's spike, last step: development-only hooks on the inner functions every MsgRequest… script function calls,
// to see which one a spoken event line goes through and what it is passed. The loader's adapter passes every call
// through unchanged (#88). The by-pointer request (MsgRequestByName) is logged by DialogueLineHook's log observer (#86).
// The functions, their arguments and the names (ours) are in docs/raw-sources/msg-request-functions-2026-11-16.md.
//
// Argument shapes: every integer or pointer is pointer-sized, which is right whichever width the game reads. The
// float's width is unverified, so it is observed as a double: a double argument travels through as the register's low
// 64 bits unchanged, which carries a 32-bit float intact too.

/// <summary>What the listeners share: formatting what a request was passed, read safely. Every read through a pointer
/// the game passed goes through <c>ReadProcessMemory</c>.</summary>
internal static class MsgRequestListening
{
    /// <summary>The pointer argument, as a C string if it reads as one. That it is a string is unverified.</summary>
    public static string Text(nint pointer) =>
        ModuleMemoryReader.ReadAnsiString(pointer) is { } text ? $"text=\"{LogText.Escaped(text)}\"" : "text=(unreadable)";

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
internal sealed class MsgRequestByNameTwinListener : IGameHook, IObserves3
{
    private readonly MessageLog? log;

    public MsgRequestByNameTwinListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "MsgRequestByNameTwin";

    public void Observe(nint kind, nint text, nint options) =>
        log?.Requested(SignatureName,
            $"{MsgRequestListening.Kind(kind)} {MsgRequestListening.Text(text)} {MsgRequestListening.Options(options)}");
}

/// <summary>A request by two numbers: what <c>MsgRequestID</c> and <c>MsgRequestSystemMessageID</c> call.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class MsgRequestByIdListener : IGameHook, IObserves4
{
    private readonly MessageLog? log;

    public MsgRequestByIdListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "MsgRequestById";

    public void Observe(nint kind, nint first, nint second, nint options) =>
        log?.Requested(SignatureName,
            $"{MsgRequestListening.Kind(kind)} {MsgRequestListening.Numbers(first, second)} {MsgRequestListening.Options(options)}");
}

/// <summary>A timed request by pointer: what <c>MsgRequestAuto</c> calls.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class MsgRequestByNameAutoListener : IGameHook, IObserves4WithDouble
{
    private readonly MessageLog? log;

    public MsgRequestByNameAutoListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "MsgRequestByNameAuto";

    public void Observe(nint kind, nint text, double time, nint options) =>
        log?.Requested(SignatureName,
            $"{MsgRequestListening.Kind(kind)} {MsgRequestListening.Text(text)} {MsgRequestListening.Time(time)} {MsgRequestListening.Options(options)}");
}

/// <summary>A timed request by two numbers: what <c>MsgRequestIDAuto</c> calls.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class MsgRequestByIdAutoListener : IGameHook, IObserves5WithDouble
{
    private readonly MessageLog? log;

    public MsgRequestByIdAutoListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "MsgRequestByIdAuto";

    public void Observe(nint kind, nint first, nint second, double time, nint options) =>
        log?.Requested(SignatureName,
            $"{MsgRequestListening.Kind(kind)} {MsgRequestListening.Numbers(first, second)} {MsgRequestListening.Time(time)} {MsgRequestListening.Options(options)}");
}
