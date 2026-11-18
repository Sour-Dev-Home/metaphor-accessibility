namespace Metaphor.Core;

/// <summary>A game global the loader locates on its own (#300): from the instruction in the game's code that loads it,
/// a RIP-relative <c>[rip+disp32]</c> load, found by its signature in <c>hook-signatures.json</c>. It is only read, never
/// hooked or called, and goes through the same build check and exactly-once match as a hook, so a global that doesn't
/// match turns off only what reads it.</summary>
/// <param name="Name">The global in plain words, as the load message says it.</param>
/// <param name="GameBuilds">The SHA-256 of each game build its load was checked on.</param>
/// <param name="SignatureName">The signature that finds the code holding the load.</param>
/// <param name="LoadOffset">Where the load instruction starts, in bytes from the signature's match.</param>
/// <param name="DisplacementOffset">Where its <c>disp32</c> sits, in bytes from the instruction's start.</param>
/// <param name="InstructionLength">The load instruction's length in bytes: RIP is its end.</param>
/// <param name="TurnsOff">What a missing global turns off, in plain words, as the load message says it.</param>
public sealed record GlobalSpec(
    string Name,
    IReadOnlyList<string> GameBuilds,
    string SignatureName,
    int LoadOffset,
    int DisplacementOffset,
    int InstructionLength,
    string TurnsOff)
{
    /// <summary>The global's RVA, from its signature's match at <paramref name="matchRva"/>: the end of the load
    /// instruction plus its signed displacement, which <paramref name="readInt32"/> reads at an RVA. Null when that
    /// read fails. The one place a RIP-relative load is decoded.</summary>
    public long? Locate(long matchRva, Func<long, int?> readInt32)
    {
        ArgumentNullException.ThrowIfNull(readInt32);
        var load = matchRva + LoadOffset;
        return readInt32(load + DisplacementOffset) is { } displacement ? load + InstructionLength + displacement : null;
    }
}

/// <summary>Where one game global is, as an RVA, or null when it wasn't located, and why.</summary>
public sealed record GlobalDecision(string Name, long? Rva, string? WhyNot)
{
    public bool Found => WhyNot is null;
}

/// <summary>A global that wasn't located, and what that turns off, for the load message (#300).</summary>
public sealed record MissingGlobal(string Name, string TurnsOff);

/// <summary>The globals <see cref="HookInstallation"/> locates with the hooks: each spec, how to read the game's code at
/// an RVA, and who takes each decision.</summary>
public sealed record GlobalLookup(
    IReadOnlyList<GlobalSpec> Globals, Func<long, int?> ReadInt32, Action<GlobalDecision> Use);

/// <summary>What the loader decided: one decision per hook and one per global, in the order given.</summary>
public sealed record LoadPlan(IReadOnlyList<HookDecision> Hooks, IReadOnlyList<GlobalDecision> Globals);
