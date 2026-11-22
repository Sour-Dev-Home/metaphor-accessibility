using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Metaphor.Installer.Tests;

/// <summary>A release payload built in memory, as scripts/release/installer-exe.ps1 builds the real one: a stored zip
/// and a sha256sum-style list of its files.</summary>
internal sealed class TestPayload
{
    public static readonly string[] Names =
    [
        "Install Metaphor accessibility.cmd",
        "metaphor.accessibility/ModConfig.json",
        "metaphor.accessibility/install.ps1",
        "metaphor.accessibility/prism.dll",
        "metaphor.accessibility/licenses/prism/LICENSE",
    ];

    public Dictionary<string, byte[]> Files { get; } = Names.ToDictionary(name => name, name => Encoding.UTF8.GetBytes("content of " + name));

    /// <summary>The manifest of <see cref="Files"/>, or of <paramref name="files"/> when given.</summary>
    public string Manifest(IDictionary<string, byte[]>? files = null) =>
        string.Concat((files ?? Files).OrderBy(file => file.Key, StringComparer.Ordinal)
            .Select(file => $"{Sha256(file.Value)}  {file.Key}\n"));

    public PayloadManifest ParsedManifest() => PayloadManifest.Parse(Manifest());

    /// <summary>A zip of <see cref="Files"/>, or of <paramref name="entries"/> when given.</summary>
    public MemoryStream Zip(IEnumerable<KeyValuePair<string, byte[]>>? entries = null)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries ?? Files)
            {
                using var target = zip.CreateEntry(entry.Key, CompressionLevel.NoCompression).Open();
                target.Write(entry.Value);
            }
        }
        stream.Position = 0;
        return stream;
    }

    public static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
