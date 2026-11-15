namespace Metaphor.Core;

/// <summary>Writes a file in one step, so a reader polling for it never sees half of it.</summary>
internal static class AtomicFile
{
    public static void WriteAllText(string path, string contents)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, contents);
        File.Move(temporary, path, overwrite: true);
    }
}
