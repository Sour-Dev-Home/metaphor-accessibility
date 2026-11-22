using System.Globalization;

namespace Metaphor.Core;

// Issues #649 and #696: what the nearby list's Debug log record adds for each field enemy, so a session can tell which
// AI state index is which behaviour and what a field strike does to HP. Read on game build 548ddc95 (Steam 18330018)
// in the shared Ghidra project; none of it is confirmed in a running game yet
// (docs/findings/field-enemy-strikes-and-notice-static.md, docs/findings/field-enemy-awareness-static.md).

/// <summary>A field enemy's AI state and HP as log text. The controller <c>[enemy+0x3110]</c> holds the current state
/// index as a <c>u16</c> at <c>+0x2e8</c> (<c>fldCharaCtrl_Enemy::vf4</c>, <c>0x658bc0</c>) and the requested one at
/// <c>+0x2ec</c> with its pending byte at <c>+0x2ee</c> (<c>0x656230</c>); the HP is the <c>u32</c> at
/// <c>[enemy+0x34e0]+0xc</c> (<c>fldChara::vf14</c>, <c>0x645110</c>, through <c>0x3e60f0</c>), which
/// <c>FldEnemyIsDead</c> (<c>0x8dc240</c>) tests against 0.</summary>
public static class FieldEnemyState
{
    /// <summary>The enemy's controller pointer.</summary>
    public const int ControllerOffset = 0x3110;
    /// <summary>The enemy's stats block pointer.</summary>
    public const int StatsOffset = 0x34e0;
    /// <summary>The controller's current state index, the low half of this int.</summary>
    public const int CurrentStateOffset = 0x2e8;
    /// <summary>The controller's requested state index (low half) and pending byte (third byte).</summary>
    public const int RequestedStateOffset = 0x2ec;
    /// <summary>The stats block's current HP.</summary>
    public const int HpOffset = 0xc;

    /// <summary>The enemy at <paramref name="enemy"/> as <c>aiState=8 aiNext=30 aiPending=1 hp=42</c>, each part
    /// <c>?</c> when unreadable.</summary>
    public static string Describe(long enemy, Func<long, long?> readPointer, Func<long, int?> readInt32)
    {
        ArgumentNullException.ThrowIfNull(readPointer);
        ArgumentNullException.ThrowIfNull(readInt32);
        int? current = null, next = null, pending = null, hp = null;
        if (readPointer(enemy + ControllerOffset) is { } controller && controller != 0)
        {
            current = readInt32(controller + CurrentStateOffset) & 0xffff;
            if (readInt32(controller + RequestedStateOffset) is { } requested)
            {
                next = requested & 0xffff;
                pending = (requested >> 16) & 0xff;
            }
        }
        if (readPointer(enemy + StatsOffset) is { } stats && stats != 0)
        {
            hp = readInt32(stats + HpOffset);
        }
        return string.Create(CultureInfo.InvariantCulture,
            $"aiState={Text(current)} aiNext={Text(next)} aiPending={Text(pending)} hp={Text(hp)}");
    }

    private static string Text(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "?";
}
