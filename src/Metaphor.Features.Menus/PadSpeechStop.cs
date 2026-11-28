using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Opt-in controller speech interrupt (#1036): when <see cref="MenusFeature.PadSpeechStop"/> is on, pressing a
/// controller button stops whatever announcement is being read before downstream controls and the game speak what that
/// press brings up. Triggers (<c>LT</c> and <c>RT</c>) and held chord modifiers (<c>LB</c>, <c>RB</c> and any button
/// currently held back for a mod chord) are excluded so camera triggers and mod chords never cut off speech by accident.
/// <para>Idea from Unseen Desire 0.1.9, a Persona 5 Royal accessibility mod by Haru (heavenay.com/unseenDesire; GPL-3.0
/// per its docs, source not published; 0.1.9 release announcement read 2026-11-28); idea only, no code read
/// (CREDITS.md).</para></summary>
[FeatureSetting<MenusFeature>(MenusFeature.PadSpeechStop)]
public sealed class PadSpeechStop : IInputControls
{
    private readonly Lock gate = new();
    private readonly Action stop;
    private readonly Func<int> chordModifiers;
    private readonly Dictionary<int, int> padsBefore = [];

    /// <param name="stop">Stops all ongoing speech (<c>StoppableAnnouncer.StopAll</c>).</param>
    /// <param name="chordModifiers">Buttons currently acting as chord modifiers, exempt alongside
    /// <see cref="ShoulderTaps.Triggers"/>; defaults to <see cref="ShoulderTaps.Shoulders"/> (<c>LB</c> and <c>RB</c>).
    /// </param>
    public PadSpeechStop(Action stop, Func<int>? chordModifiers = null)
    {
        ArgumentNullException.ThrowIfNull(stop);
        this.stop = stop;
        this.chordModifiers = chordModifiers ?? (() => ShoulderTaps.Shoulders);
    }

    public int Pad(int pad, int buttons, bool made)
    {
        if (!made)
        {
            return buttons;
        }
        int down;
        lock (gate)
        {
            down = buttons & ~padsBefore.GetValueOrDefault(pad);
            padsBefore[pad] = buttons;
        }
        var exempt = ShoulderTaps.Triggers | chordModifiers();
        if ((down & ~exempt) != 0)
        {
            stop();
        }
        return buttons;
    }

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys) => keys;
}
