using Metaphor.Core.Features;

namespace Metaphor.Features.Updates;

/// <summary>The updates project's settings (#784, ADR 0022). Its one feature, the update notice (#576), is wired by the
/// hook layer's entry point instead, beside the update check's request (ADR 0018), since both need the mod's version.
/// </summary>
public sealed class UpdatesModule : IFeatureModule
{
    public IEnumerable<Enum> Settings => Enum.GetValues<UpdatesFeature>().Select(feature => (Enum)feature);

    public void Register(FeatureContext context) => ArgumentNullException.ThrowIfNull(context);
}
