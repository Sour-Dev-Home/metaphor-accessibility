using System.Globalization;

namespace Metaphor.Core;

/// <summary>Formatting game text for the spike logs. Public only because the hook layer's request listeners log text
/// too.</summary>
public static class LogText
{
    /// <summary>Text from the game kept on one log line: control characters, quotes and backslashes become
    /// <c>\xNN</c>.</summary>
    public static string Escaped(string text) => string.Concat((text ?? "").Select(c =>
        c < ' ' || c is '"' or '\\' ? string.Create(CultureInfo.InvariantCulture, $"\\x{(int)c:x2}") : c.ToString()));
}
