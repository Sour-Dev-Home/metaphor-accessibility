# Build and test a feature without the game

A feature is plain logic that the hook layer feeds events and that says things through an announcer
([ADR 0003](../../adr/0003-thin-hook-layer.md)), so you can build and test one with no game installed: hand it made-up
events and check what it said. This page walks through one small feature, the autosave notice (#543), from its first
failing test to a passing `full` check. You need only [a clone](set-up-a-clone.md).

What needs the game, and what doesn't:

| Part | Needs the game? |
|---|---|
| The feature's logic and its tests, its setting, its module wiring | No |
| Replaying a recorded session through it (`tests/fixtures/sessions/`) | No; someone recorded it once |
| Finding what the game does (the event, its values) | Yes, or an existing finding that pins it |
| A new hook, and the by-ear check | Yes ([add a hook](add-a-hook.md)) |

So start from an issue whose event is already pinned by a finding, here
[`autosave-notice-is-a-layout-animation.md`](../../findings/autosave-notice-is-a-layout-animation.md): every field
autosave plays animation 748 on the layout `AutoSavingLayout@ui@app`, which an existing hook already reports.

## 1. Write the failing test

Tests go in the feature project's tests, tagged `Unit`. `RecordingAnnouncer` (in `Metaphor.Core`) stands in for speech
and keeps what was said, in order. In `tests/Metaphor.Features.Menus.Tests/AutosaveNoticeTests.cs`:

```csharp
[Trait("Category", "Unit")]
public sealed class AutosaveNoticeTests
{
    private readonly RecordingAnnouncer announcer = new();

    [Fact]
    public void AnAutosaveIsSaidOnceAndQueued()
    {
        var notice = new AutosaveNotice(announcer);

        notice.Played(new LayoutAnimation("AutoSavingLayout@ui@app", 748));

        var said = Assert.Single(announcer.Said);
        Assert.Equal("Autosaving", said.Text);
        Assert.False(said.Interrupt);
    }
}
```

Add a test for what must stay silent too (another animation, another layout). Run only this file while you work:

```sh
dotnet test tests/Metaphor.Features.Menus.Tests --filter "FullyQualifiedName~AutosaveNotice"
```

It fails to build: `AutosaveNotice` doesn't exist yet. That's the red step.

## 2. Make it pass

Write the class in `src/Metaphor.Features.Menus/AutosaveNotice.cs`. It takes an `IAnnouncer` and implements the
observer interface for the events it needs, here `ILayoutAnimationObserver` from `Metaphor.Core.Menus`. Cite the finding
and the game build in its doc comment ([judgement rule 1](../../coding-standards.md#judgement-rules)). The real class is
the model: [`AutosaveNotice.cs`](../../../src/Metaphor.Features.Menus/AutosaveNotice.cs). Run the same test command;
it passes.

## 3. Give it a setting

Every feature can be turned off ([architecture rule 18](../../coding-standards.md#architecture-rules)):

1. Add a member to the project's feature enum, `MenusFeature`, with a `[SettingKey]` naming its key, label, `Order`
   and description. A new feature also gets `Testing = true`, which keeps it off until a by-ear check
   ([why](../../agents/workflow.md#a-feature-that-needs-a-by-ear-check)).
2. Mark the class `[FeatureSetting<MenusFeature>(MenusFeature.AutosaveNotice)]`.
3. Add its line to `tests/Metaphor.Settings.Tests/Snapshots/Metaphor.Features.Menus.txt`; the snapshot test's failure
   message prints the line it expects.
4. Regenerate the players' settings page:
   `dotnet run --project tools/Metaphor.SettingsReference -c Release -v q`.

## 4. Wire it in the module

The project's module (`MenusModule.Register`, [ADR 0022](../../adr/0022-features-register-their-own-wiring-and-settings.md))
builds the feature and hands it to the event source the hook layer feeds. The settings gate wraps its announcer:

```csharp
context.Layouts.Observe(new AutosaveNotice(settings.Speech(MenusFeature.AutosaveNotice, reader)));
```

`tests/Metaphor.Settings.Tests/FeatureModulesTests.cs` registers the real modules with no game and checks what they
wired; add an assertion there that your feature is listening (for the autosave notice,
`context.Layouts.Wants(AutosaveNotice.Layout)`). Neither step touches the hook layer, so no security review.

## 5. Optional: replay a recorded session

When a game session has been recorded for your feature's events, a replay test runs the fixture through the same seams
the hooks feed and checks what was said, in order. `MainMenuReplayTests` is the model; the fixtures are in
`tests/fixtures/sessions/`, cut from a session's message log as [add a hook](add-a-hook.md) step 5 describes. Without a
fixture, the made-up events from step 1 are enough.

## 6. Check and open the PR

Run `bash scripts/check.sh full` and read its summary line. Add a changelog fragment
([how](../../../changelog.d/README.md)) and open the PR as the [change workflow](../README.md#change-workflow) says. Mark
the issue's by-ear check as still to do: the maintainer or a tester hears it in a
[game session](run-a-game-session.md) before the `Testing` mark comes off.

`Metaphor.StandInGame`, in `tests/`, is not for features: it stands in for Reloaded-II's launcher and the game in the
tests of `scripts/dev/session.ps1`.
