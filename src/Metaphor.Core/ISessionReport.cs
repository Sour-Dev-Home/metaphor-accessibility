using Metaphor.Core.Menus;

namespace Metaphor.Core;

/// <summary>Where the player's report lines go (#597): the mark key's markers and the silent screen list. The report
/// log writes them; nothing is sent anywhere (ADR 0018).</summary>
public interface ISessionReport
{
    /// <summary>The mark key was pressed; <paramref name="number"/> counts the presses this session, from 1.</summary>
    void Mark(int number);

    /// <summary>The mod said nothing for <paramref name="change"/>.</summary>
    void Silent(ScreenEvent change);

    /// <summary>The silent list is full at <paramref name="limit"/> entries.</summary>
    void SilentListFull(int limit);
}
