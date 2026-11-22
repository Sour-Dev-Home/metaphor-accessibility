using System.Collections.Frozen;

namespace Metaphor.Core.Settings;

/// <summary>One cue's own setting (#661): whether it plays, and its volume from 0 to 100 as a share of the sound cue
/// volume.</summary>
public readonly record struct CueSetting(bool On, int Volume)
{
    /// <summary>On, at the full sound cue volume.</summary>
    public static CueSetting Default { get; } = new(true, 100);
}

/// <summary>The values a settings file gives for keys it names, compared by content, so two reads of one file are
/// equal settings.</summary>
internal sealed class SettingMap<TKey, TValue> : IEquatable<SettingMap<TKey, TValue>>
    where TKey : notnull
{
    private readonly FrozenDictionary<TKey, TValue> values;

    public SettingMap(IDictionary<TKey, TValue> values) =>
        this.values = values.ToFrozenDictionary(values is Dictionary<TKey, TValue> d ? d.Comparer : null);

    public static SettingMap<TKey, TValue> Empty { get; } = new(new Dictionary<TKey, TValue>());

    /// <summary>The file's value for <paramref name="key"/>, or <paramref name="fallback"/>.</summary>
    public TValue Get(TKey key, TValue fallback) => values.TryGetValue(key, out var value) ? value : fallback;

    public bool Equals(SettingMap<TKey, TValue>? other) =>
        other is not null && values.Count == other.values.Count && values.All(pair =>
            other.values.TryGetValue(pair.Key, out var value) && EqualityComparer<TValue>.Default.Equals(pair.Value, value));

    public override bool Equals(object? obj) => Equals(obj as SettingMap<TKey, TValue>);

    public override int GetHashCode() => values.Count;
}
