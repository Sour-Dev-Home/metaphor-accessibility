using System.Text.Json;
using FsCheck;
using FsCheck.Fluent;
using Metaphor.Core.Settings;

namespace Metaphor.Core.Tests.Settings;

/// <summary>Properties of <see cref="ModSettings.Parse"/> (ADR 0015): whatever a player writes in the settings file, it
/// gives valid settings, or the default with a logged line, and never throws; and a file written with a mode reads back
/// as that mode.</summary>
[Trait("Category", "Unit")]
[Trait("Category", "Property")]
public sealed class ModSettingsPropertyTests
{
    private static readonly string[] ModeNames = Enum.GetNames<VoicedLinesMode>();

    // JSON's own pieces, the key and the mode names, so broken files come close to real ones, and any character besides.
    private static readonly Gen<string> Pieces = Gen.Frequency(
        (8, Gen.Elements("{", "}", "[", "]", ",", ":", "\"", "\"voicedLines\"", "\"nameOnly\"", "\"auto\"", "null", "true",
            "1", "-0.5e9", " ", "\n", "// note\n", "/* c */", "\\u0000", "\\", "\"x\"")),
        (1, PropertyRun.AnyChar));

    // Now and then a long file: a long run of pieces, or a valid file with a huge value and deep nesting.
    private static readonly Arbitrary<string> Files = Gen.Frequency(
        (18, PropertyRun.Joined(Pieces)),
        (1, PropertyRun.Long(Pieces, 20_000)),
        (1, Gen.Choose(0, 200).Select(depth =>
            $"{{ \"other\": {new string('[', depth)}{new string(']', depth)}, \"voicedLines\": \"{new string('a', 50_000)}\" }}")))
        .ToArbitrary();

    [Fact]
    public void AnyFileGivesValidSettingsOrTheDefaultWithALoggedLine() =>
        PropertyRun.Check(Prop.ForAll(Files, json =>
        {
            var logged = new List<string>();

            var settings = ModSettings.Parse(json, SampleSettings.Catalogue, logged.Add);

            Assert.True(Enum.IsDefined(settings.VoicedLines));
            Assert.True(logged.Count <= 1);
            if (logged.Count == 1)
            {
                Assert.Equal(ModSettings.Defaults, settings);
                Assert.True(logged[0].Length <= 512, logged[0]);
            }
        }));

    // A value for "voicedLines": a mode's name in any case, near misses, and anything else.
    private static readonly Gen<string> Values = Gen.OneOf(
        Gen.Elements(ModeNames).SelectMany(name => Gen.ArrayOf(Gen.Elements(true, false), name.Length), (name, upper) =>
            string.Concat(name.Select((c, i) => upper[i] ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c)))),
        Gen.Elements(ModeNames).SelectMany(name => Gen.Elements(" ", "\t", ",", "s", "0"), (name, extra) => name + extra),
        Gen.Elements("0", "1", "2", "3", "-1", "Auto, NameOnly", " auto", "", "loud"),
        PropertyRun.Joined(PropertyRun.AnyChar));

    [Fact]
    public void AModeReadsBackOnlyFromItsNameInAnyCase() =>
        PropertyRun.Check(Prop.ForAll(Values.ToArbitrary(), value =>
        {
            var logged = new List<string>();
            var json = JsonSerializer.Serialize(new Dictionary<string, object> { ["other"] = 1, ["voicedLines"] = value });

            var settings = ModSettings.Parse(json, SampleSettings.Catalogue, logged.Add);

            var named = ModeNames.FirstOrDefault(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase));
            if (named is null)
            {
                Assert.Equal(ModSettings.Defaults, settings);
                Assert.Single(logged);
            }
            else
            {
                Assert.Equal(Enum.Parse<VoicedLinesMode>(named), settings.VoicedLines);
                Assert.Empty(logged);
            }
        }));
}
