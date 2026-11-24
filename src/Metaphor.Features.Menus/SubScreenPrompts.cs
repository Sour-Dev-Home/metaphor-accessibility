using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Says a menu screen's prompt once when it opens (#907, ADR 0023): "Which item?" after the Item screen's name
/// and focused row, "Change whose gear?" after the Equipment screen's. The prompt is the system file's message by its
/// label, looked up when said, and queues after the screen's own line. Safe to call from several threads.</summary>
/// <remarks>The labels (docs/findings/chapter1-silent-text-audit-logged.md, item 7, build 548ddc95, Steam build
/// 18330018): <c>MainMenu_Item_01</c>, <c>MainMenu_Equip_01</c>, <c>MainMenu_Skill_01</c> and <c>MainMenu_Party_18</c>
/// were fetched in chapter 1 sessions and not spoken. Only the first two have a screen here: Item's list and Equipment's
/// character list, which the audit pairs with "Which item?" and "Change whose gear?". Which screen class shows the Skill
/// and Party prompts is <c>[NEEDS VERIFICATION]</c>, so they are not mapped.</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.MenuPrompts)]
public sealed class SubScreenPrompts : IScreenObserver
{
    // Screen class to the prompt's label.
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        [ItemFocus.List.ScreenClass] = "MainMenu_Item_01",
        [EquipmentFocus.Characters.ScreenClass] = "MainMenu_Equip_01",
    };

    private readonly IMessageLookup messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;

    public SubScreenPrompts(IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
    }

    /// <summary>Says the prompt when one of the mapped screens is entered; nothing on a focus move or a leave.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Change != ScreenChange.Entered || !Labels.TryGetValue(change.ScreenClass, out var label)
            || messages.Find(label) is not { } message || cleaner.Clean(message.Text) is not { Length: > 0 } text)
        {
            return;
        }
        announcer.Announce(new Announcement(text, Interrupt: false));
    }
}
