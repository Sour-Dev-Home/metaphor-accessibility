using Metaphor.Core.Features;
using Metaphor.Core.Settings;
using Metaphor.Features.Battle;
using Metaphor.Features.Dialogue;
using Metaphor.Features.Field;
using Metaphor.Features.Menus;
using Metaphor.Features.Updates;

namespace Metaphor.SettingsReference;

/// <summary>Every feature module in the feature projects (#784), found by type so this list can't miss one a project
/// adds; a new feature project adds its assembly here and a reference in the project file.</summary>
public static class AllFeatures
{
    private static readonly System.Reflection.Assembly[] Projects =
    [
        typeof(BattleModule).Assembly, typeof(DialogueModule).Assembly, typeof(FieldModule).Assembly,
        typeof(MenusModule).Assembly, typeof(UpdatesModule).Assembly,
    ];

    /// <summary>One of each module type, in name order.</summary>
    public static IReadOnlyList<IFeatureModule> Modules() =>
        [.. Projects.SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IFeatureModule).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => (IFeatureModule)Activator.CreateInstance(type)!)];

    /// <summary>The settings every module declares.</summary>
    public static SettingCatalogue Catalogue() => SettingCatalogue.From(Modules());
}
