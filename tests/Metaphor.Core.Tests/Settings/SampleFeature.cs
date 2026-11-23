using Metaphor.Core.Settings;

namespace Metaphor.Core.Tests.Settings;

/// <summary>Features for Core's settings tests, which can't see the feature projects' own enums (#784): keys like
/// theirs, one still being tested.</summary>
public enum SampleFeature
{
    [SettingKey("speech", "dialogue", "Dialogue", Order = 1, Description = "Dialogue lines.")]
    Dialogue,

    [SettingKey("speech", "menus", "Menus", Order = 2, Description = "Menus.")]
    Menus,

    [SettingKey("field", "nearby", "Nearby list", Order = 3, Description = "The nearby list.")]
    Nearby,

    [SettingKey("speech", "fresh", "Fresh", Testing = true, Order = 4, Description = "A new feature.")]
    Fresh,
}

/// <summary>The sample features as the settings reader takes them.</summary>
public static class SampleSettings
{
    public static SettingCatalogue Catalogue { get; } = SettingCatalogue.Of<SampleFeature>();
}
