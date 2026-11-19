using System.Text;

namespace Metaphor.Core.Inspector;

/// <summary>The live inspector's line protocol (#291) over one connection: reads requests a line at a time, as printable
/// ASCII ending in <c>\n</c> (a <c>\r</c> before it is dropped), and writes each response with a <c>\n</c>. A line over
/// <see cref="MaxLineBytes"/> ends the session, so a client can't make it buffer without limit.</summary>
public static class InspectorSession
{
    /// <summary>The longest request line, without its newline.</summary>
    public const int MaxLineBytes = 512;

    /// <summary>Answers each line from <paramref name="stream"/> with <paramref name="respond"/> until the stream ends or a
    /// line is too long; the number of lines answered by <paramref name="respond"/>.</summary>
    public static int Serve(Stream stream, Func<string, string> respond)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(respond);
        var served = 0;
        var line = new List<byte>(MaxLineBytes);
        var chunk = new byte[256];
        while (true)
        {
            var count = stream.Read(chunk, 0, chunk.Length);
            if (count == 0)
            {
                if (line.Count > 0)
                {
                    served += Answer(stream, line, respond);
                }
                return served;
            }
            for (var i = 0; i < count; i++)
            {
                if (chunk[i] == (byte)'\n')
                {
                    served += Answer(stream, line, respond);
                    line.Clear();
                }
                else if (line.Count == MaxLineBytes)
                {
                    Write(stream, "err line too long");
                    return served;
                }
                else
                {
                    line.Add(chunk[i]);
                }
            }
        }
    }

    /// <summary>Writes the response to <paramref name="line"/>; 1 when <paramref name="respond"/> answered it, 0 when it
    /// was refused here.</summary>
    private static int Answer(Stream stream, List<byte> line, Func<string, string> respond)
    {
        if (line.Count > 0 && line[^1] == (byte)'\r')
        {
            line.RemoveAt(line.Count - 1);
        }
        if (line.Exists(b => b is < 0x20 or > 0x7e))
        {
            Write(stream, "err not printable ASCII");
            return 0;
        }
        Write(stream, respond(Encoding.ASCII.GetString([.. line])));
        return 1;
    }

    private static void Write(Stream stream, string response)
    {
        var bytes = Encoding.ASCII.GetBytes(response + "\n");
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }
}
