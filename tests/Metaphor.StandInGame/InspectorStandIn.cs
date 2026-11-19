using System.IO.Pipes;
using Metaphor.Core.Inspector;

/// <summary>Stands in for the Debug mod's live inspector (#291) in scripts/dev/inspect.ps1's tests: the same commands and
/// line protocol over a pipe of the given name, answered from a 0x400-byte "module" at 0x140000000 that holds
/// <c>deadbeef</c> at +0x10 and, at +0x40, a pointer to +0x100. Serves one client after another, keeping watches between
/// them as the mod does, until it is killed.</summary>
internal static class InspectorStandIn
{
    public const long ModuleBase = 0x140000000;

    public static void Serve(string pipeName)
    {
        var memory = new byte[0x400];
        new byte[] { 0xde, 0xad, 0xbe, 0xef }.CopyTo(memory, 0x10);
        BitConverter.GetBytes(ModuleBase + 0x100).CopyTo(memory, 0x40);
        var commands = new InspectorCommands(ModuleBase, memory.Length, (address, buffer) =>
        {
            if (address < ModuleBase || address - ModuleBase + buffer.Length > memory.Length)
            {
                return false;
            }
            memory.AsSpan((int)(address - ModuleBase), buffer.Length).CopyTo(buffer);
            return true;
        });
        using var pipe = new NamedPipeServerStream(
            pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly);
        while (true)
        {
            pipe.WaitForConnection();
            try
            {
                InspectorSession.Serve(pipe, commands.Respond);
            }
            catch (IOException)
            {
                // The client went away mid-request.
            }
            pipe.Disconnect();
        }
    }
}
