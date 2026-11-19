using System.Text;
using Metaphor.Core.Inspector;

namespace Metaphor.Core.Tests.Inspector;

/// <summary>The live inspector's line protocol (#291) over a stream: a request per line in, a response per line out.</summary>
[Trait("Category", "Unit")]
public sealed class InspectorSessionTests
{
    private static (string Output, int Served) Serve(string input, Func<string, string>? respond = null)
    {
        using var stream = new DuplexStream(Encoding.Latin1.GetBytes(input));
        var served = InspectorSession.Serve(stream, respond ?? (line => $"ok [{line}]"));
        return (Encoding.ASCII.GetString(stream.Written.ToArray()), served);
    }

    [Fact]
    public void EachLineGetsOneResponseLine() =>
        Assert.Equal(("ok [base]\nok [read 0x1 1]\n", 2), Serve("base\nread 0x1 1\n"));

    [Fact]
    public void ACarriageReturnBeforeTheNewlineIsDropped() =>
        Assert.Equal(("ok [base]\n", 1), Serve("base\r\n"));

    [Fact]
    public void ALastLineWithoutANewlineIsServed() =>
        Assert.Equal(("ok [base]\n", 1), Serve("base"));

    [Fact]
    public void ALineOverTheLimitEndsTheSession()
    {
        var (output, served) = Serve(new string('a', InspectorSession.MaxLineBytes + 1) + "\nbase\n");

        Assert.Equal(("err line too long\n", 0), (output, served));
    }

    [Fact]
    public void BytesOutsidePrintableAsciiAreRefusedWithoutReachingTheCommands()
    {
        var seen = new List<string>();

        var (output, _) = Serve("reéd\nbase\n", line =>
        {
            seen.Add(line);
            return "ok";
        });

        Assert.Equal("err not printable ASCII\nok\n", output);
        Assert.Equal(["base"], seen);
    }

    /// <summary>Reads from a fixed input and records what is written, as one end of a pipe does.</summary>
    private sealed class DuplexStream(byte[] input) : Stream
    {
        private readonly MemoryStream reading = new(input);

        public MemoryStream Written { get; } = new();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => reading.Read(buffer, offset, count);

        public override void Write(byte[] buffer, int offset, int count) => Written.Write(buffer, offset, count);

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                reading.Dispose();
                Written.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
