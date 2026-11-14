using System.Runtime.CompilerServices;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Metaphor.Core.Tests.RoundTrip;

/// <summary>A test that needs game data. It carries the <c>RoundTrip</c> category, so CI's filter always picks it up; it
/// is skipped, not passed, when there is no data, and otherwise names the game build of the data it ran on.</summary>
[AttributeUsage(AttributeTargets.Method)]
[TraitDiscoverer("Metaphor.Core.Tests.RoundTrip.RoundTripTraitDiscoverer", "Metaphor.Core.Tests")]
internal sealed class RoundTripFactAttribute : FactAttribute, ITraitAttribute
{
    public RoundTripFactAttribute([CallerMemberName] string testName = "")
    {
        if (GameData.ThisRun.Data is { } data)
        {
            DisplayName = $"{testName} (game build {data.Build})";
        }
        else
        {
            Skip = GameData.ThisRun.MissingReason;
        }
    }
}

/// <summary>Gives every <see cref="RoundTripFactAttribute"/> test the <c>RoundTrip</c> category.</summary>
public sealed class RoundTripTraitDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute) =>
        [new("Category", "RoundTrip")];
}
