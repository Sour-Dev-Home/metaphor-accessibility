using System.Globalization;
using Metaphor.Core;
using Metaphor.Core.Messages;
using Metaphor.Features.Menus;

namespace Metaphor.Hooks;

// Issues #545 and #586: the travel map's destination panel and the field map's name strip. The functions, their
// arguments and the offsets are our reading of game build 548ddc95… (Steam build 18330018) in
// docs/findings/world-map-destination-panel-static.md, docs/findings/field-map-region-area-name-static.md and
// docs/findings/travel-map-destination-text-static.md; the names are ours. Every argument is observed pointer-sized
// (rule 9) and narrowed only where it is read.

/// <summary>The panel's show-a-destination, <c>sortie::map::Detail</c>'s <c>0xcccf50</c>:
/// <c>void (Detail* this, u32 kind, u8 index, reference* name, reference* detail, list* quests)</c>, run on each
/// selection change. It reads the name and detail references and hands them to <see cref="TravelMapDestination"/>,
/// before the original copies them into the panel.</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class TravelMapPanelHook : IGameHook, IObserves6
{
    private readonly TravelMapDestination destination;
    private readonly MessageLog? log;

    public TravelMapPanelHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        destination = services.TravelMap;
        log = services.Log;
    }

    public string SignatureName => "TravelMapPanelShow";

    public void Observe(nint first, nint second, nint third, nint fourth, nint fifth, nint sixth)
    {
        var name = new byte[MessageReference.Length];
        var detail = new byte[MessageReference.Length];
        var nameRead = ModuleMemoryReader.ReadBytes(fourth, name);
        var detailRead = ModuleMemoryReader.ReadBytes(fifth, detail);
        var said = destination.Shown(
            nameRead ? MessageReference.Read(name) : null, detailRead ? MessageReference.Read(detail) : null);
        log?.MapSeen("panel", string.Create(CultureInfo.InvariantCulture,
            $"kind={(uint)second} index={(sbyte)third} name={Hex(nameRead, name)} detail={Hex(detailRead, detail)} said={Said(said)}"));
    }

    private static string Hex(bool read, byte[] bytes) => read ? Convert.ToHexStringLower(bytes) : "(unreadable)";

    internal static string Said(string? text) => text is null ? "(nothing)" : $"\"{LogText.Escaped(text)}\"";
}

/// <summary>The name strip's composer, <c>0xefe180</c>: <c>text* (const u32* id, style* style)</c>, observed after it
/// returns. It reads the place's id and the first text a probe finds behind the composed result, and hands both to
/// <see cref="FieldMapName"/>. Development-only until a run confirms the result's layout: the probe follows pointers
/// nobody vouches for, so every read avoids guard pages, and its first string is a guess that players don't hear.
/// </summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class FieldMapNameComposeHook : IGameHook, IObserves4WithResult
{
    // How much of the composed result the text probe looks through, as the text pane listener does.
    private const int ProbeBytes = 0x100;

    private readonly FieldMapName strip;
    private readonly MessageLog? log;

    public FieldMapNameComposeHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        strip = services.FieldMapName;
        log = services.Log;
    }

    public string SignatureName => "FieldMapNameCompose";

    public void Observe(nint first, nint second, nint third, nint fourth, nint result)
    {
        if (ModuleMemoryReader.ReadInt32(first) is not { } id)
        {
            return;
        }
        var fields = new byte[ProbeBytes];
        var found = ModuleMemoryReader.ReadBytesAvoidingGuardPages(result, fields)
            ? TextProbe.FirstString(fields, address => MemoryText.ReadAnsi(
                (from, buffer) => ModuleMemoryReader.ReadBytesAvoidingGuardPages((nint)from, buffer), address))
            : null;
        strip.Composed(id, found?.Text);
        log?.MapSeen("compose", string.Create(CultureInfo.InvariantCulture,
            $"id={id} result=0x{result:x} at={(found is { } f ? $"+0x{f.Offset:x}" : "(none)")} text={TravelMapPanelHook.Said(found?.Text)}"));
    }
}

/// <summary>The name strip's set-the-place, <c>0xc93070</c>: <c>void (Name* layout, const u32* id)</c>, which stores the
/// id at <c>layout+0x7e8</c> and composes the strip's name; observed after it returns, so the composer has run. It hands
/// the stored id to <see cref="FieldMapName"/>, which says that place's name. Development-only with its composer hook,
/// which alone feeds the names.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class FieldMapNameSetHook : IGameHook, IObserves4WithResult
{
    private const int IdOffset = 0x7e8;

    private readonly FieldMapName strip;
    private readonly MessageLog? log;

    public FieldMapNameSetHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        strip = services.FieldMapName;
        log = services.Log;
    }

    public string SignatureName => "FieldMapNameSetId";

    public void Observe(nint first, nint second, nint third, nint fourth, nint result)
    {
        if (ModuleMemoryReader.ReadInt32(first + IdOffset) is not { } id)
        {
            return;
        }
        var said = strip.Shown(id);
        log?.MapSeen("strip", string.Create(CultureInfo.InvariantCulture, $"id={id} said={TravelMapPanelHook.Said(said)}"));
    }
}
