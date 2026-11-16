using System.Text.Json;
using System.Text.Json.Serialization;

namespace Metaphor.Core;

/// <summary>The shared shape of our embedded JSON lists (known builds, hook signatures, probes): strict parsing, a check
/// per entry, and no key listed twice.</summary>
internal static class JsonList
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Parses <paramref name="json"/> as a list, runs <paramref name="validate"/> on each entry and rejects a
    /// repeated <paramref name="key"/>. <paramref name="file"/> and <paramref name="entry"/> name the file and an entry
    /// in messages: "known-builds file", "build".</summary>
    /// <exception cref="InvalidDataException">The JSON is malformed, an entry is empty or invalid, or a key repeats.</exception>
    public static List<T> Parse<T>(string json, string file, string entry, Action<T> validate, Func<T, string> key)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(json);

        List<T?> items;
        try
        {
            items = JsonSerializer.Deserialize<List<T?>>(json, Options) ?? [];
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{file} is malformed: {e.Message}", e);
        }

        var valid = new List<T>(items.Count);
        foreach (var item in items)
        {
            if (item is null)
            {
                throw new InvalidDataException($"{file} has an empty entry");
            }
            validate(item);
            valid.Add(item);
        }

        var duplicate = valid.GroupBy(key).FirstOrDefault(group => group.Count() > 1);
        return duplicate is null ? valid : throw new InvalidDataException($"{entry} {duplicate.Key} is listed twice");
    }

    /// <summary>Checks an entry's name and its <see cref="BytePattern"/>, for the signature lists' checks.
    /// <paramref name="entry"/> says what the entry is in messages: "signature".</summary>
    /// <exception cref="InvalidDataException">The name is blank or the pattern doesn't parse.</exception>
    public static void CheckNameAndPattern(string entry, string? name, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidDataException($"a {entry} has no name");
        }
        try
        {
            BytePattern.Parse(pattern ?? "");
        }
        catch (FormatException e)
        {
            throw new InvalidDataException($"{entry} {name} has a bad pattern: {e.Message}", e);
        }
    }

    /// <summary>Reads one of Metaphor.Core's embedded resources.</summary>
    public static string ReadResource(string name)
    {
        using var stream = typeof(JsonList).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"missing embedded resource {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
