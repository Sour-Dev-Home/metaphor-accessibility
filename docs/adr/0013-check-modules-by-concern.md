---
status: accepted
---

# `check.sh` keeps its entry point; its internals are modules by concern

Decided by the maintainer on 2026-11-19 (#364). `scripts/check.sh` stays the one check command: hooks, CI and tests
call it as before, and it stays Bash. Its internals move into modules by concern under `scripts/check/`, which
`check.sh` sources; `check.sh` keeps `main`, the constants and the loading. A reviewer reads the module a change
touches, not 1,300 lines, and parallel lanes stop colliding in one file.

Sourcing `check.sh` still defines every function without running `main`, so the tests reach the internals as before.

## State

`main` owns the run state: `stage`, `root`, `tmp`, `solution` and the failure and warning counts, which `report` and
`warn` update. Each module's header lists the run globals it reads. Everything else comes in as arguments, and a
global only one module uses stays private to it.

## Size

About 300 lines a module and 200 for `check.sh`. A guideline for review, not a test.

## Considered options

- **Keep one file.** Every review reads all of it, and lanes conflict in it.
- **Split into separate commands.** Hooks, CI and tests would each change how they call the checks.
