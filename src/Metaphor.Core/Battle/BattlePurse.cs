namespace Metaphor.Core.Battle;

/// <summary>The party's money and MAG as the game data block holds them (#166), so a battle's rewards are the
/// difference between its start and its end.</summary>
public interface IBattlePurse
{
    /// <summary>The money now, or null when unknown.</summary>
    int? Money();

    /// <summary>The MAG now, or null when unknown.</summary>
    int? Mag();
}
