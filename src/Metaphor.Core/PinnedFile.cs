using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Metaphor.Core;

/// <summary>Checks that a file is the exact one we pinned by SHA-256, such as a native library before the mod loads it
/// (ADR 0005). Like the game build check, a hash says only whether the bytes are the ones we chose.</summary>
public static class PinnedFile
{
    /// <summary>Why <paramref name="path"/> isn't the pinned file, or null when it is. Never throws: it runs in the
    /// game. Messages name the file, never its folder, which can name the user.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "Any failure must refuse rather than reach the game.")]
    public static string? WhyNot(string path, string expectedSha256)
    {
        var name = Path.GetFileName(path);
        if (!File.Exists(path))
        {
            return $"no {name}";
        }
        try
        {
            using var stream = File.OpenRead(path);
            return WhyNot(stream, name, expectedSha256);
        }
        catch (Exception e)
        {
            return $"can't read {name}: {e.GetType().Name}";
        }
    }

    /// <summary>The same check on a stream the caller opened, so it can keep the file open, and others from writing
    /// it, until it has loaded what it checked.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "Any failure must refuse rather than reach the game.")]
    public static string? WhyNot(Stream stream, string name, string expectedSha256)
    {
        try
        {
            return Convert.ToHexStringLower(SHA256.HashData(stream)) == expectedSha256 ? null : $"{name} isn't the pinned file";
        }
        catch (Exception e)
        {
            return $"can't read {name}: {e.GetType().Name}";
        }
    }
}
