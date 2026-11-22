using System;
using System.Runtime.InteropServices;

// Every native call searches System32 alone for its DLL, never the exe's folder (a Downloads folder may hold planted
// DLLs) or the working folder, as architecture rule 4 asks of the mod (docs/coding-standards.md).
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace Metaphor.Installer.Windows;

internal static class NativeMethods
{
    /// <summary>LOAD_LIBRARY_SEARCH_SYSTEM32.</summary>
    public const uint SearchSystem32 = 0x800;

    /// <summary>LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32: a library loaded by full path finds what
    /// it needs in its own folder and System32 only, as install.ps1 loads prism.dll (Open-Prism).</summary>
    public const uint SearchLoadFolderAndSystem32 = 0x900;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetDefaultDllDirectories(uint directoryFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadLibraryExW(string fileName, IntPtr file, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    public static extern IntPtr GetProcAddress(IntPtr module, string procName);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FreeLibrary(IntPtr module);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowW")]
    public static extern IntPtr FindWindow(string? className, string? windowName);
}
