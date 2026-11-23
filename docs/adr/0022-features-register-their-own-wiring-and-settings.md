---
status: accepted
---

# Each feature project registers its own wiring and settings; the settings reference is generated

Decided by the maintainer on 2026-11-22
([#784](https://github.com/Sour-Dev-Home/metaphor-accessibility/issues/784#issuecomment-7091426451)), because feature
PRs kept conflicting in `HookServices.cs`, the central `Feature` enum and `settings.md`.

- **Settings:** one enum per feature project. Each member's `[SettingKey]` carries its key, label, `Testing` mark,
  description and an `Order` that is unique across projects. Classes are marked `[FeatureSetting<TFeature>(...)]`
  (rule 18). The settings reader takes its keys from the modules (`SettingCatalogue`).
- **Wiring:** one `IFeatureModule` per project. `Register(FeatureContext)` gets the services the hook layer shares,
  builds its own features, and adds their controls to one ordered list. Game reads and hook ports cross by type
  (`Provide`/`Find`). The hook layer names the modules in an explicit list (`FeatureModules`), whose order is behaviour
  and is pinned by an architecture test.
- **Docs:** `tools/Metaphor.SettingsReference` writes all of `settings.md`, and `Metaphor.Settings.Tests` fails when the
  page is stale.

## Considered options

- **String paths in the attribute:** no compile-time check.
- **Generate only the key and default columns:** each description would still be a shared edit.
- **Modules fill `HookServices`' fields, or are found by an assembly scan:** a central list of slots, or an implicit
  order.

## Consequences

A new feature touches only its own project and the regenerated page. Shared services stay in the hook layer
([ADR 0003](0003-thin-hook-layer.md)), so it still names the `termNames` gate and the silent screen list. Name entry
is its own module after the dialogue's, to keep the order in which shown lines are heard. The general and key-binding
rows stay hand-written in the tool.
