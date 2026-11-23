namespace Metaphor.Core.Features;

/// <summary>One feature project's registration (#784, ADR 0022): the settings it declares and the wiring of its
/// features. The hook layer names each module once, in an explicit list, and hands each the same
/// <see cref="FeatureContext"/> in that list's order; so a new feature in a project touches only that project.
/// </summary>
public interface IFeatureModule
{
    /// <summary>The members of the project's feature enum, each with its <c>[SettingKey]</c>.</summary>
    IEnumerable<Enum> Settings { get; }

    /// <summary>Builds the project's features over <paramref name="context"/>'s shared services, each behind its
    /// setting's gate, and hands the context their observers, controls and ports.</summary>
    void Register(FeatureContext context);
}
