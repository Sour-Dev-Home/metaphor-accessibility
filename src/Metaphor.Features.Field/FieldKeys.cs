using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>The field project's keys, for the help key and the settings menu's key list (#868, #754). Every binding
/// <see cref="FieldBindings.From"/> reads is here, with its default from <see cref="FieldBindings.Default"/>; a test
/// fails when one isn't.</summary>
public static class FieldKeys
{
    private static readonly FieldBindings Defaults = FieldBindings.Default;

    private static readonly BoundKey Hold = new("field", "holdButtons", Defaults.HoldButtons);

    private static readonly BoundKey CategoryHold = new("field", "categoryHoldButtons", Defaults.CategoryHoldButtons);

    /// <summary>Every key the project lists, all in the field, each behind the feature whose keys it is.</summary>
    public static IReadOnlyList<KeyEntry> All { get; } =
    [
        new(KeyPlaces.Field, FieldFeature.Nearby, new(
            "Say the whole nearby list", new("field", "nearbyKey", Defaults.NearbyKey))),
        new(KeyPlaces.Field, FieldFeature.Nearby, new(
            "Next thing nearby", new("field", "nextKey", Defaults.NextKey),
            new("field", "nextButton", Defaults.NextButton), Hold)),
        new(KeyPlaces.Field, FieldFeature.Nearby, new(
            "Thing before", new("field", "previousKey", Defaults.PreviousKey),
            new("field", "previousButton", Defaults.PreviousButton), Hold)),
        new(KeyPlaces.Field, FieldFeature.NearbyCategories, new(
            "Next nearby category", new("field", "categoryNextKey", Defaults.CategoryNextKey),
            new("field", "categoryButton", Defaults.CategoryButton), CategoryHold)),
        new(KeyPlaces.Field, FieldFeature.NearbyCategories, new(
            "Category before", new("field", "categoryPreviousKey", Defaults.CategoryPreviousKey))),
        new(KeyPlaces.Field, FieldFeature.WhereAmI, new(
            "Where am I", new("field", "placeKey", Defaults.PlaceKey),
            new("field", "placeButton", Defaults.PlaceButton), Hold)),
        new(KeyPlaces.Field, FieldFeature.Guidance, new(
            "Start or stop the guidance beep", new("field", "guideKey", Defaults.GuideKey),
            new("field", "guideButton", Defaults.GuideButton), Hold)),
        new(KeyPlaces.Field, FieldFeature.Nearby, new(
            "Log the nearest thing for the names table", new("field", "identifyKey", Defaults.IdentifyKey))
        {
            Hidden = true,
        }),
    ];
}
