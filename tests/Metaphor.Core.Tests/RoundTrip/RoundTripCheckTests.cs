using CsCheck;
using Metaphor.Core.RoundTrip;

namespace Metaphor.Core.Tests.RoundTrip;

[Trait("Category", "Unit")]
public sealed class RoundTripCheckTests
{
    [Fact]
    public void AnyToyFileRoundTripsByteForByte() =>
        ToyFile.Gen.Sample(file =>
        {
            var codec = new ToyCodec();

            var result = RoundTripCheck.Run(codec, codec.Write(file));

            Assert.Equal(RoundTripOutcome.ByteIdentical, result.Outcome);
            Assert.True(result.Passed);
        });

    [Fact]
    public void ALossyCodecFailsAndNamesTheFirstDifference() =>
        ToyFile.Gen.Where(file => file.Flags != 0).Sample(file =>
        {
            var original = new ToyCodec().Write(file);

            var result = RoundTripCheck.Run(new LossyToyCodec(), original);

            Assert.Equal(RoundTripOutcome.Different, result.Outcome);
            Assert.False(result.Passed);
            Assert.StartsWith($"first difference at offset 0x4: original 0x{file.Flags:X2}, written 0x00", result.Detail);
        });

    [Fact]
    public void AnExemptFormatPassesWhenOnlyMeaningSurvives() =>
        ToyFile.Gen.Where(file => file.Flags != 0).Sample(file =>
        {
            var codec = new ExemptLossyToyCodec();

            var result = RoundTripCheck.Run(codec, new ToyCodec().Write(file));

            Assert.Equal(RoundTripOutcome.SameMeaning, result.Outcome);
            Assert.True(result.Passed);
            Assert.EndsWith($"exempt: {codec.Exemption.Reason}", result.Detail);
        });

    [Fact]
    public void BytesTheModelDoesNotHoldFailAByteExactFormat()
    {
        // Declares one 3-byte entry but carries 4 bytes: the trailing byte is lost on reading.
        byte[] original = [.. "TOY1"u8, 0, 1, 0, 3, 1, 2, 3, 4];

        var result = RoundTripCheck.Run(new ToyCodec(), original);

        Assert.Equal(RoundTripOutcome.Different, result.Outcome);
        Assert.Equal("one file is a prefix of the other (original 12 bytes, written 11 bytes)", result.Detail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnExemptionRequiresAReason(string reason) =>
        Assert.ThrowsAny<ArgumentException>(() => new RoundTripExemption<ToyFile>(reason, ToyFile.SameEntries));

    [Fact]
    public void AnExemptionRejectsANullReason() =>
        Assert.Throws<ArgumentNullException>(() => new RoundTripExemption<ToyFile>(null!, ToyFile.SameEntries));
}
