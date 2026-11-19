using FsCheck;
using FsCheck.Fluent;
using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests.Messages;

/// <summary>Properties of <see cref="MessageLibrary"/> (ADR 0015): any source, however broken, gives messages or none
/// without throwing, each no bigger than the source; and messages written in the logged shape
/// (docs/raw-sources/message-listening-run-2026-11-16.md) read back as written.</summary>
[Trait("Category", "Unit")]
[Trait("Category", "Property")]
public sealed class MessageLibraryPropertyTests
{
    public sealed record Written(string Label, string? Speaker, string Text);

    // The format's own pieces, so broken sources come close to real ones, and any character besides.
    private static readonly Gen<string> Pieces = Gen.Frequency(
        (6, Gen.Elements("@", "@MES_a", "@MES_b ", "{", "}", "\n", "\r\n", " ", "\t", "<NS>", "<NE>", "Ada", "line",
            "#SPEAKER_ID \"5\"", "<WAIT>", "<", ">")),
        (1, PropertyRun.AnyChar));

    // Now and then a source of thousands of pieces, as a whole game file is.
    private static readonly Arbitrary<string> Sources =
        Gen.Frequency((19, PropertyRun.Joined(Pieces)), (1, PropertyRun.Long(Pieces, 20_000))).ToArbitrary();

    [Fact]
    public void AnySourceGivesBoundedMessagesOrNone() =>
        PropertyRun.Check(Prop.ForAll(Sources, source =>
        {
            var library = new MessageLibrary();
            library.Compiled(source);

            foreach (var label in Labels(source))
            {
                if (library.Find(label) is not { } message)
                {
                    continue;
                }
                Assert.Equal(label, message.Label);
                Assert.True(message.Text.Length <= source.Length);
                Assert.Equal(message.Text.Trim(), message.Text);
                Assert.True(message.Speaker is null || (message.Speaker.Length > 0 && message.Speaker == message.Speaker.Trim()));
            }
        }));

    private static readonly Gen<string> Words =
        Gen.Elements("Ada", "Will", "the", "bridge", "<VOICE \"v_1\">", "<WAIT>", "é", "{", "}x", "@not", ",", ".");

    private static readonly Gen<Written> Messages =
        from label in Gen.NonEmptyListOf(Gen.Elements("MES", "_", "a", "Z", "0", "9")).Select(p => string.Concat(p))
        from speaker in Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.NonEmptyListOf(Gen.Elements("Ada", "Will", " ")).Select(p => (string?)string.Concat(p).Trim()))
        from text in PropertyRun.Joined(Gen.Frequency((8, Words), (2, Gen.Constant(" ")), (1, Gen.Constant("\n"))))
        select new Written(label, string.IsNullOrEmpty(speaker) ? null : speaker, text);

    [Fact]
    public void MessagesInTheLoggedShapeReadBackAsWritten() =>
        PropertyRun.Check(Prop.ForAll(Gen.ListOf(Messages).ToArbitrary(), written =>
        {
            var source = string.Concat(written.Select(m =>
                $"#SPEAKER_ID \"1\"\n@{m.Label}\n{{\n{(m.Speaker is null ? "" : $"<NS>{m.Speaker}<NE>")}{m.Text}\n}}\n"));
            var library = new MessageLibrary();
            library.Compiled(source);

            // A label written twice keeps its later message.
            foreach (var m in written.GroupBy(m => m.Label).Select(g => g.Last()))
            {
                Assert.Equal(new GameMessage(m.Label, m.Speaker, m.Text.Trim()), library.Find(m.Label));
            }
        }));

    private static IEnumerable<string> Labels(string source) =>
        source.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith('@'))
            .Select(line => line[1..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .OfType<string>()
            .Distinct();
}
