#if DEBUG
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Metaphor.Core;
using Metaphor.Core.Inspector;

namespace Metaphor.Hooks;

/// <summary>The live inspector (#291): in a Debug build with <see cref="OptInFile"/> in the mod's folder, a named pipe on
/// which an agent asks the running game read-only questions (<see cref="InspectorCommands"/>), one client at a time.
/// Every read goes through <see cref="ModuleMemoryReader"/>; nothing is written, called or hooked. The pipe admits only
/// the user running the game, and never over the network. The whole type is compiled out of Release builds, which an
/// architecture test checks. Usage: docs/contributors/how-to/inspect-the-running-game.md.</summary>
internal sealed class LiveInspector
{
    /// <summary>The opt-in file, named bare so it is looked for only in the mod's folder.</summary>
    public const string OptInFile = "opt-in-inspector.txt";

    /// <summary>The pipe's name, <c>\\.\pipe\metaphor-accessibility-inspector</c>; scripts/inspect.ps1 connects to it.</summary>
    public const string PipeName = "metaphor-accessibility-inspector";

    private readonly NamedPipeServerStream pipe;
    // One set of commands for every client, so a watch set by one run of inspect.ps1 reports its changes to the next.
    private readonly InspectorCommands commands;
    private readonly Action<string> log;
    private readonly Action<string> logError;

    private LiveInspector(NamedPipeServerStream pipe, nint moduleBase, long moduleSize, Action<string> log, Action<string> logError)
    {
        this.pipe = pipe;
        commands = new InspectorCommands(moduleBase, moduleSize,
            (address, buffer) => ModuleMemoryReader.ReadBytes((nint)address, buffer));
        this.log = log;
        this.logError = logError;
    }

    /// <summary>Starts the inspector on a background thread when <see cref="OptInFile"/> is in <paramref name="folder"/>;
    /// null otherwise or when the pipe can't be created. Never throws. Logs only that it listens, a client's arrival and
    /// departure and how many requests it made: never addresses, bytes or class names.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A development tool must never stop the mod.")]
    [SuppressMessage("Reliability", "CA2000", Justification = "The pipe lives as long as the game process.")]
    public static LiveInspector? StartIfOptedIn(string folder, Action<string> log, Action<string> logError)
    {
        if (HookOptIn.Decide(devOnly: true, OptInFile, devBuild: true, name => File.Exists(Path.Combine(folder, name))) !=
            HookInclusion.Included)
        {
            return null;
        }
        try
        {
            using var process = Process.GetCurrentProcess();
            var size = process.MainModule?.ModuleMemorySize ?? throw new InvalidOperationException("the game process has no main module");
            var inspector = new LiveInspector(CreatePipe(), ModuleMemoryReader.MainModuleBase(), size, log, logError);
            new Thread(inspector.Run) { IsBackground = true, Name = "metaphor live inspector" }.Start();
            log($"live inspector: listening on \\\\.\\pipe\\{PipeName} ({OptInFile})");
            return inspector;
        }
        catch (Exception exception)
        {
            logError($"live inspector didn't start: {exception.GetType().Name}: {exception.Message}");
            return null;
        }
    }

    /// <summary>One instance, so a second process can't open another end under the same name while this one listens.
    /// Its access list grants read and write to the user running the game and denies the network logon group, so no
    /// other user and no remote client can connect; the owner is set to that user too, which a client that checks the
    /// server's owner (<c>PipeOptions.CurrentUserOnly</c>, as scripts/inspect.ps1 does) needs.
    /// https://learn.microsoft.com/dotnet/api/system.io.pipes.namedpipeserverstreamacl.create and
    /// https://learn.microsoft.com/windows/win32/ipc/named-pipe-security-and-access-rights</summary>
    private static NamedPipeServerStream CreatePipe()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new InvalidOperationException("the game's user has no security identifier");
        var security = new PipeSecurity();
        security.SetOwner(user);
        security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        return NamedPipeServerStreamAcl.Create(
            PipeName, PipeDirection.InOut, maxNumberOfServerInstances: 1, PipeTransmissionMode.Byte, PipeOptions.None,
            inBufferSize: 0, outBufferSize: 0, security);
    }

    /// <summary>Serves one client after another, on this thread only, until the game exits. A client that connects and
    /// sends nothing holds the pipe until it closes; only the game's own user can connect at all.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "An exception on this thread would end the game process.")]
    private void Run()
    {
        try
        {
            while (true)
            {
                pipe.WaitForConnection();
                Write(log, "live inspector: client connected");
                var served = 0;
                try
                {
                    served = InspectorSession.Serve(pipe, commands.Respond);
                }
                catch (IOException)
                {
                    // The client went away mid-request; the next one is served as usual.
                }
                Write(log, $"live inspector: client left after {served} requests");
                // Frees the instance for the next client, whether this one closed its end or broke it.
                pipe.Disconnect();
            }
        }
        catch (Exception exception)
        {
            Write(logError, $"live inspector stopped: {exception.GetType().Name}: {exception.Message}");
        }
    }

    /// <summary>Writes <paramref name="line"/> through <paramref name="to"/>; a log that throws on this thread must not
    /// end the game.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "An exception on this thread would end the game process.")]
    private static void Write(Action<string> to, string line)
    {
        try
        {
            to(line);
        }
        catch (Exception)
        {
            // Nowhere left to report it.
        }
    }
}
#endif
