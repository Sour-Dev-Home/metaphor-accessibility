using System.Reflection;
using Metaphor.Core.Cues;

namespace Metaphor.Core.Settings;

/// <summary>Where a feature's on/off setting or a <see cref="Cue"/> sits in the settings file: the key
/// <paramref name="name"/> inside the object <paramref name="group"/>, said as <paramref name="spoken"/> when it
/// changes (#661). Each feature project declares its features as one enum whose members carry this (#784, ADR 0022); the
/// settings reference is generated from it, so <see cref="Description"/> and <see cref="Order"/> are its row.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class SettingKeyAttribute(string group, string name, string spoken) : Attribute
{
    /// <summary>True for a feature that merged before its by-ear check (#783): off in a normal install unless the
    /// settings file turns it on, on in a session build. Passing the check is a pull request that removes this.</summary>
    public bool Testing { get; init; }

    /// <summary>What the setting does, for players: its row in <c>docs/players/reference/settings.md</c>. A testing
    /// feature's row adds "Still being tested." itself.</summary>
    public string Description { get; init; } = "";

    /// <summary>Where the setting's row sits among the features' rows, smallest first; also the order a reload says
    /// changed settings in. Unique across every feature project.</summary>
    public int Order { get; init; }

    public string Group { get; } = group;

    public string Name { get; } = name;

    public string Spoken { get; } = spoken;
}

/// <summary>Marks a feature's class with the settings that turn it off (#661), from its project's feature enum (#784).
/// The architecture test requires it on every feature class that speaks, plays a cue or takes a key; the feature's
/// module gates the class with the same feature.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class FeatureSettingAttribute<TFeature>(params TFeature[] features) : Attribute
    where TFeature : struct, Enum
{
    public IReadOnlyList<TFeature> Features { get; } = features;
}

/// <summary>A setting's place in the file, read from its <see cref="SettingKeyAttribute"/>.</summary>
public sealed record SettingKey(string Group, string Name, string Spoken, bool Testing = false)
{
    /// <summary><c>group.name</c>, as log lines name it.</summary>
    public string Path => $"{Group}.{Name}";

    /// <summary>The setting's row text in the settings reference.</summary>
    public string Description { get; init; } = "";

    /// <summary>The setting's place among the features' rows.</summary>
    public int Order { get; init; }

    /// <summary>The key of a member of a feature enum, or of a <see cref="Cue"/>.</summary>
    public static SettingKey Of(Enum setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        var member = setting.ToString();
        var attribute = setting.GetType().GetField(member)?.GetCustomAttribute<SettingKeyAttribute>()
            ?? throw new ArgumentOutOfRangeException(nameof(setting), member, "no settings key");
        return new(attribute.Group, attribute.Name, attribute.Spoken, attribute.Testing)
        {
            Description = attribute.Description,
            Order = attribute.Order,
        };
    }
}
