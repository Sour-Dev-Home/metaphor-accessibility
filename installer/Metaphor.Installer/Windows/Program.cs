namespace Metaphor.Installer.Windows;

internal static class Program
{
    // The exe's arguments pass on to install.ps1, as install.cmd's do; players need none.
    private static int Main(string[] args)
    {
        // From here on, a library loaded by name comes from System32 alone, never from the exe's own folder.
        NativeMethods.SetDefaultDllDirectories(NativeMethods.SearchSystem32);
        using var host = new WindowsHost();
        return Launcher.Run(host, args);
    }
}
