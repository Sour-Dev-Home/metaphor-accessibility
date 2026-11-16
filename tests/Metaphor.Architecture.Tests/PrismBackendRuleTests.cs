namespace Metaphor.Architecture.Tests;

/// <summary>ADR 0005: the mod creates Prism backends only from its fixed list, so the three that load a DLL by bare name
/// are never initialised.</summary>
[Trait("Category", "Unit")]
public sealed class PrismBackendRuleTests
{
    private static string Naming(string expression) =>
        $$"""public static class C { public static object M() => {{expression}}; }""";

    [Theory]
    [InlineData("prism_registry_create_best")]
    [InlineData("prism_registry_acquire_best")]
    [InlineData("prism_availability_poll_resume")]
    public void NamingPrismsOwnBackendChoiceIsAViolation(string export)
    {
        var assembly = Plant.Compile(Rules.HookLayer, Naming($"\"{export}\""));

        Assert.Contains(Rules.PrismBackendsOnlyFromTheList(assembly), v => v.Contains(export));
    }

    [Theory]
    [InlineData("0x3D93C56C9E7F2A2EUL", "ZDSR")]
    [InlineData("0x344B951962E3B835UL", "PC-Talker")]
    [InlineData("0x285ABA1C16F3300FUL", "Boy PC Reader")]
    public void NamingABareNameBackendIsAViolation(string id, string reader)
    {
        var assembly = Plant.Compile(Rules.HookLayer, Naming(id));

        Assert.Contains(Rules.PrismBackendsOnlyFromTheList(assembly), v => v.Contains(reader));
    }

    [Fact]
    public void CreatingAListedBackendPasses()
    {
        var assembly = Plant.Compile(
            Rules.HookLayer, Naming("(\"prism_registry_create\", 0x89CC19C5C4AC1A56UL)"));

        Assert.Empty(Rules.PrismBackendsOnlyFromTheList(assembly));
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void RealAssembliesComply(string name) => Assert.Empty(Rules.PrismBackendsOnlyFromTheList(Scanned.Load(name)));
}
