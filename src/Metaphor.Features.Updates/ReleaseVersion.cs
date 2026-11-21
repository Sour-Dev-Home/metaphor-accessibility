using System.Globalization;

namespace Metaphor.Features.Updates;

/// <summary>A release's semantic version (#576), such as the tag <c>v0.0.1-unstable.2</c>, ordered as semver.org 2.0.0
/// section 11 orders versions: by major, minor and patch, then a pre-release before its release, pre-release
/// identifiers compared one by one (numbers numerically, below words, which compare in ASCII order), and a longer list
/// after a shorter one it starts with. Build metadata (after <c>+</c>) is ignored.</summary>
public sealed class ReleaseVersion
{
    private readonly long major;
    private readonly long minor;
    private readonly long patch;
    private readonly string[] preRelease;

    private ReleaseVersion(long major, long minor, long patch, string[] preRelease, string text)
    {
        this.major = major;
        this.minor = minor;
        this.patch = patch;
        this.preRelease = preRelease;
        Text = text;
    }

    /// <summary>The version as said: without a leading <c>v</c> or build metadata.</summary>
    public string Text { get; }

    /// <summary>The version in <paramref name="text"/>, a tag's leading <c>v</c> or <c>V</c> allowed, or null when it
    /// isn't one.</summary>
    public static ReleaseVersion? Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        var rest = text[0] is 'v' or 'V' ? text[1..] : text;
        var plus = rest.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            if (!rest[(plus + 1)..].Split('.').All(IsIdentifier))
            {
                return null;
            }
            rest = rest[..plus];
        }
        var dash = rest.IndexOf('-', StringComparison.Ordinal);
        var core = (dash < 0 ? rest : rest[..dash]).Split('.');
        var preRelease = dash < 0 ? Array.Empty<string>() : rest[(dash + 1)..].Split('.');
        if (core.Length != 3 || !core.All(IsNumber) || !preRelease.All(id => IsIdentifier(id) && (!IsDigits(id) || IsNumber(id))))
        {
            return null;
        }
        // Nine digits at most for each core number: no release needs more, and they fit in a long.
        if (core.Any(part => part.Length > 9)
            || !long.TryParse(core[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !long.TryParse(core[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !long.TryParse(core[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
        {
            return null;
        }
        return new ReleaseVersion(major, minor, patch, preRelease, rest);
    }

    /// <summary>Whether this version comes after <paramref name="other"/>.</summary>
    public bool IsNewerThan(ReleaseVersion other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Compare(this, other) > 0;
    }

    /// <summary>Below zero when <paramref name="a"/> comes before <paramref name="b"/>, zero when they are the same
    /// version, above zero when it comes after.</summary>
    public static int Compare(ReleaseVersion a, ReleaseVersion b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var core = a.major != b.major ? a.major.CompareTo(b.major)
            : a.minor != b.minor ? a.minor.CompareTo(b.minor)
            : a.patch.CompareTo(b.patch);
        if (core != 0)
        {
            return core;
        }
        // A release comes after its pre-releases.
        if (a.preRelease.Length == 0 || b.preRelease.Length == 0)
        {
            return a.preRelease.Length == b.preRelease.Length ? 0 : a.preRelease.Length == 0 ? 1 : -1;
        }
        for (var i = 0; i < Math.Min(a.preRelease.Length, b.preRelease.Length); i++)
        {
            var each = CompareIdentifiers(a.preRelease[i], b.preRelease[i]);
            if (each != 0)
            {
                return each;
            }
        }
        return a.preRelease.Length.CompareTo(b.preRelease.Length);
    }

    // Numbers numerically, of any length (shorter is smaller, as they have no leading zeros), and below words.
    private static int CompareIdentifiers(string a, string b)
    {
        var (aNumber, bNumber) = (IsDigits(a), IsDigits(b));
        if (aNumber && bNumber)
        {
            return a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
        }
        if (aNumber != bNumber)
        {
            return aNumber ? -1 : 1;
        }
        return Math.Sign(string.CompareOrdinal(a, b));
    }

    // ASCII letters, digits and hyphens, at least one.
    private static bool IsIdentifier(string part) =>
        part.Length > 0 && part.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    private static bool IsDigits(string part) => part.Length > 0 && part.All(char.IsAsciiDigit);

    // Digits with no leading zero, or 0 itself.
    private static bool IsNumber(string part) => IsDigits(part) && (part.Length == 1 || part[0] != '0');
}
