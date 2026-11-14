using Metaphor.Core;

namespace Metaphor.Hooks;

/// <summary>Identifies this build of the mod at runtime. The status file (#16) prints it next to the game build.</summary>
public static class ModBuild
{
    /// <summary>The commit this mod was built from, and whether the build had uncommitted changes.</summary>
    public static SourceRevision Revision { get; } = SourceRevision.Of(typeof(ModBuild).Assembly);
}
