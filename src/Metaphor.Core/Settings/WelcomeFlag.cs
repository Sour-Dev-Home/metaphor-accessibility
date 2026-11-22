using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Metaphor.Core.Settings;

/// <summary>Writes the first-launch welcome's flag (#617), <c>"welcomed": true</c>, into the settings file's text,
/// keeping everything else the player wrote, comments and layout included.</summary>
public static partial class WelcomeFlag
{
    /// <summary>The flag's key at the top of the settings file.</summary>
    public const string Key = "welcomed";

    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The file's text with the flag set, from <paramref name="json"/> (null for no file). Null when there is
    /// nothing to write: the flag is already set, or the text isn't a JSON object the mod could change safely.</summary>
    public static string? Mark(string? json)
    {
        if (json is null)
        {
            return $"{{ \"{Key}\": true }}{Environment.NewLine}";
        }
        var bytes = Encoding.UTF8.GetBytes(json);
        bool hasMembers;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            if (document.RootElement.TryGetProperty(Key, out var value))
            {
                // Only a false the player wrote is turned true; anything else already reads as welcomed.
                return value.ValueKind == JsonValueKind.False ? FalseFlag().Replace(json, $"\"{Key}\": true", 1) : null;
            }
            hasMembers = document.RootElement.EnumerateObject().Any();
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
        // The root object's opening brace, found by the reader so a brace inside a comment before it doesn't count.
        var reader = new Utf8JsonReader(bytes, ReaderOptions);
        reader.Read();
        var brace = (int)reader.TokenStartIndex + 1;
        var flag = Encoding.UTF8.GetBytes($" \"{Key}\": true{(hasMembers ? "," : " ")}");
        return Encoding.UTF8.GetString([.. bytes[..brace], .. flag, .. bytes[brace..]]);
    }

    [GeneratedRegex("\"welcomed\"\\s*:\\s*false", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex FalseFlag();
}
