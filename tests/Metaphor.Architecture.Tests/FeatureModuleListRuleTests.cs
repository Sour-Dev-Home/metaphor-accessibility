using Mono.Cecil;

namespace Metaphor.Architecture.Tests;

/// <summary>The composition's explicit list of feature modules (#784, ADR 0022, ADR 0026) names every module, once, in the order
/// that keeps each input read and each event where it was before the move.</summary>
[Trait("Category", "Unit")]
public sealed class FeatureModuleListRuleTests
{
    private static readonly AssemblyDefinition Core = Plant.Compile("Metaphor.Core", """
        namespace Metaphor.Core.Features
        {
            public interface IFeatureModule { }
        }
        """);

    private static readonly AssemblyDefinition Bells = Plant.Compile("Metaphor.Features.Bells", """
        public sealed class BellsModule : Metaphor.Core.Features.IFeatureModule { }
        public sealed class ChimesModule : Metaphor.Core.Features.IFeatureModule { }
        """, references: Core);

    private static AssemblyDefinition Hooks(string modules) => Plant.Compile("Metaphor.Composition", $$"""
        namespace Metaphor.Composition
        {
            internal static class FeatureModules
            {
                public static Metaphor.Core.Features.IFeatureModule[] Create() => [{{modules}}];
            }
        }
        """, references: [Core, Bells]);

    [Fact]
    public void AModuleLeftOutOfTheListIsAViolation()
    {
        var violation = Assert.Single(Rules.ModulesAreListed(Hooks("new BellsModule()"), [Bells]));

        Assert.Contains("ChimesModule is not in Metaphor.Composition.FeatureModules.Create", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void AModuleListedTwiceIsAViolation()
    {
        var violations = Rules.ModulesAreListed(
            Hooks("new BellsModule(), new ChimesModule(), new BellsModule()"), [Bells]);

        Assert.Contains("BellsModule is in Metaphor.Composition.FeatureModules.Create more than once", Assert.Single(violations),
            StringComparison.Ordinal);
    }

    [Fact]
    public void EveryModuleListedOncePasses() =>
        Assert.Empty(Rules.ModulesAreListed(Hooks("new ChimesModule(), new BellsModule()"), [Bells]));

    [Fact]
    public void TheRealListNamesEveryModule()
    {
        var hooks = Scanned.Load("Metaphor.Composition");
        var features = Scanned.AssemblyNames.Where(name => name.StartsWith(Rules.FeaturePrefix, StringComparison.Ordinal))
            .Select(Scanned.Load).ToList();

        Assert.NotEmpty(features);
        Assert.Empty(Rules.ModulesAreListed(hooks, features));
    }

    // The order is behaviour (Metaphor.Settings.Tests registers the modules in it): the mark key and the menus take
    // each read first, the battle keys next, then the dialogue, the field last; name entry hears a shown line after
    // the dialogue's lines.
    [Fact]
    public void TheRealListKeepsItsOrder() =>
        Assert.Equal(
            [
                "Metaphor.Features.Menus.MenusModule", "Metaphor.Features.Battle.BattleModule",
                "Metaphor.Features.Dialogue.DialogueModule", "Metaphor.Features.Menus.NameEntryModule",
                "Metaphor.Features.Field.FieldModule", "Metaphor.Features.Updates.UpdatesModule",
            ],
            Rules.ListedModules(Scanned.Load("Metaphor.Composition")));
}
