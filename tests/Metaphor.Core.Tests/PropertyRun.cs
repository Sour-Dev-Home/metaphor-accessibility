using System.Globalization;
using FsCheck;
using FsCheck.Fluent;
using Microsoft.FSharp.Core;

namespace Metaphor.Core.Tests;

/// <summary>Runs the FsCheck properties of the in-game parsers (ADR 0015, #443). Every check runs <see cref="Cases"/>
/// cases from <see cref="FixedSeed"/>, so a run is repeatable. The weekly slow suite sets
/// <c>METAPHOR_PROPERTY_CASES</c> (10,000 there) and each property then takes a fresh seed; <c>METAPHOR_PROPERTY_SEED</c>
/// replays one. A failure names its seed either way. The property test classes carry the <c>Property</c> category beside
/// <c>Unit</c>, so the slow suite can pick them alone and every check and Stryker still run them.</summary>
internal static class PropertyRun
{
    /// <summary>The cases per property in a normal run.</summary>
    public const int Cases = 100;

    /// <summary>The seed of a normal run.</summary>
    public const ulong FixedSeed = 443;

    // FsCheck's split generator needs an odd gamma; any fixed odd number keeps a seed replayable.
    private const ulong Gamma = 0x9E3779B97F4A7C15;

    /// <summary>Checks <paramref name="property"/>, throwing with its seed when a case fails.</summary>
    public static void Check(Property property)
    {
        var slowCases = Number("METAPHOR_PROPERTY_CASES") is { } wanted and > 0 and <= int.MaxValue ? (int?)wanted : null;
        var cases = slowCases ?? Cases;
        var seed = Number("METAPHOR_PROPERTY_SEED") ?? (slowCases is null ? FixedSeed : (ulong)Random.Shared.NextInt64());
        var config = Config.QuickThrowOnFailure
            .WithMaxTest(cases)
            .WithQuietOnSuccess(true)
            .WithReplay(FSharpOption<FsCheck.Replay>.Some(new FsCheck.Replay(new Rnd(seed, Gamma), FSharpOption<int>.None)));
        try
        {
            property.Check(config);
        }
        catch (Exception e)
        {
            throw new PropertyFailedException(
                $"property failed with METAPHOR_PROPERTY_SEED={seed.ToString(CultureInfo.InvariantCulture)} " +
                $"METAPHOR_PROPERTY_CASES={cases.ToString(CultureInfo.InvariantCulture)}; set both to replay this run", e);
        }
    }

    /// <summary>Strings from <paramref name="pieces"/>, joined, up to the run's size in pieces.</summary>
    public static Gen<string> Joined(Gen<string> pieces) => Gen.ListOf(pieces).Select(list => string.Concat(list));

    /// <summary>Any UTF-16 code unit, weighted toward the awkward ones: NUL, lone high and low surrogates, C0 and C1
    /// controls, the byte-order mark and non-ASCII text.</summary>
    public static Gen<string> AnyChar { get; } = Gen.Frequency(
        (3, Gen.Constant(0)),
        (2, Gen.Choose(0xD800, 0xDBFF)),
        (2, Gen.Choose(0xDC00, 0xDFFF)),
        (2, Gen.Choose(0x01, 0x1F)),
        (1, Gen.Choose(0x7F, 0x9F)),
        (1, Gen.Constant(0xFEFF)),
        (2, Gen.Choose(0xA0, 0x2FFF)),
        (3, Gen.Choose(0, char.MaxValue))).Select(c => ((char)c).ToString());

    /// <summary>One string of <paramref name="length"/> pieces, past what the run's size generates.</summary>
    public static Gen<string> Long(Gen<string> pieces, int length) =>
        Gen.ArrayOf(pieces, length).Select(list => string.Concat(list));

    private static ulong? Number(string name) =>
        ulong.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}

/// <summary>A property that failed, with the seed that replays it.</summary>
public sealed class PropertyFailedException(string message, Exception inner) : Exception(message, inner);
