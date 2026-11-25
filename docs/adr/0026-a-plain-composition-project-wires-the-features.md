---
status: accepted
---

# A plain composition project wires the features; HookServices hands over game services only

Proposed for [#726](https://github.com/Sour-Dev-Home/metaphor-accessibility/issues/726) (finding 1 of the
[codebase review](../contributors/explanation/codebase-review-2026-11-22.md)). Accepted by the maintainer on
2026-11-24 (on #726), after the speech factory (#723).

[ADR 0022](0022-features-register-their-own-wiring-and-settings.md) moved each feature's wiring into its own module.
The shared composition is still in the hook layer: `HookServices.Speaking` builds the message stores, the name and term
cleaners, the game reads and the `FeatureContext`, then copies about 35 ports back out. So a feature that adds a read
or a port edits the hook layer and needs a security review: 10 of the 43 feature commits since 2026-11-23 did.

A new plain project, `Metaphor.Composition`, which references Core and the features but not Reloaded or the hook
layer, takes over:

- the stores, cleaners and `FeatureContext` that `Speaking` builds now;
- the module list and its pinned order;
- one `GameReads` list of global readers, from which the hook layer's `Globals` comes.

`HookServices` keeps only what hooks read and the events they call. An end-to-end test can then compose the whole mod
over fake reads and assert what it says.

## Considered options

- **Keep it as is:** every feature read or port stays a hook-layer review.
- **Move the hook layer's review scope instead:** that weakens the review of code that runs in the game process.

## Consequences

The architecture tests add the project. Its first PR conflicts with every open feature branch, so it lands when the
queue is quiet. [#723](https://github.com/Sour-Dev-Home/metaphor-accessibility/issues/723) comes first.
