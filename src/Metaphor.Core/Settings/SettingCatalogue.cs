using Metaphor.Core.Features;

namespace Metaphor.Core.Settings;

/// <summary>Every feature's on/off setting, gathered from the feature modules (#784, ADR 0022), in
/// <see cref="SettingKey.Order"/>: the settings file is read for these keys, and a reload says changes in this order.
/// The modules' sliders come with them (#754), for the settings reference and the settings menu.</summary>
public sealed class SettingCatalogue
{
    private SettingCatalogue(IReadOnlyList<Enum> features, IReadOnlyList<SliderLimit> limits)
    {
        Features = features;
        Limits = limits;
    }

    /// <summary>No feature settings, for code that reads only the general settings, cues and keys.</summary>
    public static SettingCatalogue Empty { get; } = new([], []);

    /// <summary>The settings, smallest <see cref="SettingKey.Order"/> first.</summary>
    public IReadOnlyList<Enum> Features { get; }

    /// <summary>The sliders, smallest <see cref="SliderLimit.Order"/> first.</summary>
    public IReadOnlyList<SliderLimit> Limits { get; }

    /// <summary>The members of one feature enum.</summary>
    public static SettingCatalogue Of<TFeature>()
        where TFeature : struct, Enum => From(Enum.GetValues<TFeature>().Select(feature => (Enum)feature));

    /// <summary>The settings and sliders <paramref name="modules"/> declare. Two sliders with one key is a mistake in
    /// the code and throws.</summary>
    public static SettingCatalogue From(IEnumerable<IFeatureModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var listed = modules.ToList();
        var limits = listed.SelectMany(module => module.Limits).ToList();
        if (limits.GroupBy(limit => limit.Path, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1) is
            { } twice)
        {
            throw new ArgumentException($"two sliders share the key {twice.Key}", nameof(modules));
        }
        return From(listed.SelectMany(module => module.Settings), [.. limits.OrderBy(limit => limit.Order)]);
    }

    /// <summary><paramref name="features"/>, each with its <see cref="SettingKeyAttribute"/>. Two with one key, or one
    /// listed twice, is a mistake in the code and throws.</summary>
    public static SettingCatalogue From(IEnumerable<Enum> features) => From(features, []);

    private static SettingCatalogue From(IEnumerable<Enum> features, IReadOnlyList<SliderLimit> limits)
    {
        ArgumentNullException.ThrowIfNull(features);
        var keyed = features.Select(feature => (Feature: feature, Key: SettingKey.Of(feature))).ToList();
        if (keyed.GroupBy(pair => pair.Key.Path, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1) is
            { } twice)
        {
            throw new ArgumentException($"two settings share the key {twice.Key}", nameof(features));
        }
        return new([.. keyed.OrderBy(pair => pair.Key.Order).Select(pair => pair.Feature)], limits);
    }
}
