using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>A choice's prompt names its speaker as the screen shows it, like a line (#81): the alias the character
/// table and flags pick, in the layout of docs/findings/speaker-alias-from-chara-id-table.md. Made-up messages only.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DialogueSpeechTests
{
    private const string Messages = """
        @Name_Ada
        {
        Ada
        <WAIT>
        }

        @Name_Ada_00
        {
        Masked Woman
        <WAIT>
        }

        #SPEAKER_ID "51"
        @MES_q
        {
        <NS>Ada<NE>
        Which way?
        <WAIT>
        }

        @MES_c SEL 0
        {
        <SEL_ITEM>North

        South
        <WAIT>
        }
        """;

    private const int AliasFlag = 0x978;

    private sealed class Memory : IGameNameMemory
    {
        public byte[]? CharacterEntry(int character) => character == 51 ? Entry("Name_Ada", "Name_Ada_00") : null;

        public int? FollowerCharacter(int follower) => null;

        public bool? StoryFlag(int flag) => flag == AliasFlag;

        public byte[]? TypedName(TypedName name) => null;
    }

    // MD5 is the game's key for a label, not a security measure.
#pragma warning disable CA5351
    private static byte[] Key(string label) => MD5.HashData(Encoding.UTF8.GetBytes(label));
#pragma warning restore CA5351

    /// <summary>An entry with a default label and one alias slot under <see cref="AliasFlag"/>; the other four slots
    /// are empty.</summary>
    private static byte[] Entry(string defaultLabel, string alias)
    {
        var entry = new byte[CharacterEntry.Size];
        Key(defaultLabel).CopyTo(entry, 0);
        for (var i = 0; i < 5; i++)
        {
            var at = 0x10 + i * 0x12;
            Key(i == 0 ? alias : "").CopyTo(entry, at);
            BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(at + 0x10), (ushort)(i == 0 ? AliasFlag : 0xffff));
        }
        return entry;
    }

    [Fact]
    public void AChoicesPromptNamesTheSpeakerTheScreenShows()
    {
        var library = new MessageLibrary();
        library.Compiled(Messages);
        var history = new DialogueHistory();
        var dialogue = new DialogueAnnouncer(new RecordingAnnouncer(), new TextCleaner(TextCleanerRules.None), history);
        var speech = DialogueSpeech.Shown(library, new GameNames(new Memory(), library), dialogue);

        speech.Choices.Observe(new ChoiceRequested(0, "MES_q", "MES_c"));

        Assert.Equal(["Masked Woman: Which way?"], history.Lines());
    }
}
