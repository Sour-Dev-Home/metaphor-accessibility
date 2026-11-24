namespace Metaphor.Core.Settings;

/// <summary>A limit in the settings file's <c>limits</c> object made for a slider (#835): <see cref="Name"/> in
/// <see cref="Group"/>, its <see cref="Default"/>, and the range and step the settings menu (#754) moves it by. A
/// hand-edited file may set any whole number; it is kept within the range. A feature module lists its sliders
/// (<see cref="Features.IFeatureModule.Limits"/>), so the settings reference and the settings menu both come from
/// them.</summary>
public sealed record SliderLimit(string Group, string Name, int Default, int Min, int Max, int Step)
{
    /// <summary>What the settings menu calls it.</summary>
    public string Spoken { get; init; } = "";

    /// <summary>What it does, for players: its row in <c>docs/players/reference/settings.md</c>, before its range.
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>Where its row sits among the limits, smallest first.</summary>
    public int Order { get; init; }

    /// <summary><c>group.name</c>, as the settings reference names it.</summary>
    public string Path => $"{Group}.{Name}";

    /// <summary>The value <paramref name="settings"/> give it, within the range; the default when they give none.</summary>
    public int From(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Math.Clamp(settings.Limit(Group, Name, Default), Min, Max);
    }
}
