using FsCheck;
using FsCheck.Fluent;
using Metaphor.Core.Inspector;

namespace Metaphor.Core.Tests.Inspector;

/// <summary>Properties of <see cref="InspectorCommands"/> (ADR 0015): any run of requests, against any memory, gets one
/// <c>ok</c> or <c>err</c> reply each, bounded in size, and never an exception. Memory is a 0x400-byte module of random
/// bytes at <see cref="Base"/>, with a readable window that can end anywhere in it.</summary>
[Trait("Category", "Unit")]
[Trait("Category", "Property")]
public sealed class InspectorCommandsPropertyTests
{
    private const long Base = 0x140000000;
    private const int ModuleSize = 0x400;

    // The longest reply: every watch changed, each as "<id>=<hex>", or one full read.
    private const int MaxReply = 16 + (InspectorCommands.MaxWatches * (12 + (2 * InspectorCommands.MaxWatchBytes)));

    public sealed record Session(byte[] Memory, int ReadableUpTo, string[] Requests);

    private static readonly Gen<string> Addresses = Gen.OneOf(
        Gen.Choose(0, ModuleSize + 8).Select(offset => $"base+0x{offset:x}"),
        Gen.Choose(0, ModuleSize + 8).Select(offset => $"0x{Base + offset:x}"),
        Gen.Elements("0x0", "0xffffffffffffffff", "0x7fffffffffffffff", "base+0xffffffffffffffff", "0x", "base+",
            "0x10000000000000000", "-0x1", "12", "0xg"));

    private static readonly Gen<string> Numbers = Gen.OneOf(
        Gen.Choose(-2, 300).Select(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Gen.Choose(0, 300).Select(n => $"0x{n:x}"),
        Gen.Elements("4096", "4097", "0x1000", "0xffffffff", "0x8000000000000000", "-0x8", "2147483648", "+1", "1e3", ""));

    private static readonly Gen<string> Words = Gen.OneOf(Addresses, Numbers, Gen.Elements(InspectorCommands.Names.ToArray()));

    // Well-formed commands with any arguments, noise, and now and then a long line of words.
    private static readonly Gen<string> Requests = Gen.Frequency(
        (10, from name in Gen.Elements(InspectorCommands.Names.ToArray())
             from args in Gen.Choose(0, InspectorCommands.MaxPathSteps + 2).SelectMany(n => Gen.ArrayOf(Words, n))
             from spaces in Gen.Elements(" ", "  ")
             select string.Join(spaces, [name, .. args])),
        (10, PropertyRun.Joined(Gen.Frequency((4, Words), (3, Gen.Constant(" ")), (1, PropertyRun.AnyChar)))),
        (1, from name in Gen.Elements(InspectorCommands.Names.ToArray())
            from rest in PropertyRun.Long(Gen.OneOf(Words, Gen.Constant(" ")), 2_000)
            select name + " " + rest));

    private static readonly Arbitrary<Session> Sessions = (
        from memory in Gen.ArrayOf(Gen.Choose(0, 255).Select(b => (byte)b), ModuleSize)
        from readableUpTo in Gen.Choose(0, ModuleSize)
        from requests in Gen.ArrayOf(Requests)
        select new Session(memory, readableUpTo, requests)).ToArbitrary();

    [Fact]
    public void EveryRequestGetsABoundedOkOrErrReply() =>
        PropertyRun.Check(Prop.ForAll(Sessions, session =>
        {
            var commands = new InspectorCommands(Base, ModuleSize, (address, buffer) =>
            {
                if (address < Base || address - Base > session.ReadableUpTo - buffer.Length)
                {
                    return false;
                }
                session.Memory.AsSpan((int)(address - Base), buffer.Length).CopyTo(buffer);
                return true;
            });

            foreach (var request in session.Requests)
            {
                var reply = commands.Respond(request);

                Assert.True(reply is "ok" || reply.StartsWith("ok ", StringComparison.Ordinal) ||
                    reply.StartsWith("err ", StringComparison.Ordinal), reply);
                Assert.True(reply.Length <= MaxReply, $"{reply.Length} characters for: {request}");
            }
        }));
}
