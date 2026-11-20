---
status: accepted
---

# The review scope names directories, and each rule has one home

Decided by the maintainer on 2026-11-19 (#409, decisions 1 and 2; built in #410).

## Scope by directory

The security-review scope in `scripts/ci/security-stamp.sh` names directories (`scripts/`, the hook project, the
workflows, the check tests, the pinned tools) and the few files that decide `dotnet restore`, not about 45 paths. A
file added under a scoped directory is in scope with no list edit, so a new script can't land unreviewed because
nobody listed it. `scripts/inspect.ps1` and `scripts/signature-spike.ps1`, the only scripts the old list left out,
come in. There are no exclusions; one would need its reason next to it.

The cost is a review for script changes that touch neither the game nor the gate. That is cheap next to a guard
slipping out of the list.

## One home per rule

`AGENTS.md` states a rule in one line, one page holds its procedure or list, and everything else links to it, agent
prompts included. The scope's home is the stamp script; the docs name what it covers in a phrase and link there.
Config whose place a tool fixes (the skill settings in `docs/agents/`) is the exception.

## Considered options

- **Keep the path list, with the test catching gaps.** The test only follows what the gate runs, so a new deploy or
  install script still needed someone to remember the list.
- **Scope by directory with exclusions for dev-only scripts.** Every exclusion is a judgement a reviewer has to
  re-check; none was needed.
