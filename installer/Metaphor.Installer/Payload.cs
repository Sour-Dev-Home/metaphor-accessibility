using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Metaphor.Installer;

/// <summary>
/// Unpacks the release zip the exe carries into a new folder, and checks the result against the list built into the
/// exe (#608: "the download checks itself"). Only the files the list names are written, each as a new file; no link is
/// written through or followed.
/// </summary>
internal static class Payload
{
    /// <summary>Writes each of ZIP's entries under ROOT, a new empty folder. Throws <see cref="PayloadDamagedException"/>
    /// when the zip can't be read, holds an entry the list doesn't name or one twice, or lacks one it names;
    /// <see cref="PayloadLinkException"/> when a folder on the way is a link; an <see cref="IOException"/> when a file
    /// can't be written, or is already there.</summary>
    public static void Unpack(Stream zip, PayloadManifest manifest, string root)
    {
        var written = new HashSet<string>(StringComparer.Ordinal);
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            throw new PayloadDamagedException("the payload isn't a zip");
        }
        using (archive)
        {
            foreach (var entry in archive.Entries)
            {
                var path = entry.FullName;
                if (manifest.HashOf(path) is null)
                {
                    throw new PayloadDamagedException($"the payload holds a file its list doesn't name: {path}");
                }
                if (!written.Add(path))
                {
                    throw new PayloadDamagedException($"the payload holds {path} twice");
                }
                var target = MakeParents(root, path);
                try
                {
                    using var source = entry.Open();
                    using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    source.CopyTo(output);
                }
                catch (InvalidDataException)
                {
                    throw new PayloadDamagedException($"the payload's copy of {path} can't be read");
                }
            }
        }
        if (written.Count != manifest.Count)
        {
            throw new PayloadDamagedException("the payload lacks a file its list names");
        }
    }

    /// <summary>Reads back everything under ROOT: exactly the files the list names, each with its SHA-256, and nothing
    /// else, not even an empty folder outside the files' own. Throws <see cref="PayloadDamagedException"/> on a
    /// difference and <see cref="PayloadLinkException"/> on a link, which it never follows.</summary>
    public static void Verify(string root, PayloadManifest manifest)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in manifest.Paths)
        {
            for (var slash = path.IndexOf('/'); slash >= 0; slash = path.IndexOf('/', slash + 1))
            {
                folders.Add(path.Substring(0, slash));
            }
        }
        root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var found = 0;
        var pending = new Stack<string>();
        RefuseLink(new DirectoryInfo(root));
        pending.Push(root);
        while (pending.Count > 0)
        {
            foreach (var item in new DirectoryInfo(pending.Pop()).EnumerateFileSystemInfos())
            {
                RefuseLink(item);
                var relative = item.FullName.Substring(root.Length + 1).Replace('\\', '/');
                if (item is DirectoryInfo)
                {
                    if (!folders.Contains(relative))
                    {
                        throw new PayloadDamagedException($"the unpacked files hold a folder the list doesn't name: {relative}");
                    }
                    pending.Push(item.FullName);
                    continue;
                }
                var expected = manifest.HashOf(relative)
                    ?? throw new PayloadDamagedException($"the unpacked files hold one the list doesn't name: {relative}");
                if (Sha256(item.FullName) != expected)
                {
                    throw new PayloadDamagedException($"{relative} doesn't match its SHA-256");
                }
                found++;
            }
        }
        if (found != manifest.Count)
        {
            throw new PayloadDamagedException("a file the list names is missing after unpacking");
        }
    }

    /// <summary>The lower-case hex SHA-256 of the file at PATH.</summary>
    public static string Sha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Sha256(stream);
    }

    public static string Sha256(Stream stream)
    {
        using var sha = SHA256.Create();
        var hex = new StringBuilder(64);
        foreach (var b in sha.ComputeHash(stream))
        {
            hex.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }
        return hex.ToString();
    }

    // Makes each folder on the way to PATH under ROOT, refusing one that is a link, and returns the file's full path.
    private static string MakeParents(string root, string path)
    {
        var segments = path.Split('/');
        var folder = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            folder = Path.Combine(folder, segments[i]);
            var info = new DirectoryInfo(folder);
            if (!info.Exists)
            {
                info.Create();
                info.Refresh();
            }
            RefuseLink(info);
        }
        var target = Path.GetFullPath(Path.Combine(folder, segments[segments.Length - 1]));
        if (!target.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new PayloadDamagedException($"{path} would land outside the folder");
        }
        return target;
    }

    private static void RefuseLink(FileSystemInfo item)
    {
        if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new PayloadLinkException($"a link is in the unpacked files: {item.FullName}");
        }
    }
}
