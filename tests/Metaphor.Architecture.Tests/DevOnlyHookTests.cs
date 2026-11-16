using Mono.Cecil.Cil;

namespace Metaphor.Architecture.Tests;

/// <summary>Development-only hooks (<c>[GameHook(..., DevOnly = true)]</c>, such as #55's listening hooks) never install
/// in the Release build players get: the loader's switch is true only in a Debug build of the hook layer.</summary>
[Trait("Category", "Unit")]
public sealed class DevOnlyHookTests
{
    private const int DisableOptimizations = 0x100; // DebuggableAttribute.DebuggingModes

    [Fact]
    public void TheLoadersDevBuildSwitchIsOnOnlyInADebugBuild()
    {
        var hookLayer = Scanned.Load(Rules.HookLayer);
        var isDevBuild = hookLayer.MainModule.GetType("Metaphor.Hooks.HookLoader").Properties
            .Single(property => property.Name == "IsDevBuild").GetMethod;
        var switchedOn = isDevBuild.Body.Instructions.First(i => i.OpCode.Code != Code.Nop).OpCode.Code == Code.Ldc_I4_1;

        var debuggable = hookLayer.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Diagnostics.DebuggableAttribute");
        var debugBuild = debuggable is not null && ((int)debuggable.ConstructorArguments[0].Value & DisableOptimizations) != 0;

        // CI and the package are Release builds, where this asserts the switch is off.
        Assert.Equal(debugBuild, switchedOn);
    }

    /// <summary>An opt-in hook (<c>OptInFile = "…"</c>) is a development-only one, and its file is named bare, so it is
    /// looked for only in the mod's folder (the loader ignores any other name, which would leave the hook off for good).
    /// </summary>
    [Fact]
    public void OptInHooksAreDevOnlyWithABareFileName()
    {
        var optIns = Scanned.Load(Rules.HookLayer).MainModule.Types
            .SelectMany(type => type.CustomAttributes
                .Where(a => a.AttributeType.FullName == "Metaphor.Hooks.GameHookAttribute")
                .Select(a => (type.FullName, Properties: a.Properties.ToDictionary(p => p.Name, p => p.Argument.Value))))
            .Where(hook => hook.Properties.ContainsKey("OptInFile"))
            .ToList();

        Assert.NotEmpty(optIns);
        Assert.All(optIns, hook =>
        {
            Assert.True(hook.Properties.GetValueOrDefault("DevOnly") is true, $"{hook.FullName} is opt-in but not DevOnly");
            var file = Assert.IsType<string>(hook.Properties["OptInFile"]);
            Assert.Matches("^[a-z0-9][a-z0-9.-]*$", file);
        });
    }
}
