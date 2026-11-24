using System.Text;
using System.Text.Json;

namespace Metaphor.Core.Settings;

/// <summary>Sets one value in the settings file's text (#754), the way <see cref="WelcomeFlag"/> sets its flag: only the
/// value's own characters change, or one member is added, so the player's other settings, keys the mod doesn't know,
/// comments and layout all stay as written.</summary>
public static class SettingsText
{
    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The text of <paramref name="json"/> (null for no file) with <paramref name="path"/>, object names from
    /// the top, set to <paramref name="value"/>: JSON text such as <c>true</c>, <c>50</c> or <c>"auto"</c>. A missing
    /// object on the way is added; one that is there but isn't an object is replaced. Null when the text isn't a JSON
    /// object the mod could change safely.</summary>
    public static string? Set(string? json, IReadOnlyList<string> path, string value)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(value);
        if (path.Count == 0)
        {
            throw new ArgumentException("a setting needs a name", nameof(path));
        }
        if (json is null)
        {
            return $"{{ {Member(path, 0, value)} }}{Environment.NewLine}";
        }
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
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
        var bytes = Encoding.UTF8.GetBytes(json);
        var (start, end, text) = Edit(bytes, path, value);
        return Encoding.UTF8.GetString([.. bytes[..start], .. Encoding.UTF8.GetBytes(text), .. bytes[end..]]);
    }

    // Walks down the objects named by the path: the span to replace and what goes there. Parsed above, so the reader
    // can't fail here.
    private static (int Start, int End, string Text) Edit(byte[] bytes, IReadOnlyList<string> path, string value)
    {
        var reader = new Utf8JsonReader(bytes, ReaderOptions);
        reader.Read();
        var open = (int)reader.TokenStartIndex;
        for (var depth = 0; ; depth++)
        {
            var hasMembers = false;
            var descended = false;
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                hasMembers = true;
                var named = reader.ValueTextEquals(path[depth]);
                reader.Read();
                if (!named)
                {
                    reader.Skip();
                    continue;
                }
                var start = (int)reader.TokenStartIndex;
                if (depth < path.Count - 1 && reader.TokenType == JsonTokenType.StartObject)
                {
                    open = start;
                    descended = true;
                    break;
                }
                reader.Skip();
                var end = (int)reader.BytesConsumed;
                return depth == path.Count - 1
                    ? (start, end, value)
                    : (start, end, $"{{ {Member(path, depth + 1, value)} }}");
            }
            if (!descended)
            {
                // Not there: added first in the object it belongs in, after its opening brace.
                var at = open + 1;
                return (at, at, $" {Member(path, depth, value)}{(hasMembers ? "," : " ")}");
            }
        }
    }

    // "name": value, with each object still to come around it.
    private static string Member(IReadOnlyList<string> path, int from, string value)
    {
        var text = value;
        for (var i = path.Count - 1; i > from; i--)
        {
            text = $"{{ {JsonSerializer.Serialize(path[i])}: {text} }}";
        }
        return $"{JsonSerializer.Serialize(path[from])}: {text}";
    }
}
