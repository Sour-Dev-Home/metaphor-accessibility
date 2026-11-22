using Metaphor.Core.Announcements;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the field map's name strip (#586): when the strip is set to a place, the name the game composed for
/// that place, such as "region, area". The name is remembered by the place's id, so a strip set again without being
/// composed again says it too; a place whose name was never read says nothing. Safe to call from several threads.
/// </summary>
/// <remarks>On build 548ddc95 the strip (<c>field::map::Name</c>) stores the place's id, then composes its name from
/// three messages in code the mod can't read; the text comes from the composed result as far as a probe finds it, which
/// is not yet seen in play (docs/findings/travel-map-destination-text-static.md).</remarks>
[FeatureSetting(Feature.PlaceNames)]
public sealed class FieldMapName
{
    /// <summary>The most places remembered; past it the names are forgotten and read again.</summary>
    public const int MaxPlaces = 512;

    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly Dictionary<int, string> names = [];
    private readonly Lock gate = new();

    public FieldMapName(IAnnouncer announcer, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.announcer = announcer;
        this.cleaner = cleaner;
    }

    /// <summary>The game composed the name of place <paramref name="id"/>; <paramref name="text"/> is what was read from
    /// it, or null when nothing was.</summary>
    public void Composed(int id, string? text)
    {
        if (text is null || cleaner.Clean(text) is not { Length: > 0 } name)
        {
            return;
        }
        lock (gate)
        {
            if (names.Count >= MaxPlaces && !names.ContainsKey(id))
            {
                names.Clear();
            }
            names[id] = name;
        }
    }

    /// <summary>The strip was set to place <paramref name="id"/>. Returns what was said, or null.</summary>
    public string? Shown(int id)
    {
        string? name;
        lock (gate)
        {
            name = names.GetValueOrDefault(id);
        }
        if (name is not null)
        {
            announcer.Announce(new Announcement(name, Interrupt: true));
        }
        return name;
    }
}
