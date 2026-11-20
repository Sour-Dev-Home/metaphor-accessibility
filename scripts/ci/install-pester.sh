#!/usr/bin/env bash
# CI's Pester install, shared by every job that runs Pester so they can't drift (#459): the pinned Pester
# (onboard.ps1's $pesterVersion, #456), installed as onboarding installs it, through install-pester.ps1, which refuses it
# unless every script and DLL file is signed by onboard.ps1's $pesterSigner (#477). Writes PESTER_PIN to $GITHUB_ENV
# when it is set, for the steps after it.
#
#   bash scripts/ci/install-pester.sh    from the repository root
set -euo pipefail

pin="$(sed -n "s/^\$pesterVersion = '\([0-9.]*\)'.*/\1/p" scripts/setup/onboard.ps1 | tr -d '\r')"
[ -n "$pin" ] || { echo "no \$pesterVersion line in scripts/setup/onboard.ps1"; exit 1; }
signer="$(sed -n "s/^\$pesterSigner = '\([0-9A-F]\{40\}\)'.*/\1/p" scripts/setup/onboard.ps1 | tr -d '\r')"
[ -n "$signer" ] || { echo "no \$pesterSigner line in scripts/setup/onboard.ps1"; exit 1; }
[ -z "${GITHUB_ENV:-}" ] || echo "PESTER_PIN=$pin" >> "$GITHUB_ENV"
pwsh -NoProfile -File scripts/setup/install-pester.ps1 -Version "$pin" -Signer "$signer"
