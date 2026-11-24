using System.Buffers.Binary;
using System.Globalization;
using Metaphor.Core;
using Metaphor.Features.Field;

namespace Metaphor.Hooks;

/// <summary>The report (corner advice) line open, <c>0xf81cd0</c> in <c>.ecode</c> (named <c>ReportMessageOpen</c>
/// here): <c>(component, key*, ?, int*, int*, int*)</c>, which every field report job, the <c>MsgReportOpenEx</c> and
/// <c>MsgReportOpenType</c> script functions and the battle report component call to queue or show one line. The key
/// holds 8 bytes, then a kind byte at <c>+0x8</c>: kind 1's 8 bytes point at the label's 16-byte hash, and kind 0's are
/// hashed as a label, most likely a pointer to the label's name <c>[NEEDS VERIFICATION]</c>. Our reading of game build
/// 548ddc95… (Steam build 18330018) in docs/findings/corner-advice-report-message-open-static.md; the name is ours.
/// </summary>
/// <remarks>A Release hook that only reads, so <see cref="CornerAdvice"/> can say the line (#587): the loader's adapter
/// guards Observe and calls the original unchanged (rule 14), with all six arguments pointer-sized (rule 9). In a Release
/// build with the setting off it reads nothing; otherwise 16 bytes at the key, then 16 bytes (kind 1) or a short string
/// (kind 0) at its pointer. A Debug build also logs each open's kind and key.</remarks>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class ReportMessageOpenHook : IGameHook, IObserves6
{
    private const int KeyLength = 16;
    private const int KindOffset = 8;
    private const int HashLength = 16;
    private const int LabelMaxBytes = 128;

    private readonly MessageLog? log;
    private readonly CornerAdvice? advice;

    public ReportMessageOpenHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
        advice = services.CornerAdvice;
    }

    public string SignatureName => "ReportMessageOpen";

    /// <summary>Observed before the line is queued; only the key is read, the rest pass on unread.</summary>
    public void Observe(nint component, nint key, nint third, nint fourth, nint fifth, nint sixth)
    {
        var wanted = advice is not null && advice.Wanted();
        if (log is null && !wanted)
        {
            return;
        }
        var bytes = new byte[KeyLength];
        if (!ModuleMemoryReader.ReadBytes(key, bytes))
        {
            log?.Requested(SignatureName, "key=(unreadable)");
            return;
        }
        var pointer = (nint)BinaryPrimitives.ReadInt64LittleEndian(bytes);
        var kind = bytes[KindOffset];
        string? label = null;
        string? hash = null;
        if (kind == 1)
        {
            var read = new byte[HashLength];
            hash = ModuleMemoryReader.ReadBytes(pointer, read) ? Convert.ToHexString(read) : null;
        }
        else if (kind == 0)
        {
            label = Label(ModuleMemoryReader.ReadAnsiString(pointer, LabelMaxBytes));
        }
        log?.Requested(SignatureName, string.Create(
            CultureInfo.InvariantCulture,
            $"kind={kind} head={Convert.ToHexString(bytes, 0, KindOffset)} hash={hash ?? "-"} label={label ?? "-"}"));
        if (wanted && (label is not null || hash is not null))
        {
            advice!.Opened(label, hash);
        }
    }

    // A label is letters, digits and underscores; anything else read there isn't one.
    private static string? Label(string? text) =>
        !string.IsNullOrEmpty(text) && text.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ? text : null;
}
