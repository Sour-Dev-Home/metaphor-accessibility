using System.Globalization;

namespace Metaphor.Core.Menus;

// Issue #250's menu-state read ("menus: know when the menus are closed", under #211): the game's own test behind the
// script function CampStatus_IsClosed, replayed by reading. Our reading of the exe on one build
// (docs/raw-sources/capture-2-field-leads-2026-11-17.md): a global holds a handle whose 32-bit kind at +0 picks one of
// three linked lists, and the handle counts as open while a node of that list has the handle's id at +0xB0. What the
// handle and the lists are is [NEEDS VERIFICATION] until a session logs them.

/// <summary>The camp menu's open state as the #250 watch reads it: the handle (0 when the global is null), its kind,
/// its id, whether a live node carries that id, and the classes of the handle and that node.</summary>
public sealed record MenuOpenState(long Handle, string? HandleClass, int? Kind, long? Id, bool? Alive, string? NodeClass)
{
    /// <summary>The handle's kind: <c>1</c> and <c>3</c> walk lists linked at <c>+0x68</c>, <c>2</c> one linked at
    /// <c>+0x78</c>; any other kind is never open.</summary>
    public const int KindOffset = 0x00;

    /// <summary>Where the handle and each node keep the id compared.</summary>
    public const int IdOffset = 0xB0;

    /// <summary>The most nodes a walk follows before it gives up and reports the state as unknown.</summary>
    public const int MaxNodes = 4096;

    /// <summary>The next-node offset for a handle of <paramref name="kind"/>, or null for a kind that is never open.</summary>
    public static int? NextOffset(int kind) => kind switch
    {
        1 or 3 => 0x68,
        2 => 0x78,
        _ => null,
    };

    /// <summary>The first node from <paramref name="head"/>, following <paramref name="nextOffset"/>, whose id is
    /// <paramref name="id"/>: 0 when the list ends without one, null when a read fails or the walk passes
    /// <see cref="MaxNodes"/>.</summary>
    public static long? FindNode(long head, int nextOffset, long id, Func<long, long?> readPointer)
    {
        ArgumentNullException.ThrowIfNull(readPointer);
        var node = head;
        for (var walked = 0; node != 0; walked++)
        {
            if (walked >= MaxNodes || readPointer(node + IdOffset) is not { } nodeId)
            {
                return null;
            }
            if (nodeId == id)
            {
                return node;
            }
            if (readPointer(node + nextOffset) is not { } next)
            {
                return null;
            }
            node = next;
        }
        return 0;
    }

    public override string ToString() => Handle == 0
        ? "camp=none"
        : string.Create(CultureInfo.InvariantCulture,
            $"camp=0x{Handle:x} class={HandleClass ?? "?"} kind={Kind?.ToString(CultureInfo.InvariantCulture) ?? "?"} id={(Id is { } i ? $"0x{i:x}" : "?")} alive={(Alive switch { true => "1", false => "0", null => "?" })} node={NodeClass ?? "-"}");
}
