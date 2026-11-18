namespace Metaphor.Core.Announcements;

/// <summary>What the player hears when the mod starts: whether it is running (#187). A blind player can't read the
/// status file, so this is how they learn the mod is off.</summary>
public static class LoadMessage
{
    public const string Loaded = "Metaphor accessibility loaded";

    public const string Unsupported = "This game version isn't supported yet; the mod is off";

    /// <summary>Any other refusal, such as the hook loader failing on a supported build.</summary>
    public const string CouldNotStart = "Metaphor accessibility couldn't start; the mod is off";

    /// <summary>The announcement for <paramref name="status"/>. It interrupts: nothing else should be speaking yet.
    /// When the mod loaded, it names each dev opt-in the run asked for, and any whose hooks didn't all go in as not
    /// active, so the maintainer hears the setup before playing (#223), and each game global that wasn't found, with
    /// what that turns off (#300).</summary>
    public static Announcement For(ModStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var text = status.State == ModState.Ready ? Loaded + OptIns(status.OptIns) + Missing(status.MissingGlobals)
            : status.Reason == ModStartup.UnknownGameBuildReason ? Unsupported
            : CouldNotStart;
        return new(text, Interrupt: true);
    }

    private static string OptIns(IReadOnlyList<OptInState> optIns) =>
        optIns.Count == 0 ? ""
            : ", dev opt-ins: " + string.Join(", ", optIns.Select(o => o.Active ? o.Name : $"{o.Name} not active"));

    private static string Missing(IReadOnlyList<MissingGlobal> missing) =>
        missing.Count == 0 ? ""
            : ", " + string.Join("; ", missing.Select(m => $"{m.Name} not found, so {m.TurnsOff} are off"));
}
