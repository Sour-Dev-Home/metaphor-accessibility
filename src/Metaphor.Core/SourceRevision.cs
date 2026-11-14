using System.Reflection;

namespace Metaphor.Core;

/// <summary>The commit a build came from, and whether the build also had uncommitted changes, so any result or report
/// can be traced to its source.</summary>
/// <param name="CommitSha">The full 40-character commit SHA, or an empty string when the build has none.</param>
/// <param name="HasUncommittedChanges">True when the working tree differed from the commit at build time.</param>
public sealed record SourceRevision(string CommitSha, bool HasUncommittedChanges)
{
    // Must match the marker Directory.Build.targets appends.
    private const string UncommittedMarker = "-dirty";
    private const int ShaLength = 40;

    private static readonly SourceRevision None = new(string.Empty, false);

    /// <summary>True when the build carries a commit SHA.</summary>
    public bool HasCommit => CommitSha.Length > 0;

    /// <summary>The revision an assembly was built from, read from its informational version.</summary>
    public static SourceRevision Of(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return FromInformationalVersion(version ?? string.Empty);
    }

    /// <summary>Reads a version such as <c>1.0.0+&lt;sha&gt;</c> or <c>1.0.0+&lt;sha&gt;-dirty</c>.</summary>
    public static SourceRevision FromInformationalVersion(string informationalVersion)
    {
        ArgumentNullException.ThrowIfNull(informationalVersion);

        var plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        var metadata = plus < 0 ? string.Empty : informationalVersion[(plus + 1)..];
        var dirty = metadata.EndsWith(UncommittedMarker, StringComparison.Ordinal);
        var sha = dirty ? metadata[..^UncommittedMarker.Length] : metadata;

        return IsFullSha(sha) ? new SourceRevision(sha, dirty) : None;
    }

    private static bool IsFullSha(string value) =>
        value.Length == ShaLength && value.All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f');
}
