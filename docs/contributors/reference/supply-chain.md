# Supply chain

The [security model](../explanation/security-model.md) gathers every security decision in one place.

Workflows use read-only tokens by default, and a job that writes asks for it itself. Every action is pinned to a
commit. [Dependabot](../../../.github/dependabot.yml) proposes NuGet and Actions updates weekly. CI's `security` job,
which the ruleset requires, runs CodeQL and fails on any package with a known vulnerability, read by a pinned
OSV-Scanner from the committed lock files ([`vuln-audit.sh`](../../../scripts/ci/vuln-audit.sh)). The ruleset also blocks
a PR that adds a high or critical CodeQL security alert; other alerts show in the Security tab without blocking. To
unblock a false positive, dismiss the alert there with a reason.
[Scorecard](../../../.github/workflows/scorecard.yml) runs weekly as a report only; its results are in the Security
tab. Bundled binaries are pinned by hash before they're packed (Prism by `scripts/dev/fetch-prism.ps1`, ADR 0005; NuGet
packages by lock files). Every `main` build's zip is attested, and only CI's `attest` job may ask for a signing token
(`tests/bash/test-release-integrity.sh`); the audit behind this is
[release integrity](../../raw-sources/release-integrity-audit-2026-11-18.md).
`tests/bash/test-repo-config.sh` checks these rules.
