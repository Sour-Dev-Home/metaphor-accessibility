using System.Text.RegularExpressions;

namespace Metaphor.Core.Logging;

/// <summary>Cleans a line before it reaches a log a player may share (#204, #824): the folders and user name it was made
/// with, each name the player typed that the mod has read since, and any raw memory dump. Safe to use from several
/// threads; never throws for a line it's given.</summary>
// Idea credit: op1-access (Timtam, MIT, https://github.com/Timtam/op1-access at cb5d1ff, README.md) scrubs the Windows
// user name from logs before they're sent. Idea only, no code copied; see CREDITS.md.
public sealed class LogScrubber
{
    /// <summary>What a redacted folder or user name becomes.</summary>
    public const string Redacted = "<redacted>";

    /// <summary>What a name the player typed becomes.</summary>
    public const string Name = "<name>";

    /// <summary>The most typed names kept; the game has two (<see cref="Messages.TypedName"/>), and a few more allow for
    /// the player renaming one.</summary>
    private const int MaxNames = 16;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private static readonly char[] Separators = ['\\', '/'];

    private const string ShortName = @"[^\\/\s~]{1,7}~[0-9]+(?:\.[^\\/\s.]{1,3})?";

    /// <summary>The fewest bytes written as hex pairs with a separator that count as a memory dump (#843).</summary>
    private const int MinSeparatedBytes = 8;

    /// <summary>The fewest hex digits in an unseparated run that can be a memory dump: 17 bytes. A run of up to 32 digits
    /// stays, since a message label's MD5 is 32 and the logs name those.</summary>
    private const int MinRunDigits = 34;

    /// <summary>The longest unseparated run that is judged by its length; a longer one is always a dump.</summary>
    private const int Sha256Digits = 64;

    /// <summary>Eight or more bytes written as hex pairs with a separator, as a memory dump prints them, or an
    /// unseparated hex run of 34 digits or more (<see cref="IsDump"/> says which of those it is), a candidate for a
    /// dump of 17 bytes or more.</summary>
    private static readonly Regex Dump = new(
        $@"(?<![0-9A-Fa-f])(?:[0-9A-Fa-f]{{2}}(?:[ ,:\-][0-9A-Fa-f]{{2}}){{{MinSeparatedBytes - 1},}}|[0-9A-Fa-f]{{{MinRunDigits},}})(?![0-9A-Fa-f])",
        RegexOptions.CultureInvariant, MatchTimeout);

    /// <summary>Whether a hex run the pattern found is a dump: any separated one, and an unseparated one of an even number
    /// of digits other than a commit's 40 and a SHA-256's 64, or any longer than that.</summary>
    private static bool IsDump(string run, int digits) =>
        run.Any(c => !char.IsAsciiHexDigit(c)) || digits > Sha256Digits || (digits % 2 == 0 && digits is not (40 or 64));

    private readonly Regex[] redacted;
    private readonly Lock gate = new();
    private readonly HashSet<string> known = new(StringComparer.OrdinalIgnoreCase);
    private volatile Regex[] names = [];

    /// <param name="redact">Folders and the user name, as <see cref="PathsToRedact"/> lists them.</param>
    public LogScrubber(IEnumerable<string> redact)
    {
        ArgumentNullException.ThrowIfNull(redact);
        redacted = [.. redact.Where(s => !string.IsNullOrEmpty(s)).OrderByDescending(s => s.Length).Select(RedactionPattern)];
    }

    /// <summary>A scrubber that only removes memory dumps and typed names, for tests and a log with no folders.</summary>
    public static LogScrubber None() => new([]);

    /// <summary>Adds a name the player typed (the protagonist's, or their own at the guide prompt), so every later line
    /// says <see cref="Name"/> for it. Matched as a whole word in any case; blank names are ignored. A name is replaced
    /// only after the mod has read it in this session, and only as a whole word: a line written before the mod read it,
    /// or a name inside a longer word, keeps it (#843).</summary>
    public void AddName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return;
        }
        lock (gate)
        {
            if (known.Count >= MaxNames || !known.Add(trimmed))
            {
                return;
            }
            var pattern = new Regex(
                $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(trimmed)}(?![\p{{L}}\p{{N}}_])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
            names = [.. names, pattern];
        }
    }

    /// <summary><paramref name="line"/> with every redacted folder and user name, typed name and memory dump replaced.
    /// A line whose cleaning times out is withheld whole, as it may still hold what it was to lose.</summary>
    public string Scrub(string? line)
    {
        try
        {
            var text = redacted.Aggregate(line ?? "", (current, pattern) => pattern.Replace(current, Redacted));
            text = names.Aggregate(text, (current, pattern) => pattern.Replace(current, Name));
            return Dump.Replace(text, match =>
            {
                var digits = match.Value.Count(char.IsAsciiHexDigit);
                return IsDump(match.Value, digits)
                    ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"<memory dump, {digits / 2} bytes>")
                    : match.Value;
            });
        }
        catch (RegexMatchTimeoutException)
        {
            return "(line withheld: it couldn't be cleaned in time)";
        }
    }

    /// <summary>What a report log redacts, since a path to any of these can turn up in an error: the mod's folder, its
    /// settings folder, the game's install folder (#204), the user profile and the user name. A user name under three
    /// characters isn't redacted on its own, as replacing it everywhere would garble the log.</summary>
    public static IReadOnlyList<string> PathsToRedact(
        string modFolder, string configFolder, string? gameExecutable, string userProfile, string userName) =>
        [.. new[] { modFolder, configFolder, FolderOf(gameExecutable), userProfile, userName.Length >= 3 ? userName : null }
            .OfType<string>().Where(s => s.Length > 0)];

    /// <summary>The folder part of a Windows path, split by hand so it reads the same on any OS the tests run on. Null
    /// for a bare drive such as <c>F:</c>, which names no one and would garble every mention of the drive.</summary>
    private static string? FolderOf(string? path) =>
        path?.LastIndexOfAny(Separators) is > 2 and var end ? path[..end] : null;

    /// <summary>Matches a redacted string however a path in it may be written (#204): with either slash, or with
    /// doubled ones as an escaped string shows them, and with any folder after the first in an 8.3 short form, since
    /// Windows may pass a path along that way. A short form is matched by shape (up to seven characters, a tilde and a
    /// number, as in <c>SOMEON~1</c> or the hashed <c>SO1A2B~1</c>), so a few unrelated short paths may be redacted
    /// too; that errs the safe way.</summary>
    private static Regex RedactionPattern(string text)
    {
        var parts = text.Split(Separators, StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape).ToArray();
        var pattern = parts.Length <= 1
            ? Regex.Escape(text)
            : string.Join(@"[\\/]+", parts.Take(1).Concat(parts.Skip(1).Select(part => $"(?:{part}|{ShortName})")));
        return new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
    }
}
