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

    /// <summary>An opt-in hook's file (<c>OptInFile = "…"</c>) is named bare, so it is looked for only in the mod's
    /// folder (the loader ignores any other name, which would leave the hook off for good). An opt-in hook needn't be
    /// development-only, though today's opt-in hooks all are (the dialogue window hooks became so with #232).
    /// </summary>
    [Fact]
    public void OptInHooksHaveABareFileName()
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
            var file = Assert.IsType<string>(hook.Properties["OptInFile"]);
            Assert.Matches("^[a-z0-9][a-z0-9.-]*$", file);
        });
    }

    /// <summary>The layout animation hook ships to players, so the cutscene skip menu can be spoken (maintainer's
    /// decision, 2026-11-23, #809); it is in <c>.ecode</c>, not <c>.text1</c>, so rule 15 doesn't hold it back.</summary>
    /// <remarks>So does the skip menu's request hook, which says the menu's question as it opens (the maintainer's
    /// decision of 2026-11-23 on #809, option (a)); it is in <c>.ecode</c> too. So does the report line open, which
    /// says the corner advice (#587, a read-only Release hook in <c>.ecode</c>).</remarks>
    [Theory]
    [InlineData("Metaphor.Hooks.LayoutAnimationListener")]
    [InlineData("Metaphor.Hooks.MoviePauseRequestListener")]
    [InlineData("Metaphor.Hooks.ReportMessageOpenHook")]
    public void TheLayoutAndSkipMenuHooksShipInRelease(string hook)
    {
        var attribute = Scanned.Load(Rules.HookLayer).MainModule.GetType(hook)
            .CustomAttributes.Single(a => a.AttributeType.FullName == "Metaphor.Hooks.GameHookAttribute");

        Assert.DoesNotContain(attribute.Properties, p => p.Name is "DevOnly" or "OptInFile");
    }

    /// <summary>The dialogue window hooks sit in .text1, so even a Debug build leaves them out unless given
    /// <c>opt-in-dialogue-window.txt</c> (maintainer, 2026-11-16).</summary>
    [Theory]
    [InlineData("Metaphor.Hooks.DialogueWindowOpenHook")]
    [InlineData("Metaphor.Hooks.DialogueWindowCloseHook")]
    public void TheDialogueWindowHooksAreOptIn(string hook)
    {
        var attribute = Scanned.Load(Rules.HookLayer).MainModule.GetType(hook).CustomAttributes
            .Single(a => a.AttributeType.FullName == "Metaphor.Hooks.GameHookAttribute");

        Assert.Equal("opt-in-dialogue-window.txt",
            attribute.Properties.Single(p => p.Name == "OptInFile").Argument.Value);
    }

    /// <summary>The window pointer drives the dialogue controls (#232); the dialogue window hooks are only Debug evidence
    /// beside it, so a Release build never installs them, opt-in file or not.</summary>
    [Theory]
    [InlineData("Metaphor.Hooks.DialogueWindowOpenHook")]
    [InlineData("Metaphor.Hooks.DialogueWindowCloseHook")]
    public void TheDialogueWindowHooksAreDevelopmentOnly(string hook)
    {
        var attribute = Scanned.Load(Rules.HookLayer).MainModule.GetType(hook).CustomAttributes
            .Single(a => a.AttributeType.FullName == "Metaphor.Hooks.GameHookAttribute");

        Assert.Equal(true, attribute.Properties.SingleOrDefault(p => p.Name == "DevOnly").Argument.Value);
    }
}
