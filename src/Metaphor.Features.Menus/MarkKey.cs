using Metaphor.Core;
using Metaphor.Core.Cues;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>The mark key (#597): the player presses it when something was wrong or missing, anywhere in the game, and
/// it writes a numbered marker with the time to the report log and plays the mark cue, so whoever reads the log finds
/// the moment without the player describing it. Nothing is sent anywhere. While the feature is on, the key is dropped
/// from every keyboard read, so the game never sees it. Keyboard only for now: the pad gets it through the RB layer
/// (#688). Safe to use from several threads.</summary>
[FeatureSetting<MenusFeature>(MenusFeature.MarkKey)]
public sealed class MarkKey : IInputControls
{
    /// <summary>F8 (<c>0x77</c>), off the number row and away from the mod's letter keys. That the game binds nothing
    /// to F8 is <c>[NEEDS VERIFICATION]</c>: no saved source lists the game's keyboard map; the game's keyboard read
    /// lists F1 to F12 when held (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md).</summary>
    public const int DefaultKey = 0x77;

    private readonly Lock gate = new();
    private readonly Func<ModSettings> settings;
    private readonly ICuePlayer cues;
    private readonly ISessionReport report;
    private bool held;
    private int marks;

    /// <param name="settings">The settings in force, read on each keyboard read: the feature's toggle and
    /// <c>keys.report.markKey</c>.</param>
    /// <param name="cues">Where the mark cue plays.</param>
    /// <param name="report">Where each marker is written.</param>
    public MarkKey(Func<ModSettings> settings, ICuePlayer cues, ISessionReport report)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(cues);
        ArgumentNullException.ThrowIfNull(report);
        this.settings = settings;
        this.cues = cues;
        this.report = report;
    }

    public int Pad(int pad, int buttons, bool made) => buttons;

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var now = settings();
        var key = now.Binding("report", "markKey", DefaultKey);
        var down = now.IsOn(MenusFeature.MarkKey) && key != 0 && keys.Contains(key);
        int number;
        lock (gate)
        {
            var pressed = down && !held;
            held = down;
            if (!pressed)
            {
                return down ? [.. keys.Where(k => k != key)] : keys;
            }
            number = ++marks;
        }
        report.Mark(number);
        cues.Play(Cue.Mark);
        return [.. keys.Where(k => k != key)];
    }
}
