namespace Metaphor.Core.Settings;

/// <summary>A limit in the settings file's <c>limits</c> object made for a slider (#835): <see cref="Name"/> in
/// <see cref="Group"/>, its <see cref="Default"/>, and the range and step a settings menu (#754) moves it by. A
/// hand-edited file may set any whole number; it is kept within the range.</summary>
public sealed record SliderLimit(string Group, string Name, int Default, int Min, int Max, int Step)
{
    /// <summary>The value <paramref name="settings"/> give it, within the range; the default when they give none.</summary>
    public int From(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Math.Clamp(settings.Limit(Group, Name, Default), Min, Max);
    }
}
