# Codebase review, 2026-11-22: spread-out features and deepening opportunities

Eleven findings, most valuable first. The three most urgent are cheap and come before the big refactors: two open PRs
promote the same hook in two different ways (finding 5), two others add the same message lookup twice (finding 4), and
every feature PR passes through one hook-layer file (finding 1). Terms follow the deep-module vocabulary (module,
interface, seam, adapter, depth, leverage, locality). Read on `origin/main` at `092bcf2`, the open branches listed in
each finding, and the local `617-597-welcome-report` branch.

Read-only review: nothing here has been built or tested. Line counts are rough. **[uncertain]** marks claims I couldn't
confirm from the code alone.

## Summary

| # | Finding | Value | Size | Hook layer? | When |
|---|---|---|---|---|---|
| 1 | Feature wiring lives in the hook layer (`HookServices.Speaking`) | High | L | Yes | After the queue drains; needs an ADR |
| 2 | Six copies of key-press edge detection; bindings per feature | High | M | No | After #661, #617, #631/#713 merge |
| 3 | `InputFrame` grows one optional parameter per input feature | High | S–M | Yes (`InputHooks`) | After #617, #689, #631 merge |
| 4 | Four message stores each parse every file; "file + position" added twice | High | M | No | **Now**: settle before #710 and #631 merge |
| 5 | The serial lookup hook promoted to Release twice, two ways | High | S | Yes | **Now**: before #707 or #631 merges |
| 6 | Feature gating and speech routing: four decorator layers wired by hand | Med–High | M | Yes (wiring) | After #661, #682, #617 merge |
| 7 | Screen identity: five mechanisms for "is this screen X" | Medium | M | Yes (`MenuHooks`) | After #707 and #631 merge |
| 8 | Settings-menu knowledge across six files in two projects and the hook layer | Medium | M | Yes (`MenuHooks`) | With 7 |
| 9 | `FieldControls` is becoming a god object | Medium | M | No | After the field stack (#667, #645, #709, #711, #714, #665) merges |
| 10 | Game state ("can a feature act now?") read in many shapes | Medium | M | Partly | After 3 and 9 |
| 11 | Small items: `MessageEvents` boilerplate, log subscriptions, pass-through gate, repeated clock | Low | S each | Mixed | Any time |

## 1. Feature wiring lives in the hook layer

**Modules.** `src/Metaphor.Hooks/HookServices.cs` (294 lines on main, 348 on `battle-banners`), `ModEntryPoint.cs`.

**Symptoms.**

- `HookServices.Speaking` builds every plain feature: the message library, the dialogue and menu features, field
  controls, travel map and tutorials. That's plain-library policy, but it sits in `src/Metaphor.Hooks/`, which is in the
  security-review scope (`scripts/ci/security-stamp.sh`, line 34). So every feature PR needs a security stamp for a
  change that patches nothing. On `origin/main...` diffs, 10 of the 12 open branches edit this file, which makes it the
  queue's main conflict point.
- The record has about 35 properties, and each new hook-facing sink adds one more (`TravelMap`, `FieldMapName`,
  `NameEntry`, `SlotScreens`, `LastDevice`, `BattleStatus`, `BattleDetails`, `BattleBanners`, `BattleHits`,
  `ReportControls`, `MissionTable`, ...). The interface is about as wide as the implementation, so it's shallow.
- Each game global is declared twice: once as a property default (`MenusOpen { get; init; } = new()`) and once as a
  local in `Speaking`, which must then be copied back in the initializer. The `Globals` list is a third hand-kept copy;
  `battle-banners` and `feat/658-mission-line` each edit it. Forgetting the copy-back fails silently: the property keeps
  a reader the loader locates, while the feature keeps the local, which is never located. **[uncertain]** whether the
  loader's tests catch that.
- Registration order carries meaning, and only comments say what it is: "hears each screen event after them"
  (`SilentScreens`), "the slot screens name them before the screens", `ObserveChoices(lines)` before `choices`. A
  reordering breaks behaviour, and no test fails.
- The same lambdas appear over and over: `() => Environment.TickCount64` (6 times on `battle-banners`) and
  `log is null ? null : log.FieldRead` (3 times on `feat/658-mission-line`).

**Deeper module.** Split the wiring into a plain composition project, for example `Metaphor.Composition`. It references
Core and the features, but not Reloaded and not the hook layer. Rule 5 of the architecture rules still holds, because
features don't reference each other; the composition project references them.

```csharp
// plain project: the whole mod's behaviour behind one call
public static ModFeatures Compose(GameReads game, ModOutputs outputs, SettingsReloader settings, ModLogs logs);
// ModFeatures exposes only what hooks feed: MessageEvents, CursorEvents, InputFrame, INameEntryObserver, ...
// GameReads: the IReadsGlobal instances, built once, listed once (Globals derives from it)
// ModOutputs: announcer, cues, clock (Func<long>)
```

Each feature area then gets one `Install(...)` function (dialogue, menus, field, battle) that registers its own observers
in its own order. `HookServices` shrinks to the game reads plus the sinks hooks call.

**Seam.** `Compose` is testable without the game. A test can build the whole mod over fake `GameReads` and a
`RecordingAnnouncer`, feed it a `MessageFileCompiled` and a `CursorInput`, and assert what's said. No such end-to-end
test exists today, because the wiring sits in an assembly that targets Windows.

**PRs and issues touched.** All 12 open branches, plus #617. ADR 0003 (thin hook layer) probably wants a short
amendment or a new ADR, and the architecture test project needs the new project added.

**Size and risk.** L (moving about 300 lines; new project; tests). It changes the hook layer, so it needs a security
review and a by-ear session to confirm nothing went silent. **Wait** until the current queue has merged; doing it now
conflicts with every open PR.

## 2. Key-press edge detection, copied six times

**Modules.** `DialogueControls`, `MenuControls` and `FieldControls`: their private `Binding` classes are byte-identical
(same md5 on main). Also `BattleKey` (on `247-battle-hits-lists` and `battle-banners`), `MarkKey` (#617) and
`NameEntrySpeech`'s `enterHeld`. Each keeps its own `Holds(buttons, binding)` too.

**Symptoms.**

- Six implementations of "pressed now, not held before, per source (pad index or keyboard)".
- They disagree. `BattleKey` matches `buttons == binding` and `keys.Count == 1 && keys[0] == key` exactly, while the
  others use a bitmask subset (`Holds`) and `keys.Contains`. A player holding another button while pressing the battle
  key gets nothing. **[uncertain]** whether that's intended.
- #661 makes dialogue, menu and field bindings rebindable through `X.From(settings)` with string keys
  (`settings.Binding("menus", "detailsKey", ...)`). The battle keys and the mark key aren't rebindable, because each
  feature declares its bindings its own way.
- Each control repeats the same choreography: compute held flags for pad and keys separately, update each `Binding`,
  pick the first pressed, then mask out the buttons or keys it consumed. `FieldControls.Pad` and `Keys` alone are about
  60 lines of it.

**Deeper module.** One key map in `Metaphor.Core.Input`:

```csharp
public sealed class KeyMap<TAction> where TAction : struct, Enum
{
    public KeyMap(Func<IReadOnlyDictionary<TAction, KeyBinding>> bindings);   // live, from settings
    public PadResult<TAction> Pad(int pad, int buttons, bool made, bool active); // pressed action + buttons to hide
    public KeysResult<TAction> Keys(IReadOnlyList<int> keys, bool active);      // pressed action + keys to hide
}
```

Each control declares an enum of actions and a handler; matching, per-source holding, masking and the thread lock live in
one place. Binding names in the settings file then follow from the enum, which removes `BindingNames.cs`'s hand list.

**Seam.** `KeyMap` is tested once, with every edge case (two pads, pad and keyboard together, a binding of 0, a chord).
The controls' tests then test actions, not bit-twiddling.

**PRs and issues touched.** #661, #617, #631 and #713 (battle), and every field branch (#667, #709, #711, #714 and #665
each edit `FieldControls.Pad`/`Keys`).

**Size and risk.** M (about 150 lines new, about 250 removed). Hook layer not touched. No by-ear session strictly
needed if the tests cover the edge cases; a quick by-ear check of each key is cheap insurance. **Wait** for #661 and
#617 (bindings) and the battle pair.

## 3. `InputFrame` grows one optional parameter per input feature

**Modules.** `src/Metaphor.Core/Input/InputFrame.cs` and `src/Metaphor.Hooks/InputHooks.cs` (`GameInputReads.Frame`).

**Symptoms.**

- The constructor is `(game, screens, menus, dialogue, field, watcher = null, padWatcher = null)`. Three open branches
  each append one more trailing optional parameter in parallel:
  - `system-popups` adds `LastInputDevice? lastDevice`.
  - #617 adds `IInputControls? report`.
  - `247-battle-hits-lists` adds `ChainedControls` and nests the battle keys inside `menus` in `InputHooks`.

  The three will conflict, and the chain order (report → menus → battle → dialogue → field) ends up split between
  `InputFrame` and `InputHooks`.
- The order is policy, but it's decided in `InputHooks.cs`, a security-scoped hook file, so every new key PR needs a
  security stamp.
- Watchers (`IKeyWatcher`, `IPadWatcher`, `LastInputDevice`) are three interfaces for one idea: "see each read,
  unchanged".

**Deeper module.**

```csharp
public InputFrame(IGameInputState game, MenuScreens screens, IWindowInputControls dialogue,
    IReadOnlyList<IInputControls> before, IReadOnlyList<IInputControls> after,
    IReadOnlyList<IInputWatcher> watchers);
// IInputWatcher { void Pad(int pad, int buttons, bool made); void Keys(IReadOnlyList<int> keys); }
```

Or simpler: an ordered `IReadOnlyList<IInputControls>`, with the dialogue window read done through a separate
`IWindowObserver`. The order is built in the composition (finding 1), so `InputHooks` only does
`services.Input.Pad(...)`. `ChainedControls` becomes unnecessary.

**Seam.** One test asserts the whole chain order, for example "the mark key sees F-key presses on every screen before
the menus", without building a hook.

**PRs and issues touched.** #617, #689, #631, #713.

**Size and risk.** S–M. It changes `InputHooks.cs` (hook layer): security review, and a by-ear session to check that
every key still reaches the game when it should. **Wait** until those three PRs merge, then do it before the next input
feature.

## 4. Four message stores, each parsing every compiled file

**Modules.** `MessageLibrary` (by label, by label hash), `MessageSerials` (by serial), `MessageFilesById` (by
`MessageReference`) and, on `108-109-placeholders`, `MessageFilePositions` (by file + position). All are in
`src/Metaphor.Core/Messages`.

**Symptoms.**

- Each is its own `IMessageFileObserver`, registered separately in `HookServices`, and each calls
  `MessageLibrary.Parse(file.Source)` again: four parses of every file the game compiles, on the game's thread.
- The same concept appears in parallel in two PRs. `108-109-placeholders` adds `MessageFilePositions.Find(file,
  position)` with `MessageFileShape(File, Count)`. `battle-banners` and `247-battle-hits-lists` add
  `IMessageInFile.FindInFile(fileName, position)` to `MessageFilesById`. Both mean "the message at position N of file
  F". They also key differently: the first uses the path from `message/` on (through `MessageSerials.FromMessageFolder`),
  the second `Path.GetFileName`. Whichever merges second adds a duplicate.
- A serial is itself a file plus a position (`SerialRange(File, First, Count)`), so `MessageSerials` could sit on the
  same store. `battle-banners` adds the item and skill files to `SerialRange` while #710 adds skill files to
  `GameTerms.Steam18330018` as `MessageFileShape`s. That's two tables of the same files, one per lookup.

**Deeper module.** One catalogue that hears each compiled file once:

```csharp
public sealed class MessageCatalogue : IMessageFileObserver,
    IMessageLookup, IMessageBySerial, IMessageByReference, IMessageInFile
{
    public MessageCatalogue(IEnumerable<SerialRange> serials);   // build-pinned table, one per build
    // Find(label), FindByLabelHash, FindBySerial, Find(MessageReference), FindInFile(file, position)
}
```

The narrow interfaces stay, so consumers don't change. The store, the parsing, the path normalization and the lock live
in one place.

**Seam.** One test feeds it a compiled file and checks all five lookups agree on the same message. Today each store has
its own tests and nothing checks they agree.

**PRs and issues touched.** #710 (#108, #109), #631 and #713 (#247, #692), #707 (via `MessageSerials`).

**Size and risk.** M (about 200 lines merged into one, tests consolidated). Hook layer not touched. **Do the decision
now**, not the refactor: pick one name and one path rule for "file + position" before #710 or #631 merges. The cheapest
fix is for #710 to use `IMessageInFile` (or the reverse) on whichever lands second. The full catalogue can wait.

## 5. The serial lookup hook, promoted to Release twice

**Modules.** `src/Metaphor.Hooks/NameLookupListeners.cs` (`GetMessageBySerialListener`), on `622-save-slots` (#707)
and `247-battle-hits-lists` (#631, with `battle-banners` #713 stacked on it).

**Symptoms.** Both branches drop `DevOnly = true` from the same hook, in different ways:

- #707 adds `HookServices.SlotScreens` and calls `slots?.Resolved(serial)` straight from the hook.
- #631 renames the hook to `GetMessageBySerialHook`, adds `SerialResolved` and `ISerialResolveObserver` to
  `MessageEvents`, and only reads memory when `events.SerialsWanted`.

That gives two notification paths for one function, against the spirit of architecture rule 13 (decode once, hand it to
observers). Merging both conflicts, and done badly it gives two Release consumers that read memory on every call.

**Proposal.** Keep #631's event. #707's `SlotScreens` becomes an `ISerialResolveObserver` registered with `MessageEvents`,
and the `SlotScreens` property goes away.

**Seam.** `SlotScreens` is then tested through `MessageEvents.Resolved` like the battle lists.

**Size and risk.** S. It's a hook-layer change either way, so it needs a security review; one by-ear check of the save
and load slots afterwards. **Now**: tell whichever of #707 and #631 merges second.

Related, not a design issue: `622-save-slots` and `system-popups` share commit `3621c92` (#623, game-over screen), and
`system-popups` changes `GameOverScreen`'s constructor afterwards. Merge `system-popups` (#689) first, or #707 conflicts
on `GameOverScreen` and `HookServices`.

## 6. Feature gating and speech routing, wired by hand

**Modules.** `HookServices.Speaking`, `SettingsReloader.Speech` and `Input` and the `FeatureSetting` attribute (#661),
`StoppableAnnouncer` and `SpeechSource` (`676-stop-on-close`, #682), `SilentScreens` (#617), `DialogueReader`
(`IReaderSwitch`).

**Symptoms.**

- A feature's speech path is assembled at its call site from up to four decorators: the reader switch (`reader` or raw
  `announcer`), the feature gate (`settings.Speech(Feature.X, ...)`), the window source (`windowSpeech.Through(...)`)
  and the silent-screen counter (`silence`). The order differs by call site and is decided in the composition root.
  Whether a feature goes through the reader or straight to the announcer is also chosen there: `ChoiceFocus` and
  `FieldControls` get `announcer`, the menus get `reader`. **[uncertain]** whether each choice is deliberate.
- Gating comes in four shapes. `settings.Speech` wraps an announcer. `settings.Input` wraps controls. `FieldControls`
  takes `feature => settings.Current.IsOn(feature)` and gates inside. `DialogueControls` gets
  `() => settings.Current.IsOn(Feature.Tutorials) ? tutorialPopups.OnScreen : null`.
- `Feature.cs`'s summary says a new feature needs "an attribute on its classes, a gate in the wiring and a line in"
  the settings reference. That's four places per feature. The architecture rule checks the attribute exists; it can't
  check that the wiring gates with the same `Feature`.

**Deeper module.** One factory that hands each feature its outputs by identity:

```csharp
public sealed class FeatureOutputs
{
    public IAnnouncer Voice(Feature feature, Route route = Route.Reader, SpeechSource? source = null);
    public IInputControls Keys(Feature feature, IInputControls controls);
    public Func<bool> IsOn(Feature feature);
}
```

It applies the decorators in one fixed, tested order: source → gate → reader → silence counter → stoppable → backend.
If constructors take `FeatureOutputs` plus their `Feature`, the attribute becomes derivable, or at least checkable
against the call.

**Seam.** One test per order invariant, such as "a gated-off feature is not counted as speaking by `SilentScreens`" or
"closing the window stops only window speech", instead of one test per call site.

**PRs and issues touched.** #668 (#661), #682 (#676), #617 (#597), and every PR that adds a speaking feature.

**Size and risk.** M. The wiring moves; the decorators stay. If done before finding 1, it edits `HookServices`, so it
needs a security review. A by-ear session is needed: a wrong order makes something silent or makes the reader toggle
miss it. **Wait** for #661, #676 and #617.

## 7. Screen identity: five mechanisms for "is this screen X"

**Modules.** `MenuScreens`, `ScreenModes`, `SettingsPages` (Core); `KnownScreens` and each `*Focus` (Features.Menus);
`ChoiceFocus` (Features.Dialogue); `SlotScreens` (#707); `MenuHooks` and `ScreenClassLookup` (hook layer).

**Symptoms.**

1. Reference equality on a feature's `Screen` record (`change.Screen == Screen`).
2. Class-string constants, duplicated because features can't share code:
   - `KeyConfigClass` in both `SettingsPages` and `SettingsConfirmQuestion`, with a stale "use that once it merges"
     comment.
   - `YesNoChoiceSample@ope@ui@app` in both `YesNoDialogFocus` and `ChoiceFocus`.
3. Predicates such as `SettingsPages.IsPage(class)`.
4. A `switch` on the class string in `ScreenModes.FieldOffset`.
5. Synthetic classes (`"slots:load"`, `"slots:save"`) that `SlotScreens` (#707) puts into the cursor stream after a
   prompt serial resolves. That's identity from a side effect, the pattern judgement rule 5 warns against.
   **[uncertain]**: the finding note may show there's no better identity yet; if so it should say so in the code.

`KnownScreens.All` is a hand list that #707, #713 and #631 each edit. `MenuHooks` branches on specific screens
(`screenClass == SettingsPages.KeyConfigClass`, `IsPage`), so adding a screen-specific datum means a hook-layer change.

**Deeper module.** A screen catalogue in `Metaphor.Core.Menus`:

```csharp
public sealed record ScreenDef(ScreenId Id, string ScreenClass, bool OutsideMenus, IReadOnlyList<ScreenRead> Reads);
public static class Screens { public static ScreenDef YesNo, KeyConfig, SettingsPage(int n), ...; }
// ScreenRead: { From: Manager | Owner, Offset, Length } — MenuHooks executes them generically
```

Features compare `change.Screen?.Id == ScreenId.KeyConfig`. Because the catalogue is in Core, `Dialogue` and `Menus`
share it without depending on each other. `MenuHooks` stops naming screens: it executes the reads the catalogue lists
and puts the bytes on `CursorInput`, and Core decodes them (`GridCursor`, `SettingValues`, key-config mode). After the
first change, a new screen datum stays out of the security scope.

**Seam.** The decoders are tested on byte arrays. The catalogue test asserts every class is unique and every focus
names a catalogued screen.

**PRs and issues touched.** #707, #631, #713, `96-settings-values` (already on main), #589.

**Size and risk.** M. It changes `MenuHooks` (hook layer, reads through `ModuleMemoryReader`): security review, and a
by-ear pass over the title, main, settings, key-config and battle-list screens. **Wait** for #707 and #631/#713.

## 8. Settings-menu knowledge across six files

**Modules.**

- `SettingsPages`: classes, working-copy offsets, key-config offsets, `SettingValues`, `ChangedBytes` (Core).
- `SettingsFocus`: page names by index into `SettingsPages.Classes`.
- `SettingsPageRows`, `KeyConfigTabs` and `SettingsConfirmQuestion` (Features.Menus).
- `MenuHooks`: the reads (hook layer).

**Symptoms.** The four settings pages are named by position (`SettingsPages.Classes[0]` is "System settings"), rows per
page sit in another table, values come through `ScreenEvent.Values` and the question comes from a pad watcher. Showing
one more fact about a settings row touches Core, the hook and two features. `ScreenEvent` and `CursorInput` gained
`Values`, `Grid` and `OnButton` one optional field at a time, and `MenuScreens` mirrors each in a private field
(`currentGrid`, `currentValues`, ...) and in its equality test.

**Deeper module.** With finding 7's catalogue, `MenuScreens` carries one opaque, equatable `ScreenState` instead of
mirrored fields. One `SettingsMenu` module in Features.Menus owns the page table (class, name, rows, value labels) and
answers `string? Say(ScreenEvent)`. `SettingsFocus`, `SettingsPageRows` and `KeyConfigTabs` become its internals.

**Seam.** One test per page: a `ScreenEvent` goes in and a sentence comes out.

**Size and risk.** M, done together with finding 7. The same hook-layer caveat applies.

## 9. `FieldControls` is becoming a god object

**Modules.** `src/Metaphor.Features.Field/FieldControls.cs` (238 lines on main) across `183-beacon`, `644-speak-reach`,
`702-field-quiet`, `690-path-beacon`, `697-687-nearby` and `feat/658-mission-line`.

**Symptoms.**

- Each branch appends a trailing optional constructor parameter: `ReachCallouts? reach` (#709), `nearbyNames` (#714),
  `routes` (#711), `mission`, plus `gameData.StoryFlag` (#665). After #667 it also owns a `Beacon` it builds itself, and
  ticks it from both `Pad` and `Keys`.
- One class handles the nearby list, stepping, "where am I", the guide/beacon, the reach call-outs check (in `Keys`),
  the mission line and debug flag dumps.
- `FreeRoamGate` (#709) is a one-line pass-through to `IFieldReads.InFreeRoam`. Its value is as the anchor for #709's
  new architecture rule 19, which is fine. But `feat/658-mission-line`'s `MissionLine` takes `field.InFreeRoam`
  directly, not the gate. **[uncertain]**: it may fail rule 19 once both merge.

**Deeper module.** Split the field feature into a key map (finding 2) plus field activities:

```csharp
internal interface IFieldActivity { void Tick(FieldMoment now); }   // beacon, reach call-outs, mission line, sonar
public sealed class FieldSession : IInputControls   // owns FreeRoamGate, the KeyMap, the activities
{
    public FieldSession(FieldBindings bindings, IFieldReads field, FieldOutputs outputs,
        IEnumerable<IFieldActivity> activities, IEnumerable<IFieldAction> actions);
}
```

The gate is applied once, by the session, to every activity, so rule 19 holds by construction. A new field feature
becomes a new activity, not a new constructor parameter.

**Seam.** Each activity is tested with a scripted `FieldMoment` sequence. The session test asserts that nothing ticks
outside free roam.

**PRs and issues touched.** #667 (#183), #645 (#644), #709 (#702), #711 (#690), #714 (#697, #687), #665 (#658).

**Size and risk.** M. Not in the hook layer, but a by-ear session is needed for the beacon and call-outs. **Wait** for
the whole field stack; every branch rewrites the same methods.

## 10. Game state read in many shapes

**Symptoms.** "Can this feature act now?" is answered in many shapes:

- `MenusOpen` and `DialogueWindow` (read by `InputFrame` on every read).
- `IFieldReads.InFreeRoam` (reads menus and window again) and `FreeRoamGate.Open`.
- `NameEntryUp` (a `Func<bool>` on `HookServices`) and `tutorialPopups.OnScreen`.
- `IBattleScene.InBattle` (`BattleKey`) and `CursorEvents.CalledRecently(class)` (#631).
- The window-closed lambda chain: `dialogue.EndConversation` plus `gameOver.WindowClosed`, composed inline in
  `HookServices` on #707 and #689.

Each new mode adds another `Func` and another ordering rule.

**Deeper module.** A `GameMoment` snapshot (menus open, window message, free roam, battle, name entry, pop-up), built
once per input read by `InputFrame` and handed to each control. Add a window-closed event on `MessageEvents` (or a
small `IWindowObserver`) instead of lambda chains. **[uncertain]**: the value depends on how many more modes come.
Revisit after findings 3 and 9.

**Size and risk.** M. It touches `InputHooks` (reads), so it needs a security review.

## 11. Small items

- **`MessageEvents` boilerplate.** Each event kind needs an interface, an `ObserveX`, an array and a `TellEach` line.
  #631 adds a sixth. A generic `Observe<T>(IObserver<T>)` and `Publish<T>(T)` would make that one line. Low value on
  its own; do it with finding 4.
- **Log subscriptions by hand.** `ReportLog` hears files, lines, choices and cursors. `MessageLog` also hears selects.
  Neither hears shown lines, and on #631 neither hears resolved serials. **[uncertain]** whether those gaps are
  intended. A log that observes "every event" through the generic bus would close them by construction.
- **`ListFocus.Of` reimplemented.** `QuickMenuFocus` repeats its guard (`change.Screen != Screen || Left ||
  Index < 0`). Small fix.
- **Clock passed around.** `() => Environment.TickCount64` is handed to six constructors. Pass one `Func<long>` (or
  `TimeProvider`) through the composition (finding 1).
- **`FreeRoamGate`.** It's a pass-through by the deletion test; keep it only as rule 19's anchor (see finding 9).

## Suggested order

1. **Now (cheap, saves conflicts):** finding 5 (one serial-resolve path) and the "file + position" decision in
   finding 4. Merge `system-popups` (#689) before #707.
2. **After the input and settings PRs merge (#661, #617, #689, #631/#713):** findings 2 and 3 together (key map and
   input chain), then 6.
3. **After the field stack merges:** finding 9.
4. **After the menu and battle PRs merge:** findings 7 and 8.
5. **Once the queue is quiet:** finding 1, with an ADR. It's the largest change but removes the main conflict point and
   takes most feature PRs out of the security-review scope. Doing findings 2, 3 and 6 first makes it smaller.
