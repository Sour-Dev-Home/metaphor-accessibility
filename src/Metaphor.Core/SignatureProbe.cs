namespace Metaphor.Core;

/// <summary>A signature someone else's mod uses, scanned for to compare the game on disk with the game running
/// (issue #21). Not one of our hooks.</summary>
/// <param name="Name">The name its source gives it.</param>
/// <param name="Pattern">Hex bytes with <c>??</c> wildcards, as in <see cref="BytePattern.Parse"/>.</param>
/// <param name="Source">The raw source it was copied from.</param>
public sealed record SignatureProbe(string Name, string Pattern, string Source);
