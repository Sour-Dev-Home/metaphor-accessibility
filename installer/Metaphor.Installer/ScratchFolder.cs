using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Metaphor.Installer;

/// <summary>The folder the payload unpacks into: new, with a random name, in the player's own temp folder, and deleted
/// afterwards without following any link in it (the maintainer's decision on #704).</summary>
internal static class ScratchFolder
{
    private const int Tries = 3;

    /// <summary>Makes a new, empty folder in TEMP, named by NEWNAME (by default <c>metaphor-installer-</c> and 128 random
    /// bits), and returns its full path. A name already taken, by a folder, a file or a link, is never used; after
    /// three taken names it throws an <see cref="IOException"/>.</summary>
    public static string Create(string temp, Func<string>? newName = null)
    {
        newName ??= RandomName;
        for (var i = 0; i < Tries; i++)
        {
            var path = Path.Combine(Path.GetFullPath(temp), newName());
            if (Directory.Exists(path) || File.Exists(path))
            {
                continue;
            }
            var folder = Directory.CreateDirectory(path);
            if ((folder.Attributes & FileAttributes.ReparsePoint) != 0 || folder.GetFileSystemInfos().Length != 0)
            {
                throw new IOException($"the new folder {path} isn't a new, empty folder");
            }
            return folder.FullName;
        }
        throw new IOException($"no free name for a new folder in {temp}");
    }

    public static string RandomName()
    {
        var bytes = new byte[16];
        using (var random = RandomNumberGenerator.Create())
        {
            random.GetBytes(bytes);
        }
        var name = new StringBuilder("metaphor-installer-");
        foreach (var b in bytes)
        {
            name.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }
        return name.ToString();
    }

    /// <summary>Deletes PATH and everything in it. A link (a junction or symbolic link) is removed itself, never what
    /// it points at, so nothing outside the folder goes. Read-only files go too. Tries a few times, as a virus scanner
    /// may hold a new file for a moment. True when nothing is left.</summary>
    public static bool Delete(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                DeleteTree(new DirectoryInfo(path));
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (attempt == 4)
                {
                    return !Directory.Exists(path) && !File.Exists(path);
                }
                Thread.Sleep(200);
            }
        }
    }

    private static void DeleteTree(DirectoryInfo folder)
    {
        if (!folder.Exists)
        {
            return;
        }
        if ((folder.Attributes & FileAttributes.ReparsePoint) == 0)
        {
            foreach (var item in folder.GetFileSystemInfos())
            {
                if (item is DirectoryInfo sub)
                {
                    DeleteTree(sub);
                    continue;
                }
                // Only a plain file's attributes are changed: a link's could reach what it points at.
                if ((item.Attributes & FileAttributes.ReparsePoint) == 0)
                {
                    item.Attributes = FileAttributes.Normal;
                }
                item.Delete();
            }
        }
        // A link to a folder is removed as an empty folder: the link alone goes.
        folder.Delete();
    }
}
