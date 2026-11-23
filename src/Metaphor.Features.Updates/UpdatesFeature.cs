using Metaphor.Core.Settings;

namespace Metaphor.Features.Updates;

/// <summary>The updates project's features, each with its own on/off key in the settings file (#661, #784). A new
/// feature gets a member here as <c>DialogueFeature</c> describes.</summary>
public enum UpdatesFeature
{
    /// <summary>The title screen's notice of a newer release (#576). The request itself is <c>checkForUpdates</c>.
    /// </summary>
    [SettingKey("speech", "updateNotice", "Update notice", Order = 1160, Description = "The title screen's notice of " +
        "a newer version. The request itself is `checkForUpdates`.")]
    UpdateNotice,
}
