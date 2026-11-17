namespace Metaphor.Core.Announcements;

/// <summary>What the player hears when the mod starts: whether it is running (#187). A blind player can't read the
/// status file, so this is how they learn the mod is off.</summary>
public static class LoadMessage
{
    public const string Loaded = "Metaphor accessibility loaded";

    public const string Unsupported = "This game version isn't supported yet; the mod is off";

    /// <summary>Any other refusal, such as the hook loader failing on a supported build.</summary>
    public const string CouldNotStart = "Metaphor accessibility couldn't start; the mod is off";

    /// <summary>The announcement for <paramref name="status"/>. It interrupts: nothing else should be speaking yet.</summary>
    public static Announcement For(ModStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var text = status.State == ModState.Ready ? Loaded
            : status.Reason == ModStartup.UnknownGameBuildReason ? Unsupported
            : CouldNotStart;
        return new(text, Interrupt: true);
    }
}
