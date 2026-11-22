using Metaphor.Core.Battle;
using Metaphor.Core.Input;

namespace Metaphor.Features.Battle;

/// <summary>A key that acts only in a battle (#631): <paramref name="binding"/> on a pad or <paramref name="key"/> on the
/// keyboard, pressed alone, calls <paramref name="pressed"/> and is hidden from the game. Outside a battle it reaches
/// the game. Any other read, a chord with it included, reaches it unchanged. A press is an up-to-down edge on any pad or
/// the keyboard, so a mirroring second pad can't fire it again; whether a battle is on is asked once, at that edge, and
/// holds until every source lets go. Safe to call from any of the game's threads; <paramref name="pressed"/> runs
/// outside its lock.</summary>
internal sealed class BattleKey(int binding, int key, IBattleScene scene, Action pressed) : IInputControls
{
    private const int KeyboardSource = -1;

    private readonly Lock gate = new();
    private readonly HashSet<int> holding = [];
    private bool inBattle;

    public int Pad(int pad, int buttons, bool made)
    {
        if (!made)
        {
            return buttons;
        }
        return Read(pad, buttons == binding) ? 0 : buttons;
    }

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return Read(KeyboardSource, keys.Count == 1 && keys[0] == key) ? [] : keys;
    }

    // Whether this read is the key held in a battle, so hidden from the game.
    private bool Read(int source, bool held)
    {
        bool press;
        bool hidden;
        lock (gate)
        {
            var before = holding.Count > 0;
            if (held)
            {
                holding.Add(source);
            }
            else
            {
                holding.Remove(source);
            }
            press = !before && holding.Count > 0;
            if (press)
            {
                inBattle = scene.InBattle();
            }
            else if (holding.Count == 0)
            {
                inBattle = false;
            }
            press &= inBattle;
            hidden = held && inBattle;
        }
        if (press)
        {
            pressed();
        }
        return hidden;
    }
}
