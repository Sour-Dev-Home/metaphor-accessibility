using Metaphor.Core.Features;
using Metaphor.Features.Battle;
using Metaphor.Features.Dialogue;
using Metaphor.Features.Field;
using Metaphor.Features.Menus;
using Metaphor.Features.Updates;

namespace Metaphor.Hooks;

/// <summary>Every feature module, in the order they register (#784, ADR 0022). The order is behaviour: each input read
/// reaches the controls in it, and each event the observers. A new feature project adds its module here; a new feature
/// in a project doesn't touch this.</summary>
internal static class FeatureModules
{
    /// <summary>The menus first (the mark key and the menus take each read first), the battle keys next, then the
    /// dialogue, name entry after the dialogue's lines (a refused name's line), the field last, and the updates.</summary>
    public static IReadOnlyList<IFeatureModule> Create() =>
        [new MenusModule(), new BattleModule(), new DialogueModule(), new NameEntryModule(), new FieldModule(),
            new UpdatesModule()];
}
